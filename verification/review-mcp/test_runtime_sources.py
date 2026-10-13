#!/usr/bin/env python3
"""Static safety checks for Runtime Scene Editor and Runtime MCP sources."""

import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "AeroSim" / "Assets" / "AeroSim" / "Runtime"


class RuntimeSourceSafetyTests(unittest.TestCase):
    def test_runtime_sources_do_not_reference_unity_editor_or_process_execution(self):
        files = list((RUNTIME / "SceneEditor").glob("*.cs")) + list((RUNTIME / "Mcp").glob("*.cs"))
        self.assertTrue(files)
        combined = "\n".join(path.read_text(encoding="utf-8") for path in files)
        self.assertNotIn("UnityEditor", combined)
        self.assertNotIn("Process.Start", combined)
        self.assertNotIn("System.Reflection", combined)

    def test_runtime_mcp_is_read_only_and_uses_persistent_data_path(self):
        source = (RUNTIME / "Mcp" / "RuntimeMcpBridge.cs").read_text(encoding="utf-8")
        for tool in ("inspect_scene", "capture_game_view", "get_console_logs", "get_performance_metrics", "validate_scene"):
            self.assertIn(f'"{tool}"', source)
        for forbidden in ("set_transform", "spawn_object", "delete_object", "save_scene", "run_command"):
            self.assertNotIn(f'"{forbidden}"', source)
        self.assertIn("Application.persistentDataPath", source)
        self.assertIn('"AeroSimReview"', source)
        self.assertIn("MaxRequestsPerSecond", source)
        self.assertIn("MaxQueueDepth", source)
        self.assertIn("Directory.EnumerateFiles", source)
        self.assertNotIn("Directory.GetFiles", source)
        self.assertIn("MaxInboxEntriesPerPoll", source)
        self.assertIn("File.GetLastWriteTimeUtc(path).Ticks", source)
        self.assertIn("TimestampTicks.CompareTo", source)
        self.assertIn("string.CompareOrdinal(left.Name, right.Name)", source)
        self.assertNotIn("SortedSet<string>", source)
        self.assertIn("SceneCacheSeconds", source)
        self.assertIn("PerformanceCacheSeconds", source)
        self.assertIn("MaxSceneObjects", source)
        self.assertIn("capture_game_view requires includeUi=true; no artifact was generated", source)
        self.assertLess(source.index("ValidateCaptureRequest(request)"), source.index("StartCoroutine(CaptureGameView"))

    def test_material_and_gizmo_lifetimes_are_explicit(self):
        lifetime = (RUNTIME / "SceneEditor" / "DraftMaterialLifetime.cs").read_text(encoding="utf-8")
        panel = (RUNTIME / "SceneEditor" / "RuntimeSceneEditorPanel.cs").read_text(encoding="utf-8")
        service = (RUNTIME / "SceneEditor" / "RuntimeSceneEditorService.cs").read_text(encoding="utf-8")
        self.assertIn("renderers[i].sharedMaterials = originals[i]", lifetime)
        self.assertIn("private void OnDestroy() => Release()", lifetime)
        self.assertIn("materials?.Acquire()", service)
        self.assertIn("materials?.Release()", service)
        self.assertIn("private Material[] materials", panel)
        self.assertIn("service.Selection.Changed += MarkSelectionDirty", panel)
        self.assertIn("service.StructureChanged += MarkStructureDirty", panel)
        self.assertIn("service.TransformChanged += MarkBoundsDirty", panel)
        self.assertIn("service.Selection.Changed -= MarkSelectionDirty", panel)
        self.assertIn("private Button deleteSelected", panel)
        self.assertIn("deleteSelected.SetEnabled(hasSelection)", panel)
        self.assertIn("if (selected == null || !selected.gameObject.activeSelf) return;", panel)
        self.assertIn("DestroyImmediate(material)", panel)
        self.assertEqual(1, panel.count("selected.GetComponentsInChildren<Renderer>"))

    def test_draft_guard_and_required_findings_are_present(self):
        editor = (RUNTIME / "SceneEditor" / "RuntimeSceneEditorService.cs").read_text(encoding="utf-8")
        contracts = (RUNTIME / "SceneEditor" / "RuntimeSceneContracts.cs").read_text(encoding="utf-8")
        validation = (RUNTIME / "Mcp" / "RuntimeMcpBridge.cs").read_text(encoding="utf-8")
        self.assertIn("DRAFT_NOT_USER_CONFIRMED", contracts)
        self.assertIn("Only an active draft sandbox object can be edited", editor)
        self.assertIn("if (!IsInsideBoundary(position))", editor)
        self.assertIn("if (!IsScaleInsideEnvelope(scale))", editor)
        self.assertIn("RuntimeSceneEditorService.IsScaleInsideEnvelope(scale)", validation)
        for finding in ("POSSIBLE_FLOATING_DRAFT", "ABNORMAL_SCALE", "NON_FINITE_TRANSFORM", "OUTSIDE_SANDBOX_BOUNDARY", "MISSING_COLLIDER", "MISSING_RENDERER"):
            self.assertIn(finding, validation)
        self.assertIn("not a geological", validation)


if __name__ == "__main__":
    unittest.main()
