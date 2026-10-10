#!/usr/bin/env python3
"""Generate v3 temp sources from hash-locked v1/v2; never rewrite baseline sources."""
import argparse
import hashlib
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
V2_GENERATOR_SHA = "04c40319198e52a04f8f8a6a02395de5068e6dd9ec26ed0d585ab9211dbb9b71"
V2_SUPPLEMENT_SHA = "87d4033dc4bd14f495e25cf9f7393c71c5890cfc93c33bad730ad21dc7baf141"
V2_REPORT_04_SHA = "469f4d14bf5f5bb87eadb5ed73d65cf7cd4025bc6b93386a71c1e4ac4b605292"


def locked(path, expected):
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError("Frozen source changed: " + path.name)
    return data.decode()


def transform():
    locked(HERE / "prepare-space-v2.py", V2_GENERATOR_SHA)
    partial = locked(HERE / "SpaceSubmitProbeV2.cs", V2_SUPPLEMENT_SHA)
    spec = importlib.util.spec_from_file_location("frozen_space_v2", HERE / "prepare-space-v2.py")
    v2 = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(v2)
    base = v2.transform((HERE / "SpaceSubmitProbe.cs").read_text())
    base = base.replace("SpaceSubmitProbeV2", "SpaceSubmitProbeV3").replace("SpaceCheckV2", "SpaceCheckV3").replace("SpaceReportV2", "SpaceReportV3")
    base = base.replace("--space-submit-qa-v2", "--space-submit-qa-v3").replace("space-submit-v2-report.json", "space-submit-v3-report.json")
    base = base.replace("independent-space-submit-v2-arrow-space-lineboxes", "independent-space-submit-v3-foldout-rendered-raw-arrow")
    anchor = "public int width,height,runtimeErrors;"
    if base.count(anchor) != 1:
        raise ValueError("V2 metadata anchor changed")
    metadata = ('public string v3Scope="All immutable v1/v2 input assertions retained; only closed Advanced Input Mapping content descendants classified UNRENDERED separately; actual keyboard expansion plus six text targets, scroll-to numeric/rate captures, held Raw arrow validation. Synthetic devices/data, not physical/iOS/visual acceptance.";'
                'public string baselineCandidate04V2ReportSha256="' + V2_REPORT_04_SHA + '";'
                'public List<PresentationNoteV3> presentationNotes=new();')
    base = base.replace(anchor, metadata + anchor)
    extension = "yield return V2TextLineBoxes();"
    if base.count(extension) != 1:
        raise ValueError("V2 extension anchor changed")
    base = base.replace(extension, extension + "\n            yield return V3ExpandController();\n            yield return V3ScrollNumericAndRate();\n            yield return V3HeldArrowRaw();")
    partial = partial.replace("SpaceSubmitProbeV2", "SpaceSubmitProbeV3")
    audit = "V2Displayed(e)&&!string.IsNullOrWhiteSpace(e.text)"
    if partial.count(audit) != 1:
        raise ValueError("V2 visible text audit anchor changed")
    partial = partial.replace(audit, "V3Presented(e,page,textScale)&&!string.IsNullOrWhiteSpace(e.text)")
    return base, partial


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    output = args.output_dir.resolve()
    if not str(output).startswith(("/private/tmp/aerosim-space-v3.", "/tmp/aerosim-space-v3.")):
        parser.error("Output must be an explicit temporary aerosim-space-v3.* directory")
    base, partial = transform()
    (output / "SpaceSubmitBaseV3.cs").write_text(base)
    (output / "SpaceSubmitAdaptedV2.cs").write_text(partial)
    print("Frozen v1/v2 verified; candidate04 v2 55FAIL baseline retained: " + V2_REPORT_04_SHA)
    print("Prepared v3 temporary sources: " + str(output))


if __name__ == "__main__":
    main()
