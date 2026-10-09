"""Temporary synthetic fixtures prove missing/failing cases are never accepted."""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

spec = importlib.util.spec_from_file_location("qa_compare", Path(__file__).with_name("compare.py"))
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)


class VisualComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="aerosim-v1-qa-test-")
        self.root = Path(self.temp.name)
        self.captures = self.root / "captures"
        self.captures.mkdir()
        Image.new("RGB", (80, 45), "black").save(self.root / "ref.png")
        self.screen = {"id": "01", "state": "Home", "file": "ref.png", "sha256": qa.digest(self.root / "ref.png"),
                       "width": 80, "height": 45, "regions": [{"id": "all", "rect": [0, 0, 1, 1]}]}
        self.manifest = {"referenceCanvas": {"width": 80, "height": 45},
                         "thresholds": {"pixelDelta": 16, "maxMeanAbsoluteError": 3, "maxChangedFraction": .01, "maxEdgeMeanAbsoluteError": 5}}

    def tearDown(self):
        self.temp.cleanup()

    def compare(self, masks=None):
        return qa.compare_screen(self.screen, self.root, self.captures, self.root / "output", self.manifest, masks or {}, {})

    def test_missing_is_not_run(self):
        self.assertEqual(self.compare()["status"], "NOT_RUN")

    def test_changed_reference_is_locked(self):
        Image.new("RGB", (80, 45), "white").save(self.root / "ref.png")
        self.assertEqual(self.compare()["status"], "REFERENCE_CHANGED")

    def test_different_is_fail_not_geometry_pass(self):
        Image.new("RGB", (80, 45), "white").save(self.captures / "Home.png")
        result = self.compare()
        self.assertEqual(result["status"], "VISUAL_DIFFERENT")
        self.assertEqual(result["rawMetrics"]["changedFraction"], 1)

    def test_identical_still_needs_review(self):
        Image.new("RGB", (80, 45), "black").save(self.captures / "Home.png")
        result = self.compare()
        self.assertEqual(result["status"], "REVIEW_REQUIRED")
        self.assertTrue(result["exactUnmodifiedMatch"])
        self.assertEqual(result["manualAcceptance"], "PENDING")

    def test_mask_preserves_raw_and_excludes_exact_rectangle(self):
        actual = Image.new("RGB", (80, 45), "black")
        actual.paste("white", (0, 0, 20, 45))
        actual.save(self.captures / "Home.png")
        result = self.compare({"Home": [{"rect": [0, 0, .25, 1], "reason": "Synthetic timestamp fixture"}]})
        self.assertEqual(result["rawMetrics"]["changedFraction"], .25)
        self.assertEqual(result["unmaskedMetrics"]["mae"], 0)
        self.assertEqual(result["maskedFraction"], .25)
        self.assertFalse(result["exactUnmodifiedMatch"])

    def test_full_image_mask_is_rejected(self):
        with self.assertRaises(ValueError):
            qa.mask_bitmap((80, 45), [{"rect": [0, 0, 1, 1], "reason": "Bad fixture"}])

    def test_unexplained_mask_is_rejected(self):
        with self.assertRaises(ValueError):
            qa.mask_bitmap((80, 45), [{"rect": [0, 0, .1, 1], "reason": ""}])

    def test_wrong_capture_dimensions_are_not_resized(self):
        Image.new("RGB", (100, 60), "black").save(self.captures / "Home.png")
        self.assertEqual(self.compare()["status"], "INVALID_CAPTURE")

    def test_contain_preserves_aspect_and_padding(self):
        image, details = qa.normalize_reference(Image.new("RGB", (10, 10), "white"), (80, 45))
        self.assertEqual(details["scaledSize"], (45, 45))
        self.assertEqual(image.getpixel((0, 0)), (9, 13, 20))
        self.assertEqual(image.getpixel((40, 20)), (255, 255, 255))


if __name__ == "__main__":
    unittest.main()
