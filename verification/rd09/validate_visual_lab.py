#!/usr/bin/env python3
"""Static RD-09 contract check. It never reports runtime performance PASS."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
FILES = {
    "evidence": ROOT / "AeroSim/Assets/AeroSim/Runtime/Diagnostics/VisualLabEvidence.cs",
    "ui": ROOT / "AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.VisualLab.cs",
    "runner": ROOT / "AeroSim/Assets/AeroSim/Runtime/Diagnostics/PreviewCapture.cs",
    "pages": ROOT / "AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Pages.cs",
    "prd": ROOT / "飞行模拟器产品设计文档.md",
    "rd01": ROOT / "AeroSim/Assets/AeroSim/Resources/RD01/rd01-baseline-manifest.json",
}

errors = []
text = {}
for name, path in FILES.items():
    if not path.is_file():
        errors.append(f"missing {name}: {path.relative_to(ROOT)}")
    else:
        text[name] = path.read_text(encoding="utf-8")

def require(name: str, pattern: str, message: str) -> None:
    if not re.search(pattern, text.get(name, ""), re.MULTILINE | re.DOTALL):
        errors.append(message)

require("evidence", r'goldenShots\s*=\s*\{\s*"A".*"H"\s*\}', "manifest lacks A-H")
require("evidence", r'reviewDistancesMeters\s*=\s*\{\s*1,\s*5,\s*15,\s*40,\s*80\s*\}', "manifest lacks 1/5/15/40/80m")
for metric in ("cpuFrameMs", "gpuFrameMs", "allocatedMemory", "gcAllocated", "drawCalls", "setPassCalls", "triangles", "rendererCount", "materialCount", "renderTexture"):
    require("evidence", metric, f"missing performance metric: {metric}")
for percentile in ("p50", "p95", "p99"):
    require("evidence", percentile, f"missing percentile: {percentile}")
require("evidence", r'residencyTrendMbPerMinute', "missing residency trend")
require("evidence", r'performance-samples\.csv.*performanceSamplesSha256', "missing hashed raw performance sample export")
require("evidence", r'rd01ManifestId.*rd01SchemaVersion.*rd01SourceCommit.*rd01BuildId', "missing RD-01 linkage")
require("evidence", r'gateStatus\s*=\s*"NOT_RUN"', "performance gate must default NOT_RUN")
require("evidence", r'missingEvidence\.Add\("30-minute High target-hardware run"\)', "missing 30-minute evidence guard")
require("evidence", r'SHA256\.Create', "missing SHA-256 artifact hashing")
for mode in ("Reference", "Candidate A", "Candidate B", "Split", "Wipe", "Quad Quality"):
    require("ui", re.escape(mode), f"missing compare mode: {mode}")
require("runner", r'--capture-visual-lab', "missing repeatable batch switch")
require("runner", r'foreach \(string shot in VisualLabEvidence\.ShotIds\).*foreach \(float distance in VisualLabEvidence\.Distances\)', "batch does not traverse full shot/distance matrix")
require("pages", r'Page\.VisualLab\s*=>\s*BuildVisualLab', "Visual Lab page is not routed")
require("rd01", r'"manifestId"\s*:\s*"aerosim-v4-rd01-m0-001"', "RD-01 baseline manifest unavailable")
require("prd", r'RD-09 Visual Lab/性能/捕获', "v4 PRD RD-09 section unavailable")

# A literal PASS assignment in the RD-09 implementation would violate the current evidence state.
for name in ("evidence", "ui"):
    if re.search(r'gateStatus\s*=\s*"PASS"', text.get(name, "")):
        errors.append(f"{name} promotes an unreviewed performance gate to PASS")

if errors:
    print("RD-09 STATIC VALIDATION: FAIL")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("RD-09 STATIC VALIDATION: PASS (contracts only)")
print("Runtime/target-hardware performance: NOT_RUN")
print("Visual review: NOT_RUN")
