#!/usr/bin/env python3
"""P04 asset-pack read-only static validator; writes only its Markdown report."""
from __future__ import annotations

import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "validation" / "static_validation_report.md"
REQUIRED_ASSET_KEYS = {
    "schema_version", "asset_id", "display_name", "category", "source_dependencies",
    "material_contract", "decal_contract", "dimensions_m", "pivot", "local_position_m",
    "collision", "integration", "acceptance"
}
FORBIDDEN_SCENE_SUFFIXES = {".unity", ".umap", ".tscn", ".scene", ".prefab"}


def load_json(relative: str):
    with (ROOT / relative).open("r", encoding="utf-8") as handle:
        return json.load(handle)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as handle:
        signature = handle.read(24)
    if len(signature) != 24 or signature[:8] != b"\x89PNG\r\n\x1a\n" or signature[12:16] != b"IHDR":
        raise ValueError("not a valid PNG IHDR")
    return struct.unpack(">II", signature[16:24])


def resolve_fragment_reference(owner: Path, reference: str | None) -> bool:
    if reference is None:
        return True
    file_part = reference.split("#", 1)[0]
    return (owner.parent / file_part).resolve().is_file()


def main() -> int:
    checks: list[tuple[str, bool, str]] = []

    def check(name: str, condition: bool, detail: str = "") -> None:
        checks.append((name, bool(condition), detail))

    try:
        package = load_json("package.manifest.json")
        check("package.manifest.json 可解析", True)
    except Exception as exc:
        package = {}
        check("package.manifest.json 可解析", False, str(exc))

    check("包 ID 正确", package.get("package_id") == "P04_TAKEOFF_FACILITY")
    scene_policy = package.get("scene_policy", {})
    check("声明为场景独立", scene_policy.get("scene_independent") is True)
    check("禁止共享场景改动", scene_policy.get("shared_scene_changes") is False)
    check("无场景引用", scene_policy.get("scene_references") == [])

    assets = package.get("assets", [])
    check("子资产数量为 10", len(assets) == 10, f"actual={len(assets)}")
    package_ids = [item.get("id") for item in assets]
    check("包索引资产 ID 唯一", len(package_ids) == len(set(package_ids)))

    manifest_ids: list[str] = []
    no_collision_ids: set[str] = set()
    manifest_paths: list[Path] = []
    for entry in assets:
        relative = entry.get("manifest", "")
        path = ROOT / relative
        manifest_paths.append(path)
        check(f"manifest 存在: {relative}", path.is_file())
        if not path.is_file():
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
            check(f"manifest 可解析: {relative}", True)
        except Exception as exc:
            check(f"manifest 可解析: {relative}", False, str(exc))
            continue
        missing = sorted(REQUIRED_ASSET_KEYS - set(data))
        check(f"manifest 字段完整: {data.get('asset_id', relative)}", not missing, ", ".join(missing))
        asset_id = data.get("asset_id", "")
        manifest_ids.append(asset_id)
        check(f"索引 ID 匹配: {asset_id}", entry.get("id") == asset_id)
        dims = data.get("dimensions_m", [])
        check(f"尺寸合法: {asset_id}", isinstance(dims, list) and len(dims) == 3 and all(isinstance(v, (int, float)) and v >= 0 for v in dims))
        integration = data.get("integration", {})
        check(f"场景隔离: {asset_id}", integration.get("scene_independent") is True and integration.get("shared_scene_write") is False)
        collision = data.get("collision", {})
        if collision.get("enabled") is False:
            no_collision_ids.add(asset_id)
        check(f"材质合同可解析: {asset_id}", resolve_fragment_reference(path, data.get("material_contract")))
        check(f"Decal 合同可解析: {asset_id}", resolve_fragment_reference(path, data.get("decal_contract")))
        for source_ref in data.get("source_dependencies", []):
            check(f"源依赖存在: {asset_id} -> {source_ref}", (path.parent / source_ref).resolve().is_file())

    check("manifest 资产 ID 唯一", len(manifest_ids) == len(set(manifest_ids)))

    for contract in package.get("contracts", []):
        contract_path = ROOT / contract
        check(f"合同存在: {contract}", contract_path.is_file())
        if contract_path.is_file():
            try:
                json.loads(contract_path.read_text(encoding="utf-8"))
                check(f"合同可解析: {contract}", True)
            except Exception as exc:
                check(f"合同可解析: {contract}", False, str(exc))

    try:
        collision_contract = load_json("contracts/collision_dimensions.contract.json")
        contract_no_collision = set(collision_contract.get("no_collision_assets", []))
        check("无碰撞资产集合与 manifests 一致", contract_no_collision == no_collision_ids,
              f"contract={sorted(contract_no_collision)} manifest={sorted(no_collision_ids)}")
        collider_ids = {item.get("asset_id") for item in collision_contract.get("colliders", [])}
        enabled_ids = set(manifest_ids) - no_collision_ids
        check("有碰撞资产均在碰撞合同登记", enabled_ids == collider_ids,
              f"enabled={sorted(enabled_ids)} contract={sorted(collider_ids)}")
    except Exception as exc:
        check("碰撞合同交叉校验", False, str(exc))

    for source in package.get("sources", []):
        path = ROOT / source.get("path", "")
        check(f"源文件存在: {source.get('id')}", path.is_file())
        if not path.is_file():
            continue
        try:
            actual_dims = png_dimensions(path)
            expected_dims = (source.get("width"), source.get("height"))
            check(f"源尺寸锁定: {source.get('id')}", actual_dims == expected_dims, f"actual={actual_dims} expected={expected_dims}")
            actual_hash = sha256(path)
            check(f"源 SHA-256 锁定: {source.get('id')}", actual_hash == source.get("sha256"), actual_hash)
        except Exception as exc:
            check(f"源文件有效: {source.get('id')}", False, str(exc))

    forbidden = [path.relative_to(ROOT).as_posix() for path in ROOT.rglob("*") if path.is_file() and path.suffix.lower() in FORBIDDEN_SCENE_SUFFIXES]
    check("资产包不含场景或 Prefab 文件", not forbidden, ", ".join(forbidden))

    passed = sum(1 for _, ok, _ in checks if ok)
    failed = len(checks) - passed
    status = "PASS" if failed == 0 else "FAIL"
    lines = [
        "# P04 静态校验报告",
        "",
        f"- 结果：**{status}**",
        f"- 通过：{passed}",
        f"- 失败：{failed}",
        f"- 校验范围：包索引、10 个 manifest、3 个合同、源图尺寸/哈希、碰撞交叉一致性、场景隔离。",
        "",
        "## 明细",
        "",
    ]
    for name, ok, detail in checks:
        suffix = f" — `{detail}`" if detail else ""
        lines.append(f"- {'PASS' if ok else 'FAIL'} · {name}{suffix}")
    lines.extend(["", "## 结论", "", "可交付。" if failed == 0 else "存在失败项，暂不可交付。", ""])
    REPORT.write_text("\n".join(lines), encoding="utf-8")
    print(f"P04 static validation: {status} ({passed} passed, {failed} failed)")
    print(REPORT)
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
