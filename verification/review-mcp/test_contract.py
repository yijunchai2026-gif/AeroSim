#!/usr/bin/env python3
"""Contract tests for the dependency-free AeroSim Review MCP bridge."""

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[2] / "Tools" / "AeroSimReviewMcp" / "aerosim_review_mcp.py"
SPEC = importlib.util.spec_from_file_location("aerosim_review_mcp", MODULE_PATH)
MCP = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MCP)

EXPECTED_TOOLS = {
    "capture_scene_view", "capture_game_view", "capture_camera", "capture_diagnostic_bundle",
    "inspect_scene", "inspect_object", "inspect_material", "inspect_lod_group", "inspect_colliders",
    "inspect_prefab", "get_console_logs", "get_shader_errors", "get_performance_metrics",
    "raycast_from_view", "list_visible_objects", "run_validator", "set_transform", "set_camera",
    "set_light_property", "set_material_property", "set_lod_thresholds", "add_or_update_collider",
    "toggle_object", "undo_change", "rollback_transaction", "replace_material", "replace_mesh",
    "add_component", "update_component", "edit_prefab", "update_model_importer", "reimport_asset",
    "run_asset_gate", "save_scene", "save_prefab",
}


class ReviewMcpContractTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.project = Path(self.temporary.name) / "AeroSim"
        (self.project / "Assets").mkdir(parents=True)
        (self.project / "ProjectSettings").mkdir()
        self.server = MCP.McpServer(self.project, timeout=0.01)

    def test_tools_list_matches_shared_contract(self):
        reply = self.server.dispatch({"jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {}})
        tools = reply["result"]["tools"]
        self.assertEqual(EXPECTED_TOOLS, {tool["name"] for tool in tools})
        self.assertEqual(len(EXPECTED_TOOLS), len(tools))
        for tool in tools:
            self.assertEqual("object", tool["inputSchema"]["type"])
            self.assertFalse(tool["inputSchema"]["additionalProperties"])

    def test_path_traversal_is_rejected_before_enqueue(self):
        with self.assertRaisesRegex(MCP.HarnessError, "escapes the Unity project"):
            MCP.call_unity(self.project, "inspect_prefab", {"assetPath": "../../outside.asset"}, timeout=0)
        self.assertFalse((self.project / "Library").exists())

    def test_unknown_tool_is_rejected_before_enqueue(self):
        with self.assertRaisesRegex(MCP.HarnessError, "Unknown tool"):
            MCP.call_unity(self.project, "delete_project", {}, timeout=0)
        self.assertFalse((self.project / "Library").exists())

    def test_timeout_is_explicit_and_request_is_cancelled_if_unconsumed(self):
        with self.assertRaisesRegex(MCP.RequestTimeout, r"Timed out after 0\.01s waiting for Unity response"):
            MCP.call_unity(self.project, "inspect_scene", {}, timeout=0.01, poll_interval=0.001)
        inbox = self.project / "Library" / "AeroSimReview" / "inbox"
        self.assertEqual([], list(inbox.glob("*.json")))

    def test_tools_call_reports_timeout_as_mcp_tool_error(self):
        reply = self.server.dispatch({
            "jsonrpc": "2.0", "id": "timeout", "method": "tools/call",
            "params": {"name": "inspect_scene", "arguments": {}},
        })
        self.assertTrue(reply["result"]["isError"])
        self.assertIn("Timed out", reply["result"]["content"][0]["text"])

    def test_initialize_and_initialized_notification(self):
        reply = self.server.dispatch({"jsonrpc": "2.0", "id": 7, "method": "initialize", "params": {}})
        self.assertEqual(MCP.MCP_PROTOCOL_VERSION, reply["result"]["protocolVersion"])
        self.assertIsNone(self.server.dispatch({"jsonrpc": "2.0", "method": "notifications/initialized"}))


if __name__ == "__main__":
    unittest.main()
