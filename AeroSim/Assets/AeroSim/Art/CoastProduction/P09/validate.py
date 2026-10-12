#!/usr/bin/env python3
"""P09 manifest-only validator. It never opens or modifies Unity scenes."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent
HEX = re.compile(r"^#[0-9A-F]{6}$")
REQUIRED = {"schemaVersion", "profileId", "displayName", "category", "enabled", "targets", "parameters", "guardrails", "productionState"}
CATEGORIES = ["exposure.clear", "exposure.overcast", "exposure.rain", "gamut.ocean", "gamut.rock", "gamut.vegetation", "gamut.industrial", "atmosphere.fog", "atmosphere.cloudShadow", "reserve.night"]


def load(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def walk(value):
    if isinstance(value, dict):
        for item in value.values():
            yield from walk(item)
    elif isinstance(value, list):
        for item in value:
            yield from walk(item)
    else:
        yield value


def main() -> int:
    pack = load(ROOT / "manifest.json")
    contract = load(ROOT / pack["isolation"]["parameterContract"])
    assert pack["isolation"]["sharedSceneModified"] is False
    assert pack["isolation"]["runtimeWiring"] == "NONE"
    assert contract["renderPipeline"] == "Built-in"
    assert contract["pipelineMigrationRequired"] is False
    assert contract["runtimeIntegration"] == "NOT_CONNECTED"
    assert contract["sharedSceneModified"] is False
    source = ROOT / pack["source"]["file"]
    assert source.is_file(), source
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    assert digest == pack["source"]["sha256"], (digest, pack["source"]["sha256"])
    profiles = pack["profiles"]
    assert len(profiles) == 10 and len(set(profiles)) == 10
    ids, categories = set(), []
    for rel in profiles:
        data = load(ROOT / rel)
        missing = REQUIRED - data.keys()
        assert not missing, f"{rel}: missing {sorted(missing)}"
        assert data["profileId"] not in ids, f"duplicate {data['profileId']}"
        ids.add(data["profileId"])
        categories.append(data["category"])
        assert data["schemaVersion"] == "aerosim.lookdev-profile/v1"
        assert data["productionState"]["runtimeIntegration"] == "NOT_CONNECTED"
        assert data["productionState"]["visualCalibration"] == "NOT_RUN"
        for value in walk(data):
            if isinstance(value, str) and value.startswith("#"):
                assert HEX.fullmatch(value), f"{rel}: invalid color {value}"
        grading = data["parameters"].get("colorGrading")
        if grading:
            assert -4 <= grading["exposureEv"] <= 4
            assert -0.25 <= grading["contrast"] <= 0.25
            assert 0 <= grading["saturation"] <= 1.25
        assert data["guardrails"], f"{rel}: guardrails required"
    assert categories == CATEGORIES, categories
    night = load(ROOT / profiles[-1])
    assert night["enabled"] is False and night["category"] == "reserve.night"
    assert pack["qualityDeclaration"]["profileCount"] == len(ids)
    print(f"PASS P09: {len(ids)} profiles; Built-in/no-migration; source sha256={digest}; shared scene untouched")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
