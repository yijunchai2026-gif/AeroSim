#!/usr/bin/env python3
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "AeroSim/Assets/AeroSim/Resources/RD01/rd01-baseline-manifest.json"
GRAPHICS = ROOT / "AeroSim/ProjectSettings/GraphicsSettings.asset"
PACKAGES = ROOT / "AeroSim/Packages/manifest.json"


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    require(data["schemaVersion"] == "aerosim.evidence/v1", "unexpected schema")
    require(data["prdVersion"] == "4.0" and data["workPackage"] == "RD-01" and data["milestone"] == "M0", "wrong ownership")
    require(re.fullmatch(r"[0-9a-f]{40}", data["sourceBaseline"]["headCommit"]) is not None, "HEAD is not a full SHA")
    require(data["build"]["status"] == "NOT_RUN", "unbuilt baseline claims a build result")
    quality = data["qualityDeclaration"]
    require((quality["levelId"], quality["weightedScore"], quality["rcDecision"], quality["gateStatus"]) == ("L1", 29.5, "NO-GO", "NOT_RUN"), "quality declaration overclaims")
    hardware = data["targetHardware"]
    require(hardware["status"] == "CANDIDATE_NOT_BENCHMARKED" and hardware["performanceEvidence"] == "NOT_RUN", "hardware declaration overclaims")
    adr = data["renderPipelineAdr"]
    require(adr["adrId"] == "ADR-RND-001" and adr["mainlinePipeline"] == "Built-in" and adr["migrationApproval"] == "NOT_APPROVED", "ADR state is unsafe")
    require([spike["pipeline"] for spike in adr["spikes"]] == ["Built-in", "URP", "HDRP"] and all(spike["status"] == "NOT_RUN" for spike in adr["spikes"]), "spike state is not explicit")
    require("m_CustomRenderPipeline: {fileID: 0}" in GRAPHICS.read_text(encoding="utf-8"), "project no longer uses Built-in")
    packages = json.loads(PACKAGES.read_text(encoding="utf-8"))["dependencies"]
    require("com.unity.render-pipelines.universal" not in packages and "com.unity.render-pipelines.high-definition" not in packages, "SRP package added without superseding ADR")

    fixed = [data["fixedReplay"]]
    matrices = fixed + data["goldenShotCases"] + data["aircraftShotCases"] + data["cameraMatrixCases"]
    ids = [case["caseId"] for case in matrices]
    require(len(ids) == 35 and len(set(ids)) == 35, "case IDs are incomplete or duplicated")
    require(ids[0] == "RPL-MAIN-001", "fixed replay ID changed")
    require([case["caseId"] for case in data["goldenShotCases"]] == [f"G3-GOLDEN-{letter}" for letter in "ABCDEFGH"], "A-H IDs changed")
    require(all(case["status"] == "NOT_RUN" for case in matrices), "baseline case claims PASS")
    print("RD-01 manifest contract: PASS (35 fixed IDs; all evidence gates remain NOT_RUN)")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, json.JSONDecodeError) as error:
        print(f"RD-01 manifest contract: FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
