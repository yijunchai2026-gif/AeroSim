#!/usr/bin/env python3
"""P02 manifest-only static validator; no Unity, shared assembly, or Ocean Shader access."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REQUIRED = {
    "assetId", "displayName", "sources", "shader", "material",
    "weatherWetness", "lod", "productionState",
}
EXPECTED_TAGS = {
    "GENERATED_SOURCE:generated_image_035657": "ceb7ff9a0b55ef5801e2bc9d4bf78308c51144c752f04cab2d51ff94e0f77b40",
    "GENERATED_SOURCE:generated_image_035650": "d84db9b1323b596e5166ea08ae1d129cb989401c9598b9877918825c7c2b2f80",
}


def load(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def in_unit_interval(value: float) -> bool:
    return 0.0 <= value <= 1.0


def main() -> int:
    pack = load(ROOT / "manifest.json")
    contract = load(ROOT / pack["isolation"]["shaderMaterialContract"])
    weather = load(ROOT / pack["isolation"]["weatherWetnessParameters"])

    assert pack["isolation"]["sharedSceneAssemblyModified"] is False
    assert pack["isolation"]["oceanMainShaderModified"] is False
    assert pack["isolation"]["runtimeWiring"] == "NONE"
    assert contract["runtimeIntegration"] == "NOT_CONNECTED"
    assert contract["shader"]["oceanDependency"] == "FORBIDDEN"
    assert contract["shader"]["oceanMainShaderModified"] is False
    assert contract["shader"]["name"] != "AeroSim/OceanSurface"
    assert weather["connection"] == "NOT_CONNECTED"
    assert weather["safety"]["mutatesSharedWeatherState"] is False
    assert weather["safety"]["oceanShaderDependency"] is False

    source_hashes = {}
    for source in pack["sources"]:
        path = ROOT / source["file"]
        assert path.is_file(), path
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        assert digest == source["sha256"]
        assert EXPECTED_TAGS[source["generatedSourceTag"]] == digest
        assert source["dimensions"] == [2880, 2880]
        assert source["colorMode"] == "RGB8"
        source_hashes[source["generatedSourceTag"]] = digest
    assert source_hashes == EXPECTED_TAGS

    manifests = pack["subAssets"]
    assert len(manifests) == 10 and len(set(manifests)) == 10
    ids, profiles = set(), set()
    allowed_profiles = set(contract["variantPolicy"]["profiles"])
    allowed_keywords = set(contract["variantPolicy"]["keywords"])
    for rel in manifests:
        data = load(ROOT / rel)
        missing = REQUIRED - data.keys()
        assert not missing, f"{rel}: missing {sorted(missing)}"
        assert data["assetId"] not in ids
        ids.add(data["assetId"])
        profile = data["shader"]["profile"]
        assert profile in allowed_profiles and profile not in profiles
        profiles.add(profile)
        assert data["shader"]["contract"] == "../shader-material-contract.json"
        assert set(data["shader"]["keywords"]) <= allowed_keywords
        assert len(data["shader"]["keywords"]) <= contract["variantPolicy"]["maxKeywordsPerMaterial"]
        for source in data["sources"]:
            tag = source["generatedSourceTag"]
            assert EXPECTED_TAGS[tag] == source["sha256"]
            assert (ROOT / "manifests" / source["file"]).is_file()
        material = data["material"]
        assert material["tileMeters"][0] > 0 and material["tileMeters"][1] > 0
        assert 0 <= material["wetRoughness"] <= material["dryRoughness"] <= 1
        assert 0 <= material["normalStrength"] <= 2
        assert 0 <= material["standingDepthMm"][0] <= material["standingDepthMm"][1] <= 120
        wetness = data["weatherWetness"]
        assert wetness["parameterSet"] == "../weather-wetness-parameters.json"
        for key in ("weatherInfluence", "tideInfluence", "wetValueMultiplier", "wetNormalMultiplier"):
            assert in_unit_interval(wetness[key]), (rel, key)
        assert len(data["lod"]) == 5
        state = data["productionState"]
        assert state["shader"] == "NOT_AUTHORED"
        assert state["runtimeIntegration"] == "NOT_CONNECTED"

    assert profiles == allowed_profiles
    assert pack["qualityDeclaration"]["manifestCount"] == len(ids)
    print(
        "PASS P02: 10 manifests; 2 generated-source tags preserved; "
        "independent shader/material and weather contracts valid; "
        "shared scene assembly and Ocean main shader untouched"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
