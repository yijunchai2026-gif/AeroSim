#!/usr/bin/env python3
"""P05 manifest static validator; uses only Python standard library."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
MANIFEST_DIR = ROOT / "manifests"
EXPECTED = {
    "P05_IF_PANEL", "P05_IF_GUARDRAIL", "P05_IF_GRATING", "P05_IF_PIPE_RACK",
    "P05_IF_BEACON", "P05_IF_CABLE", "P05_IF_BOLT_SET", "P05_IF_HINGE",
    "P05_IF_SALT_OXIDATION", "P05_IF_COLLISION_PROXY",
}
REQUIRED = {
    "schema_version", "package_id", "asset_id", "display_name_zh", "category",
    "source_reference", "modular_kit", "pivot", "snap_grid_cm", "pbr", "lod",
    "collision", "deliverables",
}
errors: list[str] = []
seen: set[str] = set()
files = sorted(MANIFEST_DIR.glob("*.json"))
if len(files) != 10:
    errors.append(f"manifest 数量应为 10，实际为 {len(files)}")
for path in files:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        errors.append(f"{path.name}: JSON 解析失败: {exc}")
        continue
    missing = REQUIRED - data.keys()
    if missing:
        errors.append(f"{path.name}: 缺少字段 {sorted(missing)}")
        continue
    aid = data["asset_id"]
    seen.add(aid)
    if data["schema_version"] != "1.0.0" or data["package_id"] != "P05_INDUSTRIAL_FACILITIES":
        errors.append(f"{path.name}: 版本或 package_id 不匹配")
    if data["source_reference"].get("path") != "../source/generated_image_035559.png":
        errors.append(f"{path.name}: 参考图相对路径不正确")
    if len(data["lod"].get("triangle_budget", [])) != 4:
        errors.append(f"{path.name}: 必须定义 LOD0-3 四档三角面预算")
    if data["lod"].get("screen_ratio") != [0.35, 0.16, 0.07, 0.025]:
        errors.append(f"{path.name}: LOD 屏占比不符合统一标准")
    for key in ("metallic", "roughness"):
        values = data["pbr"].get(key, [])
        if len(values) != 2 or not all(0 <= x <= 1 for x in values) or values[0] > values[1]:
            errors.append(f"{path.name}: PBR {key} 范围非法")
    grid = data["snap_grid_cm"]
    if grid.get("translation", 0) <= 0 or grid.get("rotation_deg", 0) <= 0:
        errors.append(f"{path.name}: snap grid 非法")
if seen != EXPECTED:
    errors.append(f"资产 ID 集不匹配: missing={sorted(EXPECTED-seen)}, extra={sorted(seen-EXPECTED)}")
standards = json.loads((ROOT / "standards.json").read_text(encoding="utf-8"))
source = ROOT / standards["source_reference"]["path"]
if not source.exists():
    errors.append("参考图缺失")
else:
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if digest != standards["source_reference"]["sha256"]:
        errors.append(f"参考图 SHA-256 不匹配: {digest}")
for required in (ROOT / "README.md", ROOT / "manifest.schema.json", ROOT / "standards.json"):
    if not required.exists():
        errors.append(f"缺少文件: {required.name}")
if errors:
    print("P05 STATIC VALIDATION: FAIL")
    for issue in errors:
        print(f"- {issue}")
    sys.exit(1)
print("P05 STATIC VALIDATION: PASS")
print(f"- manifests: {len(files)}")
print(f"- asset_ids: {len(seen)} unique")
print("- required fields / PBR ranges / snap grid / LOD policy: valid")
print("- source reference SHA-256: valid")
