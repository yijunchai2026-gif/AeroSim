from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
SLICE = ROOT / "AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.EnvironmentVerticalSlice.cs"
VISUALS = ROOT / "AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.Visuals.cs"
CONTRACTS = ROOT / "AeroSim/Assets/AeroSim/Runtime/Simulation/EnvironmentProductionContracts.cs"
SHADER = ROOT / "AeroSim/Assets/AeroSim/Resources/HeroSurfaceV1.shader"
COAST_SHADER = ROOT / "AeroSim/Assets/AeroSim/Resources/CoastalWaterDetail.shader"
DIAGNOSTICS = ROOT / "AeroSim/Assets/AeroSim/Runtime/Diagnostics/PreviewCapture.cs"


class Rd08VerticalSliceContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.slice = SLICE.read_text(encoding="utf-8")
        cls.visuals = VISUALS.read_text(encoding="utf-8")
        cls.contracts = CONTRACTS.read_text(encoding="utf-8")
        cls.shader = SHADER.read_text(encoding="utf-8")
        cls.coast_shader = COAST_SHADER.read_text(encoding="utf-8")
        cls.diagnostics = DIAGNOSTICS.read_text(encoding="utf-8")

    def test_honest_provenance_and_three_explicit_hero_markers(self):
        self.assertIn('RequiredLabel = "Procedural Vertical Slice"', self.slice)
        self.assertIn("NOT SCANNED", self.slice)
        for zone in ("coastal-takeoff", "cliff-route", "industrial-inspection"):
            self.assertIn(f'HeroZone(parent, "{zone}"', self.slice)
        self.assertEqual(3, len(re.findall(r'HeroZone\(parent, "(?:coastal-takeoff|cliff-route|industrial-inspection)"', self.slice)))

    def test_consumes_rd07_state_sector_material_contracts(self):
        for contract in ("EnvironmentState", "EnvironmentStreamingPolicy", "RegisterEnvironmentObject", "RegisterSurface"):
            self.assertIn(contract, self.slice + self.contracts)
        self.assertIn("SectorId(center)", self.slice)
        self.assertIn("BuildProceduralVerticalSlice(coastalWater)", self.visuals)

    def test_layered_content_and_weather_quality_matrix(self):
        for layer in ("Surface", "Prop", "Vegetation", "Water", "Vfx", "Audio"):
            self.assertIn(layer, self.slice)
        for weather in ("Clear", "Overcast", "Rain"):
            self.assertIn(f'"{weather}"', self.slice)
        for quality in ("Low", "High", "Ultra"):
            self.assertIn(f"AeroSimQuality.{quality}", self.slice)
        self.assertIn("SupportsProceduralSliceVariant", self.diagnostics)

    def test_golden_distance_states_are_data_not_camera_changes(self):
        self.assertRegex(self.slice, r"GoldenDistances\s*=\s*\{\s*1,\s*5,\s*15,\s*40,\s*80\s*\}")
        self.assertIn("goldenViewStates==15", self.diagnostics)
        self.assertNotIn("Camera.main", self.slice)
        self.assertNotIn("View.fieldOfView", self.slice)

    def test_shader_wetness_puddles_and_quality_keywords(self):
        for token in ("_Wetness", "_Puddle", "AEROSIM_HERO_HIGH", "AEROSIM_HERO_ULTRA", "#pragma surface surf Standard"):
            self.assertIn(token, self.shader)
        self.assertIn('Resources.Load<Shader>("HeroSurfaceV1")', self.slice)
        self.assertIn('"HeroSurfaceV1"', self.diagnostics)

    def test_audio_and_vfx_are_deterministic_and_diagnosed(self):
        self.assertIn("AudioClip.Create", self.slice)
        self.assertIn("System.Random(_environmentState.seed", self.slice)
        self.assertIn("particles.randomSeed", self.slice)
        self.assertIn("slice.IsValid", self.diagnostics)

    def test_runtime_coast_content_collision_and_capture_provenance(self):
        for semantic in ("tide-pool", "intertidal-wet-band", "shore-breaker-foam"):
            self.assertIn(semantic, self.slice)
        for token in ("AEROSIM_COAST_HIGH", "AEROSIM_COAST_ULTRA", "_Wetness", "_Wind"):
            self.assertIn(token, self.coast_shader)
        self.assertIn("BuildErodedSeaStack", self.slice)
        self.assertIn("BuildFlightSafetyEnvelope", self.slice)
        self.assertIn("AddComponent<BoxCollider>", self.slice)
        self.assertIn("physicalColliders>=12", self.diagnostics)
        self.assertIn("reviewBand", self.diagnostics)
        self.assertIn("NOT SCANNED", self.diagnostics)


if __name__ == "__main__":
    unittest.main()
