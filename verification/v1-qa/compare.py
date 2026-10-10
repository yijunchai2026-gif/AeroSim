#!/usr/bin/env python3
"""Read-only visual QA of locked concepts and genuine app captures. No automatic acceptance."""
import argparse
import hashlib
import html
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST = Path(__file__).with_name("reference-manifest.json")


def digest(file):
    return hashlib.sha256(Path(file).read_bytes()).hexdigest()


def normalize_reference(source, canvas):
    """Contain original without aspect-ratio distortion; never normalize the capture."""
    width, height = canvas
    factor = min(width / source.width, height / source.height)
    size = (round(source.width * factor), round(source.height * factor))
    offset = ((width - size[0]) // 2, (height - size[1]) // 2)
    output = Image.new("RGB", canvas, "#090D14")
    output.paste(source.convert("RGB").resize(size, Image.Resampling.LANCZOS), offset)
    return output, {"mode": "contain", "scale": factor, "scaledSize": size, "offset": offset,
                    "paddingColor": "#090D14", "originalSize": list(source.size)}


def box(rect, width, height):
    if len(rect) != 4 or any(not isinstance(v, (int, float)) for v in rect):
        raise ValueError("Rectangle requires numeric normalized x/y/width/height")
    x, y, w, h = rect
    if x < 0 or y < 0 or w <= 0 or h <= 0 or x + w > 1.000001 or y + h > 1.000001:
        raise ValueError("Rectangle out of normalized canvas")
    return (round(x * width), round(y * height), round((x + w) * width), round((y + h) * height))


def mask_bitmap(size, masks):
    allowed = np.ones((size[1], size[0]), dtype=bool)
    for mask in masks:
        if not mask.get("reason", "").strip():
            raise ValueError("Every mask needs a disclosed reason")
        left, top, right, bottom = box(mask["rect"], *size)
        allowed[top:bottom, left:right] = False
    if not allowed.any():
        raise ValueError("Mask excludes the entire image")
    return allowed


def metric(reference, actual, allowed, thresholds):
    ref = np.asarray(reference).astype(np.int16)
    act = np.asarray(actual).astype(np.int16)
    diff = np.abs(ref - act)
    per_pixel = diff.max(axis=2)
    ref_edges = np.asarray(reference.convert("L").filter(ImageFilter.FIND_EDGES)).astype(np.int16)
    act_edges = np.asarray(actual.convert("L").filter(ImageFilter.FIND_EDGES)).astype(np.int16)
    edges = np.abs(ref_edges - act_edges)
    count = int(allowed.sum())
    if count == 0:
        return {"status": "MASKED_OUT", "pixels": 0}
    mae = float(diff[allowed].mean())
    changed = float((per_pixel[allowed] > thresholds["pixelDelta"]).mean())
    edge_mae = float(edges[allowed].mean())
    passed = (mae <= thresholds["maxMeanAbsoluteError"] and
              changed <= thresholds["maxChangedFraction"] and
              edge_mae <= thresholds["maxEdgeMeanAbsoluteError"])
    return {"status": "WITHIN_TOLERANCE" if passed else "DIFFERENT", "pixels": count,
            "mae": round(mae, 4), "changedFraction": round(changed, 6),
            "edgeMae": round(edge_mae, 4), "identicalPixels": bool((per_pixel[allowed] == 0).all())}


def region_mask(region, allowed):
    result = np.zeros_like(allowed)
    left, top, right, bottom = box(region["rect"], allowed.shape[1], allowed.shape[0])
    result[top:bottom, left:right] = allowed[top:bottom, left:right]
    return result


def locate_capture(directory, state):
    for name in (f"100-text100-{state}.png", f"100-text1-{state}.png", f"{state}.png"):
        file = directory / name
        if file.is_file():
            return file
    return None


def read_capture_meta(captures):
    meta = captures / "capture-meta.json"
    if meta.is_file():
        return json.loads(meta.read_text()), str(meta)
    # Genuine existing instrumented capture reports carry dimensions/scales/version/time.
    report = captures / "report.json"
    if report.is_file():
        return json.loads(report.read_text()), str(report)
    return {}, None


def native_evidence(paths):
    result = []
    for file in paths:
        path = Path(file).resolve()
        if not path.is_file():
            result.append({"file": str(path), "status": "NOT_RUN"})
            continue
        source = json.loads(path.read_text())
        failures, text_candidates, legacy_conflicts, count = [], [], [], 0
        for case in source.get("cases", []):
            for check in case.get("checks", []):
                count += 1
                if check.get("passed"):
                    continue
                item = {"state": case.get("state"), **check}
                if check.get("rule") == "layout.text-fits":
                    text_candidates.append(item)
                elif check.get("rule", "").startswith("legacy-prd."):
                    legacy_conflicts.append(item)
                else:
                    failures.append(item)
        result.append({"file": str(path), "sha256": digest(path), "version": source.get("version"),
                       "checks": count, "failures": len(failures),
                       "textCandidates": len(text_candidates), "legacyPrdConflicts": len(legacy_conflicts),
                       "runtimeErrors": source.get("runtimeErrors"),
                       "status": "FAIL" if failures or source.get("runtimeErrors") else "REVIEW_REQUIRED",
                       "failedExamples": failures[:20], "textExamples": text_candidates[:10],
                       "legacyConflictExamples": legacy_conflicts[:10],
                       "note": "No failure is hidden. Text candidates and legacy PRD conflicts remain explicit; zero checks is NOT_RUN."})
        if count == 0:
            result[-1]["status"] = "NOT_RUN"
    return result


def evidence_images(reference, actual, masks, regions, output, prefix):
    output.mkdir(parents=True, exist_ok=True)
    w, h = reference.size
    side = Image.new("RGB", (w * 2, h))
    side.paste(reference, (0, 0))
    side.paste(actual, (w, 0))
    overlay = Image.blend(reference, actual, .5)
    diff = np.abs(np.asarray(reference).astype(np.int16) - np.asarray(actual).astype(np.int16))
    heat = np.zeros((h, w, 3), dtype=np.uint8)
    heat[:, :, 0] = np.minimum(255, diff.max(axis=2) * 4).astype(np.uint8)
    heat[:, :, 1] = np.minimum(170, diff.mean(axis=2) * 2).astype(np.uint8)
    diff_img = Image.fromarray(heat)
    annotated = reference.copy()
    draw = ImageDraw.Draw(annotated)
    for region in regions:
        rect = box(region["rect"], w, h)
        draw.rectangle(rect, outline="#25d9cf", width=3)
        draw.text((rect[0] + 4, rect[1] + 4), region["id"], fill="white", stroke_width=1, stroke_fill="black")
    for mask in masks:
        rect = box(mask["rect"], w, h)
        ImageDraw.Draw(diff_img).rectangle(rect, outline="#ffea00", width=5)
        draw.rectangle(rect, outline="#ffea00", width=5)
        draw.text((rect[0] + 4, rect[1] + 20), "MASK: " + mask["reason"], fill="yellow")
    names = {}
    for key, image in (("sideBySide", side), ("overlay", overlay), ("diff", diff_img), ("regions", annotated)):
        file = output / f"{prefix}-{key}.png"
        image.save(file)
        names[key] = file.name
    return names


def compare_screen(screen, root, captures, output, manifest, extra_masks, meta):
    result = {"id": screen["id"], "state": screen["state"], "reference": screen["file"],
              "referenceSha256": screen["sha256"], "semanticState": screen.get("semanticState")}
    reference = root / screen["file"]
    if not reference.is_file() or digest(reference) != screen["sha256"]:
        return {**result, "status": "REFERENCE_CHANGED", "reason": "Locked original missing or SHA256 differs"}
    with Image.open(reference) as source:
        if source.size != (screen["width"], screen["height"]):
            return {**result, "status": "REFERENCE_CHANGED", "reason": "Locked original size differs"}
        normalized, normalization = normalize_reference(source, (manifest["referenceCanvas"]["width"], manifest["referenceCanvas"]["height"]))
    result["normalization"] = normalization
    actual_file = locate_capture(captures, screen["state"])
    if actual_file is None:
        return {**result, "status": "NOT_RUN", "reason": "No 100% real app capture for this target"}
    result.update(capture=str(actual_file), captureSha256=digest(actual_file))
    with Image.open(actual_file) as source:
        actual = source.convert("RGB")
    if actual.size != normalized.size:
        return {**result, "status": "INVALID_CAPTURE", "actualSize": list(actual.size),
                "reason": "Baseline comparison requires actual 1920x1080 capture; no capture resize permitted"}
    masks = screen.get("masks", []) + extra_masks.get(screen["state"], [])
    allowed = mask_bitmap(actual.size, masks)
    raw = metric(normalized, actual, np.ones_like(allowed), manifest["thresholds"])
    filtered = metric(normalized, actual, allowed, manifest["thresholds"])
    regions = [{"id": region["id"], "rect": region["rect"],
                **metric(normalized, actual, region_mask(region, allowed), manifest["thresholds"])}
               for region in screen.get("regions", [])]
    result.update(rawMetrics=raw, unmaskedMetrics=filtered, regions=regions, masks=masks,
                  maskedFraction=round(1 - float(allowed.mean()), 6))
    mask_problem = (1 - allowed.mean()) > .40 or any(r["status"] == "MASKED_OUT" for r in regions)
    different = filtered["status"] == "DIFFERENT" or any(r["status"] == "DIFFERENT" for r in regions)
    result["status"] = "MASK_REVIEW_REQUIRED" if mask_problem else "VISUAL_DIFFERENT" if different else "REVIEW_REQUIRED"
    result["exactUnmodifiedMatch"] = raw["identicalPixels"] and not masks
    result["evidence"] = evidence_images(normalized, actual, masks, screen.get("regions", []), output, screen["id"] + "-" + screen["state"])
    case_meta = next((c for c in meta.get("cases", []) if c.get("state") == screen["state"] and abs(c.get("scale", 0)-1)<.001 and abs(c.get("textMultiplier", 0)-1)<.001), None)
    result["captureMetadata"] = case_meta and {k: case_meta[k] for k in ("width", "height", "scale", "textMultiplier") if k in case_meta}
    result["provenance"] = "RECORDED" if case_meta or meta.get("captureSource") else "UNVERIFIED"
    # Metric success is never a semantic/interactive/user acceptance, even when pixels match.
    result["manualAcceptance"] = "PENDING"
    return result


def render_report(report, output):
    esc = lambda value: html.escape(str(value), quote=True)
    rows, details = [], []
    for page in report["screens"]:
        m = page.get("rawMetrics", {})
        rows.append(f'<tr><td>{esc(page["id"])}</td><td>{esc(page["state"])}</td><td>{esc(page["status"])}</td><td>{esc(m.get("mae", "—"))}</td><td>{esc(m.get("changedFraction", "—"))}</td></tr>')
        figures = "".join(f'<figure><figcaption>{esc(key)}</figcaption><a href="evidence/{esc(file)}"><img loading="lazy" src="evidence/{esc(file)}"></a></figure>' for key, file in page.get("evidence", {}).items())
        details.append(f'<details><summary>{esc(page["state"])} · {esc(page["status"])}</summary><p>{esc(page.get("semanticState", ""))}</p>{figures}<pre>{esc(json.dumps(page, ensure_ascii=False, indent=2))}</pre></details>')
    document = f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>AeroSim 1.0 Visual QA</title><style>body{{background:#090d14;color:#f2f6fc;font:15px/1.6 system-ui;margin:0}}main{{max-width:1600px;padding:24px;margin:auto}}a{{color:#8fb1ff}}table{{border-collapse:collapse;width:100%}}td,th{{text-align:left;border:1px solid #2a374a;padding:8px}}details{{border:1px solid #2a374a;margin:16px 0;padding:12px}}img{{max-width:100%;height:auto}}pre{{white-space:pre-wrap;overflow-wrap:anywhere}}figure{{margin:8px 0}}.warning{{background:#33281a;padding:16px;border-left:4px solid #ffb547}}</style><main><h1>AeroSim 1.0 · 独立视觉 QA</h1><p class="warning">{esc(report["status"])} · 用户人工验收 PENDING。本报告不以几何通过冒充逐像素一致；几何/文字/语义/输入均需独立验收。缺图 NOT_RUN，目标哈希改变 REFERENCE_CHANGED。原图等比 contain 至 1920×1080；运行截图绝不自动伸缩。</p><p>应用版本 {esc(report["captureVersion"])} · 来源 {esc(report["captureMetadataSource"])} · 原图 SHA manifest {esc(report["manifestSha256"])}</p><p>阈值 {esc(report["thresholds"])}。WITHIN_TOLERANCE 不是一模一样；exactUnmodifiedMatch 仅说明未遮罩像素严格相同。掩码不改变原图，黄色框明确标示；保留未遮罩 rawMetrics。</p><table><tr><th>#</th><th>Page</th><th>Visual state</th><th>Raw MAE</th><th>Raw changed fraction</th></tr>{"".join(rows)}</table><h2>逐页原图 / 真截图 / overlay / diff / ROI</h2>{"".join(details)}<h2>独立原生回归</h2><pre>{esc(json.dumps(report["nativeEvidence"], ensure_ascii=False, indent=2))}</pre><h2>验收边界</h2><pre>{esc(json.dumps(report["limitations"], ensure_ascii=False, indent=2))}</pre></main></html>'''
    (output / "report.html").write_text(document)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--captures", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--masks", type=Path, help="JSON state => [{rect: [x,y,w,h], reason: ...}], disclosed, optional")
    parser.add_argument("--native-report", type=Path, action="append", default=[])
    args = parser.parse_args(argv)
    manifest = json.loads(args.manifest.read_text())
    captures, output = args.captures.resolve(), args.output.resolve()
    if output == captures or output in captures.parents or captures in output.parents:
        parser.error("Output must be separate from the source capture directory")
    output.mkdir(parents=True, exist_ok=True)
    meta, meta_source = read_capture_meta(captures)
    masks = json.loads(args.masks.read_text()) if args.masks else {}
    unknown = set(masks) - {s["state"] for s in manifest["screens"]}
    if unknown:
        parser.error("Unknown masked states: " + ", ".join(sorted(unknown)))
    pages = [compare_screen(screen, args.root, captures, output / "evidence", manifest, masks, meta) for screen in manifest["screens"]]
    statuses = {status: sum(p["status"] == status for p in pages) for status in sorted({p["status"] for p in pages})}
    missing = any(p["status"] in ("NOT_RUN", "INVALID_CAPTURE", "REFERENCE_CHANGED") for p in pages)
    different = any(p["status"] == "VISUAL_DIFFERENT" for p in pages)
    report = {"schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(),
              "status": "INCOMPLETE" if missing else "VISUAL_DIFFERENT" if different else "REVIEW_REQUIRED",
              "targetVersion": manifest["targetVersion"], "captureVersion": meta.get("version", "UNVERIFIED"),
              "captureMetadataSource": meta_source, "manifestSha256": digest(args.manifest),
              "thresholds": manifest["thresholds"], "counts": statuses, "screens": pages,
              "nativeEvidence": native_evidence(args.native_report), "manualAcceptance": "PENDING",
              "limitations": ["No fabricated PASS; concepts are raster designs, not executable component specs.",
                  "Core ROI manually calibrated, not automated OCR/component bounds acceptance.",
                  "Pixel/edge difference measures structure, typography/color/art jointly; no semantic/function correctness is inferred.",
                  "Missing or unverified capture metadata prevents build provenance acceptance.",
                  "All optional masks disclosed and raw metrics retained; masking cannot prove exact equality.",
                  "Scaling matrix, keyboard/focus/controller, saves, real mobile hardware, performance and user approval are separate gates.",
                  "Legacy 0.4 PRD topbar64/navigation240 vs latest picture-led 1.0 shell88/248 conflict remains disclosed; old numeric failures are not hidden."]}
    (output / "summary.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    render_report(report, output)
    print(json.dumps({"status": report["status"], "counts": statuses, "summary": str(output / "summary.json"), "report": str(output / "report.html")}, ensure_ascii=False))
    return 2 if missing or different else 0


if __name__ == "__main__":
    sys.exit(main())
