import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location("native_aggregate", Path(__file__).with_name("aggregate-native.py"))
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)


class NativeEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="aerosim-native-evidence-")
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def write_report(self, config, checks=None, duplicates=False):
        folder = self.root / config["name"]
        folder.mkdir(parents=True, exist_ok=True)
        cases = [{"state": state, "scale": 1, "textMultiplier": 1, "checks": []}
                 for state in ("Native-Preferences", "Keyboard-MainFlow", "Interaction-Modal-OSD-ReplayContext")]
        for scale in config["scales"]:
            for text in config["textScales"]:
                for state in qa.EXPECTED_STATES:
                    cases.append({"state": "Fake-Duplicate" if duplicates else state, "scale": scale,
                                  "textMultiplier": text, "checks": checks or [{"rule": "layout.test", "passed": True}]})
        (folder / "report.json").write_text(json.dumps({"version": "1.0.0", "qaContractVersion":qa.CONTRACT_VERSION,"startedUtc": "2026-10-08T00:00:00Z",
            "width": config["requestedWidth"], "height": config["requestedHeight"], "runtimeErrors": 0, "cases": cases}))
        (folder / "execution.json").write_text(json.dumps({"appExitcode": 0}))

    def test_matrix_contract(self):
        self.assertEqual(len(qa.matrix()), 7)
        self.assertEqual(len(qa.EXPECTED_STATES), 34)
        self.assertEqual(len(qa.matrix()[0]["scales"]) * len(qa.matrix()[0]["textScales"]), 24)

    def test_missing_is_incomplete(self):
        report = qa.aggregate(self.root)
        self.assertEqual(report["status"], "INCOMPLETE")
        self.assertEqual(len(report["missing"]), 7)

    def test_nonzero_exit_without_report_is_run_error(self):
        folder = self.root / qa.matrix()[0]["name"]
        folder.mkdir()
        (folder / "execution.json").write_text('{"appExitcode": 2}')
        report = qa.aggregate(self.root)
        self.assertEqual(report["runs"][0]["status"], "RUN_ERROR")
        self.assertEqual(report["totals"]["nonzeroAppExits"], 1)

    def test_valid_assertions_are_not_user_acceptance(self):
        for config in qa.matrix():
            self.write_report(config)
        report = qa.aggregate(self.root)
        self.assertEqual(report["status"], "PASS_AUTOMATED_ONLY")
        self.assertEqual(report["manualAcceptance"], "PENDING")

    def test_legacy_and_text_failures_retained(self):
        checks = [{"rule": "legacy-prd.topbar-height-64", "passed": False}, {"rule": "layout.text-fits", "passed": False}]
        for config in qa.matrix():
            self.write_report(config, checks)
        report = qa.aggregate(self.root)
        self.assertEqual(report["status"], "REVIEW_REQUIRED")
        self.assertGreater(report["totals"]["legacyPrdConflicts"], 0)
        self.assertGreater(report["totals"]["textCandidates"], 0)

    def test_hard_failures_never_hidden(self):
        for config in qa.matrix():
            self.write_report(config, [{"rule": "interaction.launch", "passed": False}])
        self.assertEqual(qa.aggregate(self.root)["status"], "FAIL")

    def test_duplicate_states_do_not_satisfy_coverage(self):
        for config in qa.matrix():
            self.write_report(config, duplicates=True)
        self.assertEqual(qa.aggregate(self.root)["status"], "INCOMPLETE")

    def test_old_contract_cannot_be_reinterpreted_as_candidate02(self):
        for config in qa.matrix():
            self.write_report(config)
            file=self.root / config["name"] / "report.json"
            report=json.loads(file.read_text());report.pop("qaContractVersion")
            file.write_text(json.dumps(report))
        self.assertEqual(qa.aggregate(self.root)["status"], "INCOMPLETE")


if __name__ == "__main__":
    unittest.main()
