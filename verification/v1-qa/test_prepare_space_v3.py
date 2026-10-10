import hashlib
import importlib.util
import unittest
from pathlib import Path
from unittest.mock import patch

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("space_v3", HERE / "prepare-space-v3.py")
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)


class SupplementalV3Tests(unittest.TestCase):
    def test_frozen_sources_verified_and_inputs_retained(self):
        base, partial = qa.transform()
        original = (HERE / "SpaceSubmitProbe.cs").read_text()
        v2 = (HERE / "SpaceSubmitProbeV2.cs").read_text()
        self.assertEqual(base.count("Check("), original.count("Check("))
        self.assertEqual(partial.count("Check("), v2.count("Check("))
        self.assertIn("yield return V2ArrowSpaceAndIntentionalTab()", base)
        self.assertIn("yield return SpaceMustNotAct(\"v2-arrow-then-space-\"+stage)", partial)
        self.assertIn('SpaceSubmitProbeV3', base)
        self.assertIn('space-submit-v3-report.json', base)
        self.assertIn(qa.V2_REPORT_04_SHA, base)

    def test_only_visible_classification_hook_changes_v2_audit(self):
        _, partial = qa.transform()
        expected = (HERE / "SpaceSubmitProbeV2.cs").read_text().replace("SpaceSubmitProbeV2", "SpaceSubmitProbeV3").replace("V2Displayed(e)&&!string.IsNullOrWhiteSpace(e.text)", "V3Presented(e,page,textScale)&&!string.IsNullOrWhiteSpace(e.text)")
        self.assertEqual(partial, expected)
        source = (HERE / "SpaceSubmitProbeV3.cs").read_text()
        self.assertIn('foldout.text=="Advanced Input Mapping" && !foldout.value && foldout.contentContainer.Contains(element)', source)
        self.assertIn('classification="UNRENDERED"', source)

    def test_new_extensions_before_device_cleanup(self):
        base, _ = qa.transform()
        for name in ["V3ExpandController", "V3ScrollNumericAndRate", "V3HeldArrowRaw"]:
            self.assertLess(base.index("yield return " + name), base.index("InputSystem.RemoveDevice(_keyboard)"))

    def test_lock_rejects_modified_source(self):
        with patch.object(Path, "read_bytes", return_value=b"modified"):
            with self.assertRaises(ValueError):
                qa.locked(HERE / "SpaceSubmitProbeV2.cs", qa.V2_SUPPLEMENT_SHA)


if __name__ == "__main__":
    unittest.main()
