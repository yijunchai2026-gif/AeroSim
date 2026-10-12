#!/usr/bin/env python3
"""Zero-dependency MCP stdio bridge for the AeroSim Unity review harness."""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
import uuid
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional

PROTOCOL = "aerosim.review-mcp/v1"
MCP_PROTOCOL_VERSION = "2024-11-05"
SERVER_NAME = "aerosim-review-mcp"
SERVER_VERSION = "1.0.0"

READ_TOOLS = (
    "capture_scene_view", "capture_game_view", "capture_camera", "capture_diagnostic_bundle",
    "inspect_scene", "inspect_object", "inspect_material", "inspect_lod_group", "inspect_colliders",
    "inspect_prefab", "get_console_logs", "get_shader_errors", "get_performance_metrics",
    "raycast_from_view", "list_visible_objects", "run_validator",
)
MUTATION_TOOLS = (
    "set_transform", "set_camera", "set_light_property", "set_material_property",
    "set_lod_thresholds", "add_or_update_collider", "toggle_object", "undo_change",
    "rollback_transaction", "replace_material", "replace_mesh", "add_component",
    "update_component", "edit_prefab", "update_model_importer", "reimport_asset",
    "run_asset_gate", "save_scene", "save_prefab",
)
ALL_TOOLS = READ_TOOLS + MUTATION_TOOLS
ALL_TOOL_SET = frozenset(ALL_TOOLS)

VECTOR_SCHEMA = {
    "type": "object",
    "properties": {"x": {"type": "number"}, "y": {"type": "number"}, "z": {"type": "number"}},
    "required": ["x", "y", "z"],
    "additionalProperties": False,
}
COLOR_SCHEMA = {
    "type": "object",
    "properties": {
        "r": {"type": "number"}, "g": {"type": "number"},
        "b": {"type": "number"}, "a": {"type": "number"},
    },
    "required": ["r", "g", "b", "a"],
    "additionalProperties": False,
}
CAPTURE_SCHEMA = {
    "type": "object",
    "properties": {
        "width": {"type": "integer", "minimum": 1},
        "height": {"type": "integer", "minimum": 1},
        "cameraPath": {"type": "string"},
        "channels": {"type": "array", "items": {"type": "string"}},
        "includeUi": {"type": "boolean"},
    },
    "additionalProperties": False,
}
INPUT_SCHEMA = {
    "type": "object",
    "properties": {
        "target": {"type": "string"},
        "transactionId": {"type": "string"},
        "property": {"type": "string"},
        "stringValue": {"type": "string"},
        "assetPath": {"type": "string", "description": "Unity project path, normally under Assets/."},
        "secondaryAssetPath": {"type": "string", "description": "Second Unity project path, normally under Assets/."},
        "componentType": {"type": "string"},
        "vectorValue": VECTOR_SCHEMA,
        "rotationEuler": VECTOR_SCHEMA,
        "colorValue": COLOR_SCHEMA,
        "floatValue": {"type": "number"},
        "intValue": {"type": "integer"},
        "boolValue": {"type": "boolean"},
        "floatValues": {"type": "array", "items": {"type": "number"}},
        "arguments": {"type": "array", "items": {"type": "string"}},
        "capture": CAPTURE_SCHEMA,
    },
    "additionalProperties": False,
}


class HarnessError(RuntimeError):
    """A request that cannot safely be handed to Unity."""


class RequestTimeout(HarnessError):
    """Unity did not write the expected response before the deadline."""


def _is_unity_project(path: Path) -> bool:
    return path.is_dir() and (path / "Assets").is_dir() and (path / "ProjectSettings").is_dir()


def _ancestors(path: Path) -> Iterable[Path]:
    yield path
    yield from path.parents


def locate_project(explicit: Optional[str] = None) -> Path:
    """Locate a Unity root using bounded, deterministic candidates (never a global search)."""
    if explicit:
        candidate = Path(explicit).expanduser().resolve()
        if not _is_unity_project(candidate):
            raise HarnessError(f"Not a Unity project (Assets/ and ProjectSettings/ required): {candidate}")
        return candidate

    env_project = os.environ.get("AEROSIM_UNITY_PROJECT")
    if env_project:
        return locate_project(env_project)

    script_repo = Path(__file__).resolve().parents[2]
    candidates: List[Path] = []
    for base in _ancestors(Path.cwd().resolve()):
        candidates.extend((base, base / "AeroSim"))
    candidates.extend((script_repo, script_repo / "AeroSim"))

    seen = set()
    for candidate in candidates:
        resolved = candidate.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        if _is_unity_project(resolved):
            return resolved
    raise HarnessError("Unable to locate the AeroSim Unity project; pass --project or set AEROSIM_UNITY_PROJECT")


def _inside(child: Path, parent: Path) -> bool:
    try:
        child.relative_to(parent)
        return True
    except ValueError:
        return False


def transport_directories(project: Path, create: bool = True) -> tuple[Path, Path]:
    project = project.resolve()
    base = project / "Library" / "AeroSimReview"
    inbox, outbox = base / "inbox", base / "outbox"
    # Path.resolve(strict=False) follows any existing symlink before mkdir can write through it.
    for path in (base.resolve(), inbox.resolve(), outbox.resolve()):
        if not _inside(path, project):
            raise HarnessError(f"Transport path escapes Unity project: {path}")
    if create:
        inbox.mkdir(parents=True, exist_ok=True)
        outbox.mkdir(parents=True, exist_ok=True)
    return inbox.resolve(), outbox.resolve()


def validate_arguments(arguments: Any, project: Path) -> Dict[str, Any]:
    if arguments is None:
        return {}
    if not isinstance(arguments, dict):
        raise HarnessError("Tool arguments must be a JSON object")
    unknown = sorted(set(arguments) - set(INPUT_SCHEMA["properties"]))
    if unknown:
        raise HarnessError("Unknown argument field(s): " + ", ".join(unknown))

    for field in ("assetPath", "secondaryAssetPath"):
        value = arguments.get(field)
        if value is None or value == "":
            continue
        if not isinstance(value, str):
            raise HarnessError(f"{field} must be a string")
        normalized = value.replace("\\", "/")
        raw = Path(normalized)
        if raw.is_absolute() or any(part == ".." for part in raw.parts):
            raise HarnessError(f"{field} escapes the Unity project: {value}")
        resolved = (project / raw).resolve()
        if not _inside(resolved, project.resolve()):
            raise HarnessError(f"{field} escapes the Unity project: {value}")
    return dict(arguments)


def tool_definitions() -> List[Dict[str, Any]]:
    result = []
    for name in ALL_TOOLS:
        kind = "Read-only review" if name in READ_TOOLS else "Mutation (Unity Undo/transaction boundary applies)"
        result.append({"name": name, "description": f"{kind} tool forwarded to the AeroSim Unity Editor bridge.", "inputSchema": INPUT_SCHEMA})
    return result


def call_unity(project: Path, tool: str, arguments: Any, timeout: float = 30.0, poll_interval: float = 0.05) -> Dict[str, Any]:
    if tool not in ALL_TOOL_SET:
        raise HarnessError(f"Unknown tool: {tool}")
    if timeout < 0:
        raise HarnessError("Timeout must be non-negative")
    project = project.resolve()
    safe_arguments = validate_arguments(arguments, project)
    inbox, outbox = transport_directories(project)
    request_id = uuid.uuid4().hex
    request = {"protocol": PROTOCOL, "id": request_id, "tool": tool}
    request.update(safe_arguments)
    request_path = inbox / f"{request_id}.json"
    response_path = outbox / f"{request_id}.json"
    temporary_path = inbox / f".{request_id}.tmp"

    encoded = json.dumps(request, ensure_ascii=False, separators=(",", ":"))
    with temporary_path.open("x", encoding="utf-8") as stream:
        stream.write(encoded)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary_path, request_path)

    deadline = time.monotonic() + timeout
    while True:
        if response_path.is_file():
            try:
                with response_path.open("r", encoding="utf-8") as stream:
                    response = json.load(stream)
            except (OSError, json.JSONDecodeError) as exc:
                raise HarnessError(f"Invalid Unity response for request {request_id}: {exc}") from exc
            if not isinstance(response, dict):
                raise HarnessError(f"Invalid Unity response for request {request_id}: expected object")
            if response.get("requestId") not in (None, request_id):
                raise HarnessError(f"Unity response requestId mismatch for request {request_id}")
            return response
        if time.monotonic() >= deadline:
            # Cancel only if Unity has not consumed the inbox file. Once consumed, the operation may still finish.
            try:
                request_path.unlink()
            except FileNotFoundError:
                pass
            raise RequestTimeout(f"Timed out after {timeout:g}s waiting for Unity response to request {request_id} ({tool})")
        time.sleep(min(poll_interval, max(0.0, deadline - time.monotonic())))


def _tool_result(response: Dict[str, Any]) -> Dict[str, Any]:
    return {
        "content": [{"type": "text", "text": json.dumps(response, ensure_ascii=False, indent=2)}],
        "structuredContent": response,
        "isError": not bool(response.get("success", False)),
    }


class McpServer:
    def __init__(self, project: Path, timeout: float = 30.0):
        self.project = project.resolve()
        self.timeout = timeout

    def dispatch(self, message: Any) -> Optional[Dict[str, Any]]:
        if not isinstance(message, dict) or message.get("jsonrpc") != "2.0" or not isinstance(message.get("method"), str):
            return self._error(message.get("id") if isinstance(message, dict) else None, -32600, "Invalid Request")
        request_id = message.get("id")
        method = message["method"]
        if method == "notifications/initialized":
            return None
        if "id" not in message:
            return None
        if method == "initialize":
            result = {
                "protocolVersion": MCP_PROTOCOL_VERSION,
                "capabilities": {"tools": {"listChanged": False}},
                "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION},
            }
            return self._result(request_id, result)
        if method == "tools/list":
            return self._result(request_id, {"tools": tool_definitions()})
        if method == "tools/call":
            params = message.get("params", {})
            if not isinstance(params, dict) or not isinstance(params.get("name"), str):
                return self._error(request_id, -32602, "tools/call requires string params.name")
            try:
                response = call_unity(self.project, params["name"], params.get("arguments", {}), self.timeout)
                return self._result(request_id, _tool_result(response))
            except HarnessError as exc:
                result = {"content": [{"type": "text", "text": str(exc)}], "isError": True}
                return self._result(request_id, result)
        return self._error(request_id, -32601, f"Method not found: {method}")

    @staticmethod
    def _result(request_id: Any, result: Any) -> Dict[str, Any]:
        return {"jsonrpc": "2.0", "id": request_id, "result": result}

    @staticmethod
    def _error(request_id: Any, code: int, message: str) -> Dict[str, Any]:
        return {"jsonrpc": "2.0", "id": request_id, "error": {"code": code, "message": message}}

    def serve(self) -> int:
        for line in sys.stdin:
            if not line.strip():
                continue
            try:
                message = json.loads(line)
                reply = self.dispatch(message)
            except json.JSONDecodeError as exc:
                reply = self._error(None, -32700, f"Parse error: {exc.msg}")
            except Exception as exc:  # Keep the stdio server alive without contaminating stdout.
                print(f"Unexpected server error: {exc}", file=sys.stderr, flush=True)
                reply = self._error(None, -32603, "Internal error")
            if reply is not None:
                print(json.dumps(reply, ensure_ascii=False, separators=(",", ":")), flush=True)
        return 0


def _json_argument(value: str) -> Dict[str, Any]:
    try:
        parsed = json.loads(value)
    except json.JSONDecodeError as exc:
        raise argparse.ArgumentTypeError(f"invalid JSON: {exc}") from exc
    if not isinstance(parsed, dict):
        raise argparse.ArgumentTypeError("arguments must decode to a JSON object")
    return parsed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command")
    for command in ("serve", "health"):
        item = subparsers.add_parser(command)
        item.add_argument("--project")
        if command == "serve":
            item.add_argument("--timeout", type=float, default=30.0)
    request = subparsers.add_parser("request")
    request.add_argument("tool", choices=ALL_TOOLS)
    request.add_argument("--arguments", type=_json_argument, default={})
    request.add_argument("--project")
    request.add_argument("--timeout", type=float, default=30.0)
    return parser


def main(argv: Optional[List[str]] = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    command = args.command or "serve"
    try:
        project = locate_project(getattr(args, "project", None))
        if command == "health":
            inbox, outbox = transport_directories(project)
            print(json.dumps({"ok": True, "protocol": PROTOCOL, "project": str(project), "inbox": str(inbox), "outbox": str(outbox)}, ensure_ascii=False, indent=2))
            return 0
        if command == "request":
            response = call_unity(project, args.tool, args.arguments, args.timeout)
            print(json.dumps(response, ensure_ascii=False, indent=2))
            return 0 if response.get("success", False) else 1
        return McpServer(project, getattr(args, "timeout", 30.0)).serve()
    except HarnessError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
