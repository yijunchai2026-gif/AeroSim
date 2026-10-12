#!/usr/bin/env python3
"""Static validator for the isolated P07 water FX asset pack."""

from __future__ import annotations

import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parent
EXPECTED_HASH = "1afda591f7a75dc2cdcd80934d7626429ca22da5d1310eeb3786b1b20908433f"
EXPECTED_WEATHER = ["Clear", "Overcast", "Rain"]
EXPECTED_QUALITY = ["Low", "High", "Ultra"]
EXPECTED_NAMES = [
    "远海泡沫", "岸浪长条", "碎浪", "回流", "岩石包边",
    "潮池边", "雨滴涟漪", "浪花粒子", "湿边 mask", "foam decay",
]


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def png_size(path: Path) -> tuple[int, int]:
    data = path.read_bytes()[:24]
    assert data[:8] == b"\x89PNG\r\n\x1a\n", f"not PNG: {path}"
    return struct.unpack(">II", data[16:24])


def main() -> None:
    pack = load_json(ROOT / "manifest.json")
    contract = load_json(ROOT / "foam-atlas-material-contract.json")
    source = ROOT / pack["source"]["file"]
    shader = ROOT / pack["isolation"]["shader"]

    assert pack["packId"] == "P07-WATER-FX-PACK-V1"
    assert pack["isolation"]["oceanModified"] is False
    assert pack["isolation"]["sharedEnvironmentAssemblyModified"] is False
    assert pack["isolation"]["runtimeWiring"] == "NONE"
    assert len(pack["subAssets"]) == 10
    assert len(set(pack["subAssets"])) == 10
    assert source.is_file() and png_size(source) == (2880, 2880)
    assert hashlib.sha256(source.read_bytes()).hexdigest() == EXPECTED_HASH
    assert pack["source"]["sha256"] == EXPECTED_HASH
    assert contract["keywords"]["weather"] == ["_WEATHER_CLEAR", "_WEATHER_OVERCAST", "_WEATHER_RAIN"]
    assert contract["keywords"]["quality"] == ["_QUALITY_LOW", "_QUALITY_HIGH", "_QUALITY_ULTRA"]
    assert list(contract["weatherPresets"]) == EXPECTED_WEATHER
    assert list(contract["qualityPresets"]) == EXPECTED_QUALITY
    assert contract["oceanModified"] is False
    assert contract["sharedEnvironmentAssemblyModified"] is False

    shader_text = shader.read_text(encoding="utf-8")
    assert 'Shader "AeroSim/P07/FoamAtlas"' in shader_text
    for prop in contract["requiredProperties"]:
        assert prop in shader_text, f"shader property missing: {prop}"
    for keyword in contract["keywords"]["weather"] + contract["keywords"]["quality"]:
        assert keyword in shader_text, f"shader keyword missing: {keyword}"

    names = []
    for relative in pack["subAssets"]:
        path = ROOT / relative
        assert path.is_file(), f"missing subasset: {relative}"
        item = load_json(path)
        names.append(item["displayName"])
        assert item["schemaVersion"] == "aerosim.water-fx-subasset/v1"
        assert item["source"]["sha256"] == EXPECTED_HASH
        assert item["variants"]["weather"] == EXPECTED_WEATHER
        assert item["variants"]["quality"] == EXPECTED_QUALITY
        crop_x, crop_y, crop_w, crop_h = item["source"]["cropPixelsTopLeft"]
        assert crop_x >= 0 and crop_y >= 0 and crop_w > 0 and crop_h > 0
        assert crop_x + crop_w <= 2880 and crop_y + crop_h <= 2880
        expected_rect = [crop_x / 2880, (2880 - crop_y - crop_h) / 2880, crop_w / 2880, crop_h / 2880]
        actual_rect = item["source"]["atlasRectBottomLeft"]
        assert all(abs(a - b) < 1e-6 for a, b in zip(expected_rect, actual_rect)), f"atlas rect mismatch: {relative}"
        assert item["material"]["contract"] == "../foam-atlas-material-contract.json"
        assert item["productionState"]["runtimeIntegration"] == "NOT_CONNECTED"
    assert names == EXPECTED_NAMES

    print("P07 static validation: PASS")
    print("- 10/10 subasset manifests")
    print("- source PNG: 2880x2880 RGB source, SHA-256 verified")
    print("- Clear/Overcast/Rain and Low/High/Ultra contract coverage")
    print("- FoamAtlas shader properties and local keywords present")
    print("- Ocean/shared assembly isolation declarations intact")


if __name__ == "__main__":
    main()
