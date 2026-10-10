import hashlib
import importlib.util
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("space_v2", HERE / "prepare-space-v2.py")
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)


class ImmutableV1AdaptationTests(unittest.TestCase):
    def setUp(self):
        self.source = (HERE / "SpaceSubmitProbe.cs").read_text()

    def test_submitted_v1_stays_exact(self):
        self.assertEqual(hashlib.sha256(self.source.encode()).hexdigest(), qa.V1_SHA)

    def test_changed_v1_is_rejected(self):
        with self.assertRaises(ValueError):
            qa.transform(self.source + "\n")

    def test_v2_is_distinct_and_retains_original_checks(self):
        result = qa.transform(self.source)
        self.assertEqual(result.count("Check("), self.source.count("Check("))
        self.assertIn('public sealed partial class SpaceSubmitProbeV2:MonoBehaviour', result)
        self.assertIn('Arg("--space-submit-qa-v2")', result)
        self.assertIn('space-submit-v2-report.json', result)
        self.assertIn('baseV1SourceSha256="' + qa.V1_SHA + '"', result)
        self.assertNotIn('Arg("--space-submit-qa")', result)

    def test_extensions_run_before_device_cleanup(self):
        result = qa.transform(self.source)
        self.assertLess(result.index("yield return V2ArrowSpaceAndIntentionalTab()"), result.index("InputSystem.RemoveDevice(_keyboard)"))
        self.assertLess(result.index("yield return V2TextLineBoxes()"), result.index("InputSystem.RemoveDevice(_keyboard)"))


if __name__ == "__main__":
    unittest.main()
