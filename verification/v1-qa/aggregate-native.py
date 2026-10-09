#!/usr/bin/env python3
"""Compact native evidence aggregator; retain every nonzero app exit and legacy/text candidates."""
import argparse
import hashlib
import json
from pathlib import Path

SCRIPT = Path(__file__).with_name("run-matrix.sh")
CONTRACT_VERSION = "v1-candidate02-34states"
EXPECTED_STATES = {screen["state"] for screen in json.loads(Path(__file__).with_name("reference-manifest.json").read_text())["screens"]} | {
    "Aerial-Brief", "Aerial-Preflight", "Aerial-Challenges", "Aerial-Hangar", "Aerial-Records",
    "FPV-Academy", "Aerial-Pause", "Aerial-LaunchInteraction",
    "Aerial-ResultsFailure", "Aerial-FlightGeneric", "FPV-FlightGeneric"}


def matrix():
    # The executable shell matrix is the one source of truth, not a nonexistent matrix.json.
    table = SCRIPT.read_text().split("done <<'QA_MATRIX'\n", 1)[1].split("\nQA_MATRIX", 1)[0]
    result = []
    for row in table.splitlines():
        name, width, height, scales, text = row.split()
        result.append({"name": name, "requestedWidth": int(width), "requestedHeight": int(height),
                       "scales": [float(x) for x in scales.split(",")],
                       "textScales": [float(x) for x in text.split(",")]})
    return result


def aggregate(directory):
    runs, missing, rules = [], [], {}
    totals = {"runs": 0, "states": 0, "checks": 0, "hardFailures": 0, "textCandidates": 0,
              "legacyPrdConflicts": 0, "runtimeErrors": 0, "nonzeroAppExits": 0, "evidenceErrors": 0}
    for config in matrix():
        name = config["name"]
        folder = directory / name
        source, execution = folder / "report.json", folder / "execution.json"
        run = {**config, "status": "NOT_RUN", "expectedStates": len(EXPECTED_STATES) * len(config["scales"]) * len(config["textScales"]) + 3}
        if execution.is_file():
            run.update(json.loads(execution.read_text()))
            totals["nonzeroAppExits"] += int(run.get("appExitcode", 0) != 0)
        else:
            run["evidenceErrors"] = ["Missing recorded application exit code"]
        if not source.is_file():
            run["reason"] = "Application did not produce report.json; no successful checks are inferred"
            run["status"] = "RUN_ERROR" if execution.is_file() else "NOT_RUN"
            missing.append(name)
            totals["evidenceErrors"] += 1
            runs.append(run)
            continue
        try:
            report = json.loads(source.read_text())
        except (ValueError, OSError) as error:
            run.update(status="RUN_ERROR", reason=str(error))
            totals["evidenceErrors"] += 1
            runs.append(run)
            continue
        errors = run.setdefault("evidenceErrors", [])
        cases = report.get("cases", [])
        if len(cases) != run["expectedStates"]:
            errors.append(f"Expected {run['expectedStates']} states, received {len(cases)}")
        if report.get("width") != config["requestedWidth"] or report.get("height") != config["requestedHeight"]:
            errors.append("Actual viewport differs from requested viewport")
        if not report.get("startedUtc") or not report.get("version"):
            errors.append("Missing native build version/UTC provenance")
        if report.get("qaContractVersion") != CONTRACT_VERSION:
            errors.append("QA contract differs from frozen candidate02; historical reports require their original contract, never reinterpret them as this run")
        for scale in config["scales"]:
            for text in config["textScales"]:
                matched = [c for c in cases if abs(c.get("scale", 0)-scale)<.001 and abs(c.get("textMultiplier", 0)-text)<.001
                           and c.get("state") not in ("Native-Preferences", "Keyboard-MainFlow", "Interaction-Modal-OSD-ReplayContext")]
                if len(matched) != len(EXPECTED_STATES):
                    errors.append(f"Incomplete UI/HUD {scale}/Text {text}: {len(matched)} of {len(EXPECTED_STATES)} states")
                if {c.get("state") for c in matched} != EXPECTED_STATES:
                    errors.append(f"Missing/duplicated state identities at UI/HUD {scale}/Text {text}")
        failed, candidates, legacy = [], [], []
        checks = 0
        for case in cases:
            for check in case.get("checks", []):
                checks += 1
                rule = check.get("rule", "UNKNOWN")
                stats = rules.setdefault(rule, {"checks": 0, "failed": 0})
                stats["checks"] += 1
                if check.get("passed"):
                    continue
                stats["failed"] += 1
                item = {"state": case.get("state"), "scale": case.get("scale"), "textMultiplier": case.get("textMultiplier"), **check}
                if rule.startswith("legacy-prd."):
                    legacy.append(item)
                elif rule == "layout.text-fits":
                    candidates.append(item)
                else:
                    failed.append(item)
        runtime = report.get("runtimeErrors", 0)
        abnormal_exit = run.get("appExitcode", 0) != 0 and not (failed or candidates or legacy or runtime)
        if abnormal_exit:
            errors.append("Nonzero app exit without an explained check/runtime failure")
        run.update(version=report.get("version"), startedUtc=report.get("startedUtc"),fixtureDescription=report.get("fixtureDescription","UNVERIFIED fixture scenario"),
                   actualWidth=report.get("width"), actualHeight=report.get("height"), states=len(cases), checks=checks,
                   reportSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                   hardFailures=len(failed), textCandidates=len(candidates), legacyPrdConflicts=len(legacy), runtimeErrors=runtime,
                   failureExamples=failed[:30], textExamples=candidates[:10], legacyExamples=legacy[:10])
        run["status"] = "FAIL" if failed or runtime or errors else "REVIEW_REQUIRED" if candidates or legacy else "PASS_AUTOMATED_ONLY"
        totals["runs"] += 1
        totals["states"] += len(cases)
        totals["checks"] += checks
        totals["hardFailures"] += len(failed)
        totals["textCandidates"] += len(candidates)
        totals["legacyPrdConflicts"] += len(legacy)
        totals["runtimeErrors"] += runtime
        totals["evidenceErrors"] += len(errors)
        runs.append(run)
    status = ("INCOMPLETE" if missing or totals["evidenceErrors"] else "FAIL" if totals["hardFailures"] or totals["runtimeErrors"]
              else "REVIEW_REQUIRED" if totals["textCandidates"] or totals["legacyPrdConflicts"] or totals["nonzeroAppExits"] else "PASS_AUTOMATED_ONLY")
    return {"schemaVersion": 1, "targetVersion": "1.0.0", "qaContractVersion": CONTRACT_VERSION,"status": status, "matrixSource": str(SCRIPT),
            "totals": totals, "missing": missing, "runs": runs, "rules": rules, "manualAcceptance": "PENDING",
            "limitations": ["Legacy PRD numeric conflicts and text measurement candidates are retained, never hidden.",
                            "PASS_AUTOMATED_ONLY is native assertion evidence, not visual parity or user acceptance.",
                            "Desktop logical window tests do not establish iOS/iPad hardware validation."]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--record-run")
    parser.add_argument("--exitcode", type=int)
    args = parser.parse_args()
    directory = args.output.resolve()
    if args.record_run:
        if args.record_run not in {row["name"] for row in matrix()} or args.exitcode is None:
            parser.error("A known matrix name and explicit exit code are mandatory")
        record = directory / args.record_run / "execution.json"
        record.parent.mkdir(parents=True, exist_ok=True)
        record.write_text(json.dumps({"appExitcode": args.exitcode}, indent=2) + "\n")
        return 0
    directory.mkdir(parents=True, exist_ok=True)
    summary = aggregate(directory)
    file = directory / "native-summary.json"
    file.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"status": summary["status"], "totals": summary["totals"], "summary": str(file)}, ensure_ascii=False))
    return 0 if summary["status"] == "PASS_AUTOMATED_ONLY" else 2


if __name__ == "__main__":
    raise SystemExit(main())
