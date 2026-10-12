#!/usr/bin/env python3
"""Static validator for Coast Production P01. No Unity process required."""
from __future__ import annotations
import hashlib, json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parents[4]
LABEL = "PROCEDURAL / GENERATED SOURCE"
EXPECTED_SHA = "5a3f5a1eedd1bd65df28a256d1824eca02c6c2f5ca0f18777db9d788a0aad309"
EXPECTED = {
 "P01-CLIFF-01":"macro-cliff", "P01-STRATA-01":"strata-wall", "P01-OVERHANG-01":"overhang",
 "P01-TALUS-01":"talus-blocks", "P01-STACK-A":"sea-stack-a", "P01-STACK-B":"sea-stack-b",
 "P01-FRACTURE-01":"fractured-edge", "P01-FOOT-01":"cliff-foot",
 "P01-DISTANT-01":"distant-silhouette", "P01-COL-01":"collision-proxy"
}
REQUIRED_SHADER = {"_BaseMap","_Tint","_MetersPerTile","_StrataScale","_StrataStrength","_MacroStrength","_Roughness","_Wetness","_EdgeLight"}

def fail(errors, message): errors.append(message)
def project_path(text): return PROJECT / text

def main():
    errors=[]
    package_path=ROOT/"package-manifest.json"
    try: package=json.loads(package_path.read_text(encoding="utf-8"))
    except Exception as exc: package={}; fail(errors,f"package-manifest unreadable: {exc}")
    if package.get("provenance",{}).get("classification") != LABEL: fail(errors,"package missing PROCEDURAL / GENERATED SOURCE classification")
    if package.get("provenance",{}).get("scanClaim") != "NOT A SCAN": fail(errors,"package must state NOT A SCAN")
    if package.get("integration",{}).get("modifiesSharedSceneAssembly") is not False: fail(errors,"shared scene assembly opt-out missing")
    source=ROOT/"Source/generated_image_035637.png"
    if not source.is_file(): fail(errors,"generated texture source missing")
    elif hashlib.sha256(source.read_bytes()).hexdigest()!=EXPECTED_SHA: fail(errors,"generated texture SHA-256 mismatch")
    records=package.get("assets",[])
    if len(records)!=10: fail(errors,f"expected 10 package records, got {len(records)}")
    ids=[]; slugs=[]
    for record in records:
        manifest_path=project_path(record.get("manifestPath",""))
        try: data=json.loads(manifest_path.read_text(encoding="utf-8"))
        except Exception as exc: fail(errors,f"manifest unreadable {manifest_path}: {exc}"); continue
        asset_id=data.get("assetId"); slug=data.get("slug"); ids.append(asset_id); slugs.append(slug)
        if EXPECTED.get(asset_id)!=slug: fail(errors,f"unexpected asset id/slug: {asset_id}/{slug}")
        provenance=data.get("provenance",{})
        if provenance.get("classification")!=LABEL or provenance.get("scanClaim")!="NOT A SCAN": fail(errors,f"{asset_id}: provenance labels invalid")
        integration=data.get("integration",{})
        if integration.get("sceneAssemblyOwnedByIntegrator") is not True: fail(errors,f"{asset_id}: scene assembly ownership invalid")
        for key in ("prefabPath","meshPath","materialPath"):
            path=integration.get(key)
            if path:
                target=project_path(path)
                if not target.is_file(): fail(errors,f"{asset_id}: missing {key} {path}")
                if not Path(str(target)+".meta").is_file(): fail(errors,f"{asset_id}: missing Unity meta for {path}")
                if key=="prefabPath" and target.is_file():
                    prefab_text=target.read_text(encoding="utf-8",errors="replace")
                    if asset_id not in prefab_text or "PROCEDURAL / GENERATED SOURCE" not in prefab_text: fail(errors,f"{asset_id}: prefab provenance marker missing")
                    if asset_id=="P01-COL-01" and prefab_text.count("BoxCollider:")!=3: fail(errors,"collision proxy must contain exactly three BoxColliders")
                    if asset_id!="P01-COL-01" and "MeshRenderer:" not in prefab_text: fail(errors,f"{asset_id}: visual prefab renderer missing")
        if asset_id=="P01-COL-01":
            if integration.get("meshPath") is not None or integration.get("materialPath") is not None: fail(errors,"collision proxy must be non-rendering")
            if data.get("collision",{}).get("mode")!="three-box-proxy": fail(errors,"collision proxy mode invalid")
        elif data.get("lod",{}).get("lod0TriangleBudget",0)<=0: fail(errors,f"{asset_id}: triangle budget missing")
    if set(ids)!=set(EXPECTED) or len(ids)!=len(set(ids)): fail(errors,"asset ids are incomplete or duplicated")
    if set(slugs)!=set(EXPECTED.values()) or len(slugs)!=len(set(slugs)): fail(errors,"asset slugs are incomplete or duplicated")
    shader_path=ROOT/"Shaders/CoastRockP01.shader"
    shader=shader_path.read_text(encoding="utf-8") if shader_path.is_file() else ""
    missing=[p for p in sorted(REQUIRED_SHADER) if not re.search(r"\b"+re.escape(p)+r"\b",shader)]
    if missing: fail(errors,"shader properties missing: "+", ".join(missing))
    material=ROOT/"Materials/CoastRockP01.mat"
    if material.is_file():
        text=material.read_text(encoding="utf-8",errors="replace")
        if "CoastRockP01_PROCEDURAL" not in text: fail(errors,"material generated name/provenance marker missing")
    else: fail(errors,"generated material missing")
    scenes=list(ROOT.rglob("*.unity"))
    if scenes: fail(errors,"package must not contain shared scene files: "+", ".join(map(str,scenes)))
    for path in ROOT.rglob("*"):
        if path.name.endswith(".meta") or path.name in {".DS_Store"}: continue
        if path.is_file() and not Path(str(path)+".meta").is_file(): fail(errors,f"Unity meta missing: {path.relative_to(ROOT)}")
        if path.is_dir() and path!=ROOT and not Path(str(path)+".meta").is_file(): fail(errors,f"Unity folder meta missing: {path.relative_to(ROOT)}")
    if errors:
        print(f"Coast P01 STATIC VALIDATION FAIL ({len(errors)})")
        for item in errors: print(" -",item)
        return 1
    print("Coast P01 STATIC VALIDATION PASS")
    print(" - 10/10 explicit subasset manifests")
    print(" - source SHA-256 verified")
    print(" - shader/material/prefab/mesh paths and Unity meta verified")
    print(" - PROCEDURAL / GENERATED SOURCE; NOT A SCAN")
    print(" - no shared scene assembly file present")
    return 0

if __name__=="__main__": sys.exit(main())
