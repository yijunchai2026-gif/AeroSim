#!/usr/bin/env python3
"""Contract tests for the read-only AeroSim Runtime MCP subset."""

import importlib.util
import json
import tempfile
import threading
import time
import unittest
from pathlib import Path
from unittest import mock

MODULE_PATH = Path(__file__).resolve().parents[2] / "Tools" / "AeroSimReviewMcp" / "aerosim_review_mcp.py"
SPEC = importlib.util.spec_from_file_location("aerosim_review_mcp_runtime", MODULE_PATH)
MCP = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MCP)

EXPECTED_RUNTIME_TOOLS = {
    "inspect_scene", "capture_game_view", "get_console_logs", "get_performance_metrics", "validate_scene",
}


class RuntimeMcpContractTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.runtime_root = Path(self.temporary.name) / "AeroSimReview"
        self.server = MCP.McpServer(mode="runtime", runtime_root=self.runtime_root, timeout=0.01)

    def test_runtime_tool_list_is_read_only_and_minimal(self):
        reply = self.server.dispatch({"jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {}})
        tools = reply["result"]["tools"]
        self.assertEqual(EXPECTED_RUNTIME_TOOLS, {tool["name"] for tool in tools})
        self.assertNotIn("set_transform", {tool["name"] for tool in tools})
        for tool in tools:
            self.assertEqual({"capture"}, set(tool["inputSchema"]["properties"]))
            self.assertFalse(tool["inputSchema"]["additionalProperties"])
            capture = tool["inputSchema"]["properties"]["capture"]
            self.assertEqual({"width", "height", "includeUi"}, set(capture["properties"]))
            self.assertFalse(capture["additionalProperties"])

    def test_runtime_mutation_is_rejected_before_queue_creation(self):
        with self.assertRaisesRegex(MCP.HarnessError, "non-read-only runtime tool"):
            MCP.call_runtime(self.runtime_root, "set_transform", {}, timeout=0)
        self.assertFalse(self.runtime_root.exists())

    def test_runtime_arguments_reject_paths_and_unknown_fields(self):
        with self.assertRaisesRegex(MCP.HarnessError, "Unknown runtime argument"):
            MCP.call_runtime(self.runtime_root, "inspect_scene", {"assetPath": "Assets/x"}, timeout=0)
        self.assertFalse(self.runtime_root.exists())

    def test_runtime_capture_contract_is_bounded(self):
        with self.assertRaisesRegex(MCP.HarnessError, "1..4096"):
            MCP.call_runtime(self.runtime_root, "capture_game_view", {"capture": {"width": 5000}}, timeout=0)
        with self.assertRaisesRegex(MCP.HarnessError, "includeUi"):
            MCP.call_runtime(self.runtime_root, "capture_game_view", {"capture": {"includeUi": "yes"}}, timeout=0)

    def test_runtime_root_must_be_fixed_aerosim_review_directory(self):
        with self.assertRaisesRegex(MCP.HarnessError, "persistentDataPath/AeroSimReview"):
            MCP.runtime_transport_directories(Path(self.temporary.name) / "arbitrary")

    def test_runtime_request_uses_separate_protocol_and_consumes_response(self):
        def respond():
            inbox = self.runtime_root / "inbox"
            outbox = self.runtime_root / "outbox"
            deadline = time.monotonic() + 1
            while time.monotonic() < deadline:
                requests = list(inbox.glob("*.json")) if inbox.exists() else []
                if requests:
                    request = json.loads(requests[0].read_text())
                    self.assertEqual(MCP.RUNTIME_PROTOCOL, request["protocol"])
                    self.assertIn(request["tool"], EXPECTED_RUNTIME_TOOLS)
                    requests[0].unlink()
                    response = {
                        "protocol": MCP.RUNTIME_PROTOCOL,
                        "requestId": request["id"],
                        "tool": request["tool"],
                        "success": True,
                        "status": "OK",
                    }
                    (outbox / f"{request['id']}-run.json").write_text(json.dumps(response))
                    return
                time.sleep(0.001)

        worker = threading.Thread(target=respond)
        worker.start()
        response = MCP.call_runtime(self.runtime_root, "validate_scene", {}, timeout=1, poll_interval=0.001)
        worker.join()
        self.assertTrue(response["success"])
        self.assertEqual([], list((self.runtime_root / "outbox").glob("*.json")))

    def test_invalid_runtime_response_is_preserved_for_diagnostics(self):
        def respond():
            inbox = self.runtime_root / "inbox"
            outbox = self.runtime_root / "outbox"
            while True:
                requests = list(inbox.glob("*.json")) if inbox.exists() else []
                if requests:
                    request_id = json.loads(requests[0].read_text())["id"]
                    requests[0].unlink()
                    (outbox / f"{request_id}-invalid.json").write_text("{invalid json")
                    return
                time.sleep(0.001)

        worker = threading.Thread(target=respond)
        worker.start()
        with self.assertRaisesRegex(MCP.HarnessError, "Invalid runtime response"):
            MCP.call_runtime(self.runtime_root, "inspect_scene", {}, timeout=1, poll_interval=0.001)
        worker.join()
        self.assertEqual(1, len(list((self.runtime_root / "outbox").glob("*-invalid.json"))))

    def test_runtime_response_cleanup_failure_returns_result_with_warning(self):
        original_unlink = Path.unlink

        def fail_outbox_cleanup(path, *args, **kwargs):
            if path.parent.name == "outbox":
                raise PermissionError("cleanup denied")
            return original_unlink(path, *args, **kwargs)

        def respond():
            inbox = self.runtime_root / "inbox"
            outbox = self.runtime_root / "outbox"
            while True:
                requests = list(inbox.glob("*.json")) if inbox.exists() else []
                if requests:
                    request = json.loads(requests[0].read_text())
                    requests[0].unlink()
                    response = {"requestId": request["id"], "success": True, "status": "OK"}
                    (outbox / f"{request['id']}-run.json").write_text(json.dumps(response))
                    return
                time.sleep(0.001)

        worker = threading.Thread(target=respond)
        worker.start()
        with mock.patch.object(Path, "unlink", fail_outbox_cleanup):
            response = MCP.call_runtime(self.runtime_root, "inspect_scene", {}, timeout=1, poll_interval=0.001)
        worker.join()
        self.assertTrue(response["success"])
        self.assertIn("cleanup failed", response["transportWarnings"][0])
        self.assertEqual(1, len(list((self.runtime_root / "outbox").glob("*.json"))))

    def test_runtime_timeout_cancels_unconsumed_request(self):
        with self.assertRaisesRegex(MCP.RequestTimeout, "waiting for runtime response"):
            MCP.call_runtime(self.runtime_root, "inspect_scene", {}, timeout=0.01, poll_interval=0.001)
        self.assertEqual([], list((self.runtime_root / "inbox").glob("*.json")))

    def test_editor_contract_remains_available_by_default(self):
        editor_tools = {tool["name"] for tool in MCP.tool_definitions()}
        self.assertIn("set_transform", editor_tools)
        self.assertIn("save_scene", editor_tools)
        self.assertNotIn("validate_scene", editor_tools)


if __name__ == "__main__":
    unittest.main()
