#!/usr/bin/env python3
"""Create the Coast P01 integration manifests from one deterministic specification.
PROCEDURAL / GENERATED SOURCE — NOT A SCAN.
"""
from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LABEL = "PROCEDURAL / GENERATED SOURCE"
SOURCE_SHA256 = "5a3f5a1eedd1bd65df28a256d1824eca02c6c2f5ca0f18777db9d788a0aad309"
ASSETS = [
    ("P01-CLIFF-01", "macro-cliff", "宏观悬崖", [96,54,30], [0,80,250], 42000, "hero cliff mass / primary coastline rhythm"),
    ("P01-STRATA-01", "strata-wall", "层理壁", [58,34,12], [0,60,180], 32000, "readable horizontal bedding wall"),
    ("P01-OVERHANG-01", "overhang", "悬挑", [32,27,20], [0,45,140], 26000, "negative-space overhang accent"),
    ("P01-TALUS-01", "talus-blocks", "崩积块", [38,10,24], [0,35,110], 18000, "talus cluster at erosion transitions"),
    ("P01-STACK-A", "sea-stack-a", "岩柱A", [18,46,17], [0,70,220], 28000, "slender near/mid sea stack"),
    ("P01-STACK-B", "sea-stack-b", "岩柱B", [24,61,20], [0,90,280], 34000, "tall asymmetric sea stack"),
    ("P01-FRACTURE-01", "fractured-edge", "碎裂边", [44,29,18], [0,50,160], 26000, "broken transition edge / silhouette breakup"),
    ("P01-FOOT-01", "cliff-foot", "坡脚", [72,12,34], [0,50,160], 22000, "cliff-to-water grounding mass"),
    ("P01-DISTANT-01", "distant-silhouette", "远景轮廓", [210,86,24], [250,900,2000], 12000, "distance-only coastline skyline"),
    ("P01-COL-01", "collision-proxy", "碰撞代理", [94,52,28], [0,250,600], 0, "three-box non-rendering collision proxy"),
]

def make_asset(asset_id, slug, display, bounds, distances, tris, purpose):
    collision = asset_id == "P01-COL-01"
    base = "Assets/AeroSim/Art/CoastProduction/P01"
    return {
        "schema": "aerosim.coast-production.subasset/v1",
        "packageId": "COAST-PRODUCTION-P01",
        "assetId": asset_id,
        "slug": slug,
        "displayName": display,
        "provenance": {"classification": LABEL, "scanClaim": "NOT A SCAN", "generator": "CoastP01Generator.cs", "sourceTextureSha256": SOURCE_SHA256},
        "integration": {
            "prefabPath": f"{base}/Prefabs/{slug}.prefab",
            "meshPath": None if collision else f"{base}/Meshes/{slug}.asset",
            "materialPath": None if collision else f"{base}/Materials/CoastRockP01.mat",
            "coordinateSystem": "Unity:Y-up,+Z-forward,+X-right,meters",
            "pivot": "bottom-center",
            "sceneAssemblyOwnedByIntegrator": True
        },
        "boundsMeters": {"x": bounds[0], "y": bounds[1], "z": bounds[2]},
        "lod": {"distanceMeters": distances, "lod0TriangleBudget": tris, "generatedMeshIs": "LOD0" if not collision else "NONE"},
        "collision": {"mode": "three-box-proxy" if collision else "external-proxy", "isTrigger": False, "renderersAllowed": 0 if collision else 1},
        "purpose": purpose,
        "status": "GENERATED_READY"
    }

def main():
    manifests = ROOT / "Manifests"; manifests.mkdir(parents=True, exist_ok=True)
    records = []
    for spec in ASSETS:
        data = make_asset(*spec)
        path = manifests / f"{spec[1]}.manifest.json"
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        records.append({"assetId": spec[0], "manifestPath": f"Assets/AeroSim/Art/CoastProduction/P01/Manifests/{path.name}", "prefabPath": data["integration"]["prefabPath"]})
    package = {
        "schema": "aerosim.coast-production.package/v1",
        "packageId": "COAST-PRODUCTION-P01",
        "version": "1.0.0",
        "owner": "Coast Art Lead-01",
        "provenance": {"classification": LABEL, "scanClaim": "NOT A SCAN"},
        "source": {"path": "Assets/AeroSim/Art/CoastProduction/P01/Source/generated_image_035637.png", "sha256": SOURCE_SHA256, "dimensions": [2880,2880]},
        "material": {"path": "Assets/AeroSim/Art/CoastProduction/P01/Materials/CoastRockP01.mat", "shader": "AeroSim/Coast/CoastRockP01", "parameters": {"_Tint": [0.72,0.75,0.76,1], "_MetersPerTile": 4.0, "_StrataScale": 0.34, "_StrataStrength": 0.42, "_MacroStrength": 0.18, "_Roughness": 0.84, "_Wetness": 0.08, "_EdgeLight": 0.12}},
        "assets": records,
        "integration": {"modifiesSharedSceneAssembly": False, "entryPoint": "package-manifest.json", "generator": "Editor/CoastP01Generator.cs", "validator": "Tools/validate_p01.py"}
    }
    (ROOT / "package-manifest.json").write_text(json.dumps(package, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(records)} subasset manifests and package-manifest.json")

if __name__ == "__main__": main()
