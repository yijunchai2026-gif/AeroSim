#!/usr/bin/env python3
"""P08 manifest-only validator; does not import Unity or touch shared scene/state files."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
DIGEST = "7db8075a5b0a8558282b15c3276c9c081a298b083633303fc906511a66e176e1"
EXPECTED = [
    ("P08-WX-01-DRIZZLE", "细雨"),
    ("P08-WX-02-DOWNPOUR", "暴雨"),
    ("P08-WX-03-LENS-DROPS", "镜头雨滴"),
    ("P08-WX-04-SURFACE-STREAKS", "地表流痕"),
    ("P08-WX-05-PUDDLE-RIPPLES", "积水涟漪"),
    ("P08-WX-06-LOW-FOG", "低雾"),
    ("P08-WX-07-SEA-FOG", "海雾"),
    ("P08-WX-08-CLOUD-SHADOWS", "云影"),
    ("P08-WX-09-RAIN-CURTAIN", "雨幕"),
    ("P08-WX-10-WIND-DRIVEN-SPRAY", "风驱喷雾"),
]
REQUIRED = {"assetId", "displayName", "kind", "source", "inputs", "activation", "render", "quality", "offPath", "fallback", "productionState"}
QUALITIES = ["Low", "Medium", "High", "Ultra"]


def load(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def check_anchor(name: str, anchor: dict) -> None:
    for field in ("cloudAmount", "precipitation", "wetness", "puddleAmount", "vfxDensity"):
        assert 0 <= anchor[field] <= 1, f"{name}.{field} outside [0,1]"
    assert 0 <= anchor["fogDensity"] <= .02, f"{name}.fogDensity outside contract"


def main() -> int:
    pack = load(ROOT / "manifest.json")
    contract = load(ROOT / pack["isolation"]["contract"])
    adapter = load(ROOT / pack["isolation"]["adapter"])
    assert contract["contractName"] == "WeatherVisualContract"
    assert contract["runtimeIntegration"] == "CONSUMER_OPT_IN"
    assert contract["stateOwnership"]["writesEnvironmentState"] is False
    assert contract["stateOwnership"]["assemblesScene"] is False
    assert pack["isolation"]["sharedEnvironmentStateModified"] is False
    assert pack["isolation"]["sharedSceneAssemblyModified"] is False
    assert adapter["writes"] == []
    assert adapter["environmentStateModification"] == "FORBIDDEN"
    assert adapter["sceneAssemblyModification"] == "FORBIDDEN"

    source = ROOT / pack["source"]["file"]
    assert source.is_file(), source
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    assert digest == DIGEST == pack["source"]["sha256"]
    assert pack["source"]["dimensions"] == [2880, 2880]

    manifests = pack["subAssets"]
    assert len(manifests) == 10 and len(set(manifests)) == 10
    actual = []
    for rel in manifests:
        data = load(ROOT / rel)
        missing = REQUIRED - data.keys()
        assert not missing, f"{rel}: missing {sorted(missing)}"
        actual.append((data["assetId"], data["displayName"]))
        asset_source = (ROOT / rel).parent / data["source"]["file"]
        assert asset_source.resolve() == source.resolve(), f"{rel}: wrong source path"
        assert data["source"]["sha256"] == digest
        assert data["inputs"], f"{rel}: empty inputs"
        assert data["activation"]["formula"]
        assert data["activation"]["enter"] >= data["activation"]["exit"] >= 0
        assert list(data["quality"].keys()) == QUALITIES, f"{rel}: quality order/coverage"
        assert data["offPath"] and data["fallback"]
        state = data["productionState"]
        assert state == {"texture": "NOT_DERIVED", "runtimeIntegration": "NOT_CONNECTED", "visualCalibration": "NOT_RUN"}
    assert actual == EXPECTED, (actual, EXPECTED)

    continuum = contract["weatherContinuum"]
    for name in ("Clear", "Overcast", "Rain"):
        check_anchor(name, continuum[name])
    assert continuum["Clear"]["cloudAmount"] < continuum["Overcast"]["cloudAmount"] <= continuum["Rain"]["cloudAmount"]
    assert continuum["Clear"]["precipitation"] == continuum["Overcast"]["precipitation"] == 0
    assert continuum["Rain"]["precipitation"] == 1
    assert list(contract["qualityTiers"].keys()) == QUALITIES
    assert [contract["qualityTiers"][q]["rank"] for q in QUALITIES] == [0, 1, 2, 3]
    for key in ("globalOff", "vfxPolicyOff", "clearWeather", "assetOff", "qualityFallback", "invalidState", "dispose"):
        assert contract["shutdownPaths"][key]

    implementation = (ROOT / pack["isolation"]["referenceImplementation"]).read_text(encoding="utf-8")
    for token in ("Evaluate(EnvironmentState snapshot", "EvaluateOff", "ApplyQuality", "ApplyAssetSwitches"):
        assert token in implementation, f"reference adapter missing {token}"
    forbidden = ("snapshot.cloudAmount =", "snapshot.precipitation =", "snapshot.wetness =", "new GameObject", "AddComponent<")
    assert not any(token in implementation for token in forbidden), "reference adapter mutates state or assembles scene"

    print(f"PASS P08: {len(actual)} manifests; WeatherVisualContract + read-only adapter; source sha256={digest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
