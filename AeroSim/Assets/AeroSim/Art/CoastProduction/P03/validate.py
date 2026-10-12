#!/usr/bin/env python3
"""P03 manifest-only static validator; no Unity or shared assembly access."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REQUIRED = {"assetId", "displayName", "source", "scale", "tile", "macroVariation", "normal", "roughness", "lod", "streaming", "productionState"}


def load(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def main() -> int:
    pack = load(ROOT / "manifest.json")
    contract = load(ROOT / pack["isolation"]["materialContract"])
    assert contract["runtimeIntegration"] == "NOT_CONNECTED"
    assert pack["isolation"]["sharedEnvironmentAssemblyModified"] is False
    source = ROOT / pack["source"]["file"]
    assert source.is_file(), source
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    assert digest == pack["source"]["sha256"], (digest, pack["source"]["sha256"])
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
        assert data["scale"]["coverageMeters"][0] > 0
        assert data["tile"]["repeatMeters"] > 0
        lo, hi = data["roughness"]["range"]
        assert 0 <= lo <= data["roughness"]["target"] <= hi <= 1
        assert 0 <= data["normal"]["strength"] <= 2
        assert data["productionState"]["runtimeIntegration"] == "NOT_CONNECTED"
        assert len(data["lod"]) == 5
    print(f"PASS P03: {len(ids)} manifests; source sha256={digest}; shared assembly untouched")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
