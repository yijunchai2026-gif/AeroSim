#!/usr/bin/env python3
"""Fail-closed static audit for the Mini 4 Pro RD-06 production intake."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

CONTENT_NOT_DELIVERED = "CONTENT_NOT_DELIVERED"
FORMAL_MODEL_EXTENSIONS = {".fbx", ".glb"}
FORMAL_TEXTURE_EXTENSIONS = {".png", ".tga", ".tif", ".tiff", ".exr", ".psd", ".jpg", ".jpeg"}

ROOT = Path(__file__).resolve().parents[2]
INTAKE = ROOT / "AeroSim/Assets/AeroSim/Art/AircraftProduction/mini4"
PLACEHOLDER = INTAKE / "intake-manifest.placeholder.json"
RD02_MANIFEST = ROOT / "AeroSim/Assets/AeroSim/Resources/AircraftAssets/asset-manifest-v1.json"
LOADER = ROOT / "AeroSim/Assets/AeroSim/Runtime/Simulation/ProductionAssetLoader.cs"
PRODUCTION_PREFAB = ROOT / "AeroSim/Assets/AeroSim/Resources/AircraftProduction/mini4/Aircraft.prefab"


def load_json(path: Path, errors: list[str]) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"无法读取有效 JSON：{path.relative_to(ROOT)} ({exc})")
        return {}


def require_blank(value: object, field: str, errors: list[str]) -> None:
    if value not in (None, "", [], {}):
        errors.append(f"未交付阶段 {field} 必须为空，实际为 {value!r}")


def main() -> int:
    errors: list[str] = []
    placeholder = load_json(PLACEHOLDER, errors)
    rd02 = load_json(RD02_MANIFEST, errors)

    if placeholder.get("schema") != "aerosim.production-intake-placeholder/v1":
        errors.append("placeholder schema 不正确")
    if placeholder.get("runtimeId") != "mini4":
        errors.append("placeholder runtimeId 必须为 mini4")
    if placeholder.get("status") != CONTENT_NOT_DELIVERED:
        errors.append("当前 placeholder 必须保持 CONTENT_NOT_DELIVERED")

    source = placeholder.get("sourceModel") or {}
    for field in ("path", "format", "sha256"):
        require_blank(source.get(field), f"sourceModel.{field}", errors)
    for field in ("textureSets", "productionPrefabPath", "productionResourcePath", "productionAddress", "approvalEvidence"):
        require_blank(placeholder.get(field), field, errors)

    candidates = [entry for entry in rd02.get("assets", []) if entry.get("runtimeId") == "mini4"]
    if len(candidates) != 1:
        errors.append(f"RD-02 主 manifest 必须且只能有一个 mini4 记录，实际为 {len(candidates)}")
    else:
        mini4 = candidates[0]
        expected = {
            "identityState": "Unverified",
            "qualityState": "ProgrammaticPrototype",
            "sourcePath": "",
            "sourceFormat": "programmatic",
            "sourceSha256": "",
            "productionContentStatus": CONTENT_NOT_DELIVERED,
        }
        for field, value in expected.items():
            if mini4.get(field) != value:
                errors.append(f"RD-02 mini4.{field} 必须为 {value!r}，实际为 {mini4.get(field)!r}")
        for field in ("productionPrefabPath", "productionResourcePath", "productionAddress"):
            require_blank(mini4.get(field), f"RD-02 mini4.{field}", errors)

    if INTAKE.is_dir():
        forbidden = sorted(
            path.relative_to(ROOT).as_posix()
            for path in INTAKE.rglob("*")
            if path.is_file() and path.suffix.lower() in FORMAL_MODEL_EXTENSIONS | FORMAL_TEXTURE_EXTENSIONS | {".prefab"}
        )
        if forbidden:
            errors.append("CONTENT_NOT_DELIVERED 阶段发现正式内容文件：" + ", ".join(forbidden))
    else:
        errors.append("Mini 4 Pro intake 目录不存在")
    if PRODUCTION_PREFAB.exists():
        errors.append("CONTENT_NOT_DELIVERED 阶段不得存在正式 Aircraft.prefab")

    try:
        loader_text = LOADER.read_text(encoding="utf-8")
    except OSError as exc:
        errors.append(f"无法读取 loader：{exc}")
    else:
        required_patterns = (
            r"public const string ContentNotDelivered\s*=\s*\"CONTENT_NOT_DELIVERED\"",
            r"productionContentStatus",
            r"ContentStatus",
            r"ProductionAssetLoadState\.ContentNotDelivered",
        )
        for pattern in required_patterns:
            if not re.search(pattern, loader_text):
                errors.append(f"loader 缺少 fail-closed 门：{pattern}")
        lower_loader = loader_text.lower()
        if "procedural geometry" not in lower_loader or ("fallback" not in lower_loader and "falls back" not in lower_loader):
            errors.append("loader 必须明确禁止 procedural/fallback production geometry")

    if errors:
        print("RD-06 Mini 4 Pro intake: FAIL")
        for error in errors:
            print(f"- {error}")
        return 1

    print("RD-06 Mini 4 Pro intake: PASS")
    print("- CONTENT_NOT_DELIVERED")
    print("- 未发现正式 FBX/GLB、贴图或 Prefab")
    print("- RD-02 UI 身份保持 ProgrammaticPrototype / Unverified")
    print("- loader fail-closed 合同存在")
    return 0


if __name__ == "__main__":
    sys.exit(main())
