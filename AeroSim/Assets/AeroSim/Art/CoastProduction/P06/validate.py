#!/usr/bin/env python3
"""P06 manifest-only static validator; no Unity or shared-scene access."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REQUIRED = {"assetId", "displayName", "archetype", "source", "geometry", "alphaCutout", "wind", "seasonWeather", "lodBillboard", "ecology", "productionState"}
ECO_RANGES = {"distanceToMeanHighWaterM", "elevationAboveMeanHighWaterM", "slopeDegrees", "soilMoisture01", "salinity01", "exposure01", "spacingMeters", "patchRadiusMeters", "densityPerSquareMeter"}


def load(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def ordered_pair(value, label, minimum=None, maximum=None):
    assert isinstance(value, list) and len(value) == 2, label
    lo, hi = value
    assert lo <= hi, label
    if minimum is not None:
        assert lo >= minimum, label
    if maximum is not None:
        assert hi <= maximum, label


def main() -> int:
    pack = load(ROOT / "manifest.json")
    contract = load(ROOT / pack["isolation"]["materialContract"])
    assert contract["runtimeIntegration"] == "NOT_CONNECTED"
    assert contract["sharedSceneModified"] is False
    assert pack["isolation"]["sharedSceneModified"] is False
    assert pack["isolation"]["sharedEnvironmentAssemblyModified"] is False
    assert pack["isolation"]["runtimeWiring"] == "NONE"
    assert contract["alphaCutout"]["surfaceType"].startswith("Opaque/Cutout")
    assert contract["alphaCutout"]["mipCoveragePreservation"] is True
    assert set(contract["windContract"]["globalInputs"]) == {"windDirectionXZ", "meanSpeedMps", "gust01", "timeSeconds"}
    assert "wetness01" in contract["seasonWeather"]
    assert contract["lodBillboard"]["billboard"]["views"] >= 8
    assert contract["ecologyContract"]["hardReject"]

    source = ROOT / pack["source"]["file"]
    assert source.is_file(), source
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    assert digest == pack["source"]["sha256"], (digest, pack["source"]["sha256"])
    assert pack["source"]["dimensions"] == [2880, 2880]
    assert pack["source"]["colorMode"] == "RGB8" and pack["source"]["alphaPresent"] is False

    manifests = pack["subAssets"]
    assert len(manifests) == 10 and len(set(manifests)) == 10
    ids = set()
    for rel in manifests:
        data = load(ROOT / rel)
        missing = REQUIRED - data.keys()
        assert not missing, f"{rel}: missing {sorted(missing)}"
        assert data["assetId"] not in ids, f"duplicate {data['assetId']}"
        ids.add(data["assetId"])
        assert data["source"]["sha256"] == digest
        region = data["source"]["regionNormalizedTopLeft"]
        assert len(region) == 4 and all(0 <= n <= 1 for n in region)
        assert region[0] + region[2] <= 1.0001 and region[1] + region[3] <= 1.0001
        ordered_pair(data["geometry"]["heightMeters"], f"{rel}:height", 0)
        assert data["geometry"]["pivot"]
        for key in ("primaryBend", "edgeFlutter", "stiffness", "phaseJitter"):
            assert 0 <= data["wind"][key] <= 1, f"{rel}:wind.{key}"
        assert data["wind"]["maxDisplacementMeters"] >= 0
        assert 0 <= data["seasonWeather"]["autumnDryness"] <= 1
        assert 0 <= data["seasonWeather"]["wetnessResponse"] <= 1
        ordered_pair(data["seasonWeather"]["flowerAmount"], f"{rel}:flower", 0, 1)
        lod = data["lodBillboard"]
        for key in ("lod0Meters", "lod1Meters", "lod2Meters", "billboardMeters"):
            ordered_pair(lod[key], f"{rel}:{key}", 0)
        assert lod["lod0Meters"][0] <= lod["lod1Meters"][0] <= lod["lod2Meters"][0] <= lod["billboardMeters"][0] < lod["cullMeters"]
        assert lod["billboardMeters"][1] <= lod["cullMeters"]
        ecology = data["ecology"]
        missing_eco = ECO_RANGES - ecology.keys()
        assert not missing_eco, f"{rel}: ecology missing {sorted(missing_eco)}"
        for key in ECO_RANGES:
            ordered_pair(ecology[key], f"{rel}:ecology.{key}", 0)
        for key in ("soilMoisture01", "salinity01", "exposure01"):
            ordered_pair(ecology[key], f"{rel}:ecology.{key}", 0, 1)
        assert ecology["substrate"]
        state = data["productionState"]
        assert state["runtimeIntegration"] == "NOT_CONNECTED"
        assert state["alpha"] == "NOT_DERIVED" and state["billboard"] == "NOT_BAKED"
    assert pack["qualityDeclaration"]["manifestCount"] == len(ids)
    assert pack["qualityDeclaration"]["staticValidation"] == "PASS"
    print(f"PASS P06: {len(ids)} manifests; alpha/wind/weather/LOD/ecology contracts valid; source sha256={digest}; shared scene untouched")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
