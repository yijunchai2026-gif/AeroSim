using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.Diagnostics
{
    [Serializable]
    public sealed class HeroCliffCaptureFrame
    {
        public string file;
        public string channel;
        public string viewId;
        public string status = HeroCliffCaptureRoute.Captured;
        public float distanceMeters;
        public Vector3 camera;
        public Vector3 target;
        public float verticalFov;
        public string sha256;
        public string renderPath;
        public float diagnosticMode;
        public bool distinctFromFinalColor;
    }

    [Serializable]
    public sealed class HeroCliffCapturePackage
    {
        public string schema = "aerosim.hero-cliff-capture/v2";
        public string generatedUtc;
        public string pipeline = "Built-in";
        public string status = HeroCliffCaptureRoute.Captured;
        public string reviewStatus = HeroCliffCaptureRoute.NotReviewed;
        public string gateStatus = HeroCliffCaptureRoute.GateNotRun;
        public string route = "P11_FIXED_ROUTE_1_5_15_40_80M";
        public string targetName;
        public string sourceMaterialShader;
        public string diagnosticShader;
        public int width;
        public int height;
        public string[] channels = HeroCliffCaptureRoute.ChannelNames;
        public string compositionProtection = "Target remains centered; 1/5m inspect contact detail and 15/40/80m protect cliff silhouette.";
        public List<HeroCliffCaptureFrame> frames = new();
    }

    /// <summary>Built-in pipeline fixed-route evidence capture. Capture is never a visual or production gate pass.</summary>
    public sealed class HeroCliffCaptureRoute : MonoBehaviour
    {
        public const string Captured = "CAPTURED";
        public const string NotReviewed = "NOT_REVIEWED";
        public const string GateNotRun = "GATE_NOT_RUN";
        public static readonly float[] Distances = { 1, 5, 15, 40, 80 };
        public static readonly string[] ChannelNames =
            { "FinalColor", "Depth", "WorldNormal", "Roughness", "AO", "Wetness", "MaterialID", "LODColor" };

        public Camera captureCamera;
        public HeroCliffP11Metadata target;
        public Shader diagnosticShader;
        public int width = 1920;
        public int height = 1080;
        public KeyCode captureKey = KeyCode.F10;
        bool running;

        void Update()
        {
            if (!running && UnityEngine.Input.GetKeyDown(captureKey) && target != null)
                StartCoroutine(CaptureToDirectory(Path.Combine(Application.persistentDataPath, "hero-cliff-p11", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")), null, Debug.LogError));
        }

        [ContextMenu("Capture P11 Fixed Route")]
        public void CaptureFromContext()
        {
            if (!running && target != null)
                StartCoroutine(CaptureToDirectory(Path.Combine(Application.persistentDataPath, "hero-cliff-p11", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")), null, Debug.LogError));
        }

        public static bool ValidateContract(Shader shader, HeroCliffP11Metadata metadata, out string report)
        {
            var errors = new List<string>();
            if (!Distances.SequenceEqual(new[] { 1f, 5f, 15f, 40f, 80f })) errors.Add("fixed distances invalid");
            if (!ChannelNames.SequenceEqual(new[] { "FinalColor", "Depth", "WorldNormal", "Roughness", "AO", "Wetness", "MaterialID", "LODColor" })) errors.Add("diagnostic channels invalid");
            if (ChannelNames.Select(DiagnosticMode).Distinct().Count() != ChannelNames.Length) errors.Add("diagnostic modes are not unique");
            if (shader == null || shader.name != "Hidden/AeroSim/HeroCliffDiagnostics" || !shader.isSupported) errors.Add("diagnostic replacement shader unavailable");
            if (metadata != null)
            {
                var materials = metadata.GetComponentsInChildren<Renderer>(true).SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().ToArray();
                if (materials.Length != 1) errors.Add("target must use one shared material");
                else
                {
                    var material = materials[0];
                    foreach (string property in new[] { "_BaseColor", "_RoughnessMap", "_AOMap", "_WetCavityMask" })
                        if (!material.HasProperty(property) || material.GetTexture(property) == null) errors.Add("missing material texture " + property);
                }
            }
            report = errors.Count == 0 ? "P11_CAPTURE_CONTRACT_OK" : string.Join("; ", errors);
            return errors.Count == 0;
        }

        public IEnumerator CaptureToDirectory(string outputDirectory, Action<HeroCliffCapturePackage> completed, Action<string> failed)
        {
            if (running) { failed?.Invoke("P11 capture already running."); yield break; }
            running = true;
            Camera source = captureCamera != null ? captureCamera : Camera.main;
            Shader replacement = diagnosticShader != null ? diagnosticShader : CoastProductionRuntimeCatalog.Load()?.p11DiagnosticShader;
            if (replacement == null) replacement = Shader.Find("Hidden/AeroSim/HeroCliffDiagnostics");
            string contract = "capture contract not evaluated";
            if (source == null || target == null || !ValidateContract(replacement, target, out contract))
            {
                running = false; failed?.Invoke("P11 capture setup rejected: " + (source == null ? "camera missing" : target == null ? "target missing" : contract)); yield break;
            }

            Directory.CreateDirectory(outputDirectory);
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Vector3 anchor = bounds.center + Vector3.up * (bounds.extents.y * 0.05f);
            // Runtime evidence is authoritative after FBX import: capture from +local X,
            // where the imported LOD presents its complete front-facing surface.
            Vector3 viewDirection = target.transform.right.normalized;
            float surfaceOffset = Mathf.Max(bounds.extents.x, 1f);
            string materialShader = renderers.SelectMany(value => value.sharedMaterials).First(value => value != null).shader.name;
            var package = new HeroCliffCapturePackage
            {
                generatedUtc = DateTime.UtcNow.ToString("O"), targetName = target.name,
                sourceMaterialShader = materialShader, diagnosticShader = replacement.name,
                width = width, height = height
            };

            var cameraHost = new GameObject("P11 CLI Capture Camera");
            var camera = cameraHost.AddComponent<Camera>();
            camera.CopyFrom(source); camera.enabled = false;
            var targetTransforms = target.GetComponentsInChildren<Transform>(true);
            var originalLayers = targetTransforms.ToDictionary(value => value.gameObject, value => value.gameObject.layer);
            foreach (var targetTransform in targetTransforms) targetTransform.gameObject.layer = 31;
            camera.cullingMask = 1 << 31;
            var keyLight = cameraHost.AddComponent<Light>();
            keyLight.type = LightType.Directional; keyLight.intensity = 1.25f; keyLight.color = new Color(1f, 0.96f, 0.9f); keyLight.cullingMask = 1 << 31;
            AmbientMode originalAmbientMode = RenderSettings.ambientMode;
            Color originalAmbientLight = RenderSettings.ambientLight;
            float originalAmbientIntensity = RenderSettings.ambientIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.68f);
            RenderSettings.ambientIntensity = 1f;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "P11 CLI capture", antiAliasing = 1 };
            rt.Create(); camera.targetTexture = rt;
            bool valid = true; string failure = null;
            var forcedLodGroup = target.GetComponent<LODGroup>();

            for (int view = 0; view < Distances.Length && valid; view++)
            {
                float distance = Distances[view];
                if (forcedLodGroup != null) forcedLodGroup.ForceLOD(distance <= 5 ? 0 : distance <= 15 ? 1 : distance <= 40 ? 2 : 3);
                camera.transform.position = anchor + viewDirection * (surfaceOffset + distance) + Vector3.up * Mathf.Lerp(0.15f, bounds.extents.y * 0.08f, distance / 80f);
                camera.transform.rotation = Quaternion.LookRotation((anchor - camera.transform.position).normalized, Vector3.up);
                camera.fieldOfView = distance <= 5 ? 50 : distance >= 40 ? 60 : 55;
                yield return new WaitForSecondsRealtime(0.6f);
                yield return new WaitForEndOfFrame();
                string finalHash = null;
                var viewHashes = new HashSet<string>(StringComparer.Ordinal);
                foreach (string channel in ChannelNames)
                {
                    float mode = DiagnosticMode(channel);
                    Shader.SetGlobalFloat("_HeroCliffDiagnosticMode", mode);
                    string renderPath;
                    if (channel == "FinalColor") { camera.ResetReplacementShader(); camera.Render(); renderPath = "camera.Render/final-material"; }
                    else { camera.RenderWithShader(replacement, "RenderType"); renderPath = "camera.RenderWithShader/" + replacement.name + "/mode-" + mode.ToString("0"); }
                    string file = "P11_" + distance.ToString("00") + "m_" + channel + ".png";
                    string hash = Write(rt, Path.Combine(outputDirectory, file));
                    if (channel == "FinalColor") finalHash = hash;
                    bool distinct = channel == "FinalColor" || hash != finalHash;
                    viewHashes.Add(hash);
                    package.frames.Add(new HeroCliffCaptureFrame
                    {
                        file = file, channel = channel, viewId = distance.ToString("0") + "m", distanceMeters = distance,
                        camera = camera.transform.position, target = anchor, verticalFov = camera.fieldOfView,
                        sha256 = hash, renderPath = renderPath, diagnosticMode = mode, distinctFromFinalColor = distinct
                    });
                    if (!distinct) { valid = false; failure = channel + " duplicated FinalColor at " + distance + "m"; break; }
                    yield return null;
                }
                if (valid && viewHashes.Count != ChannelNames.Length)
                {
                    valid = false; failure = "diagnostic channels did not produce eight unique images at " + distance + "m";
                }
            }

            if (forcedLodGroup != null) forcedLodGroup.ForceLOD(-1);
            foreach (var originalLayer in originalLayers) originalLayer.Key.layer = originalLayer.Value;
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbientLight;
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            camera.targetTexture = null; rt.Release(); Destroy(rt); Destroy(cameraHost);
            Shader.SetGlobalFloat("_HeroCliffDiagnosticMode", 0);
            running = false;
            if (!valid) { failed?.Invoke("P11 capture verification failed: " + failure); yield break; }
            if (package.frames.Count != Distances.Length * ChannelNames.Length)
            { failed?.Invoke("P11 capture frame count invalid: " + package.frames.Count); yield break; }
            File.WriteAllText(Path.Combine(outputDirectory, "manifest.json"), JsonUtility.ToJson(package, true));
            completed?.Invoke(package);
        }

        public static float DiagnosticMode(string channel)
        {
            switch (channel)
            {
                case "FinalColor": return 0;
                case "Depth": return 1;
                case "WorldNormal": return 2;
                case "Roughness": return 3;
                case "AO": return 4;
                case "Wetness": return 5;
                case "MaterialID": return 6;
                case "LODColor": return 7;
                default: return -1;
            }
        }

        static string Write(RenderTexture source, string path)
        {
            var old = RenderTexture.active; RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); texture.Apply(false, false);
            byte[] bytes = texture.EncodeToPNG(); File.WriteAllBytes(path, bytes);
            Destroy(texture); RenderTexture.active = old;
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }
    }
}
