#!/usr/bin/env python3
"""Generate a temporary v2 base from the immutable submitted v1; never edit v1 or the matrix."""
import argparse
import hashlib
from pathlib import Path

V1_SHA = "fe75cf13cdaf6d59d85e225f470b73ff212be8b73798ce9a546d97f592c1c6bd"
HERE = Path(__file__).resolve().parent


def transform(source):
    if hashlib.sha256(source.encode()).hexdigest() != V1_SHA:
        raise ValueError("Submitted SpaceSubmitProbe v1 changed; refusing to reinterpret baseline")
    source = source.replace("SpaceSubmitProbe", "SpaceSubmitProbeV2")
    source = source.replace("SpaceCheck", "SpaceCheckV2").replace("SpaceReport", "SpaceReportV2")
    source = source.replace("public sealed class SpaceSubmitProbeV2", "public sealed partial class SpaceSubmitProbeV2")
    source = source.replace("--space-submit-qa", "--space-submit-qa-v2")
    source = source.replace("independent-space-submit-v1", "independent-space-submit-v2-arrow-space-lineboxes")
    source = source.replace("space-submit-report.json", "space-submit-v2-report.json")
    metadata_anchor = "public int width,height,runtimeErrors;"
    if source.count(metadata_anchor) != 1:
        raise ValueError("V1 report metadata anchor changed")
    source = source.replace(metadata_anchor, 'public string baseV1SourceSha256="' + V1_SHA + '";public string v2Scope="Retains all v1 assertions; adds 3 resume modes x 4 actual Arrow->Space sequences, intentional Tab->Space, nested rendered TextElement lineboxes at Text100/125/150/200 on five pages. Synthetic input/data only; not full scale matrix or user acceptance.";' + metadata_anchor)
    anchor = 'LocalStore.Profile.settings.textScale=1;LocalStore.Save();_app.ShowPage(Page.Home);yield return Settle();'
    if source.count(anchor) != 1:
        raise ValueError("V1 finalization anchor changed")
    return source.replace(anchor, 'yield return V2ArrowSpaceAndIntentionalTab();\n            yield return V2TextLineBoxes();\n            ' + anchor)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    if not str(output).startswith(("/private/tmp/aerosim-space-v2.", "/tmp/aerosim-space-v2.")):
        parser.error("Generated base must be in an explicit temporary aerosim-space-v2.* directory")
    output.write_text(transform((HERE / "SpaceSubmitProbe.cs").read_text()))
    print("Immutable Space v1 source: " + V1_SHA)
    print("Prepared v2 base: " + str(output))


if __name__ == "__main__":
    main()
