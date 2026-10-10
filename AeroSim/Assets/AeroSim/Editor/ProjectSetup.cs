using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace AeroSim.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("AeroSim/Configure Project")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/AeroSim/Scenes");
            if (!File.Exists("Assets/AeroSim/Scenes/Main.unity"))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, "Assets/AeroSim/Scenes/Main.unity");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/AeroSim/Scenes/Main.unity", true)
            };

            PlayerSettings.companyName = "AeroSim Labs";
            PlayerSettings.productName = "AeroSim";
            PlayerSettings.bundleVersion = "0.4.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.aerosim.training");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.aerosim.training");
            PlayerSettings.defaultScreenWidth = 1680;
            PlayerSettings.defaultScreenHeight = 945;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            // New Input System is required by UI Toolkit events and all flight adapters.
            var settingsAsset = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settingsAsset.FindProperty("activeInputHandler").intValue = 1;
            settingsAsset.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard);

            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            AssetDatabase.SaveAssets();
            Debug.Log("AeroSim project configured successfully.");
        }

        public static void ConfigureFromCommandLine()
        {
            Configure();
        }

        public static void BuildMacPreview()
        {
            Configure();
            Directory.CreateDirectory("Builds/macOS");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/AeroSim/Scenes/Main.unity" },
                locationPathName = "Builds/macOS/AeroSim.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Build failed: {report.summary.result}");
            }
        }

        [MenuItem("AeroSim/Build Art V2 Preview")]
        public static void BuildArtPreview()
        {
            Configure(); AuditUiAssets();
            foreach(var texture in new[]{"meadow-grass-v2","coastal-rock-v2","aircraft-shell-v2","evergreen-needles-v2"})
            {
                var path="Assets/AeroSim/Resources/Art/MaterialsV2/"+texture+".png";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                if(AssetDatabase.LoadAssetAtPath<Texture2D>(path)==null) throw new BuildFailedException("Art texture missing: "+path);
            }
            Directory.CreateDirectory("Builds/macOS-ArtV2");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/AeroSim/Scenes/Main.unity"},locationPathName="Builds/macOS-ArtV2/AeroSim.app",
                target=BuildTarget.StandaloneOSX,options=BuildOptions.None
            });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new BuildFailedException("Art V2 build failed: "+report.summary.result);
        }

        [MenuItem("AeroSim/Build Aircraft V3 Preview")]
        public static void BuildAircraftPreview()
        {
            Configure(); AuditUiAssets();
            foreach(var texture in new[]{"mini-shell-v3","air-shell-v3","mavic-shell-v3","avata-shell-v3","carbon-weave-v3"})
            {
                var path="Assets/AeroSim/Resources/Art/AircraftV3/"+texture+".png";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                if(AssetDatabase.LoadAssetAtPath<Texture2D>(path)==null)throw new BuildFailedException("Aircraft texture missing: "+path);
            }
            Directory.CreateDirectory("Builds/macOS-AircraftV3");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/AeroSim/Scenes/Main.unity"},locationPathName="Builds/macOS-AircraftV3/AeroSim.app",
                target=BuildTarget.StandaloneOSX,options=BuildOptions.None
            });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new BuildFailedException("Aircraft V3 build failed: "+report.summary.result);
        }

        public static void AuditUiAssets()
        {
            foreach(var file in new[]{"AeroSim", "Product", "Responsive"})
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/AeroSim/Resources/UI/"+file+".uss");
                if (sheet == null) throw new BuildFailedException(file+".uss could not be loaded.");
                var serialized = new SerializedObject(sheet);
                var rules = serialized.FindProperty("m_Rules");
                if(rules==null || rules.arraySize==0) throw new BuildFailedException(file+".uss has no imported rules.");
                Debug.Log($"AeroSim stylesheet audit: {file}, rules={rules.arraySize}");
            }
        }

        // Build candidates separately; never overwrite the user's last verified 0.4 during QA.
        public static void BuildUiCandidate()
        {
            Configure(); AuditUiAssets();
            var destination = System.Environment.GetEnvironmentVariable("AEROSIM_UI_BUILD_PATH");
            if (string.IsNullOrEmpty(destination) || !destination.StartsWith("/private/tmp/aerosim-ui-build.") || !destination.EndsWith("/AeroSim.app"))
                throw new BuildFailedException("AEROSIM_UI_BUILD_PATH must point to a new isolated temporary AeroSim.app.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/AeroSim/Scenes/Main.unity" }, locationPathName = destination,
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException("UI candidate build failed: " + report.summary.result);
        }

        public static void BuildIosExport()
        {
            Configure();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new BuildFailedException("Unity iOS Build Support is unavailable.");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            Directory.CreateDirectory("Builds/iOS");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/AeroSim/Scenes/Main.unity" },
                locationPathName = "Builds/iOS", target = BuildTarget.iOS, options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException("iOS export failed: " + report.summary.result);
        }

        // Version field denotes a development target, never a visual acceptance claim.
        public static void BuildDesignV1Candidate()
        {
            Configure(); AuditUiAssets();
            // Assign persisted project-wide actions in Edit mode, not from a runtime
            // callback. UI Toolkit derives Submit from these actions in Unity 6.3.
            var uiActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/AeroSim/UIInput.inputactions");
            if (uiActions == null) throw new BuildFailedException("AeroSim UI input actions not imported.");
            UnityEngine.InputSystem.InputSystem.actions = uiActions;
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/AeroSim/Resources/UI/FlightDesignV1.uss");
            if (sheet == null) throw new BuildFailedException("FlightDesignV1.uss not imported.");
            var flightRules = new SerializedObject(sheet).FindProperty("m_Rules");
            if (flightRules == null || flightRules.arraySize == 0) throw new BuildFailedException("FlightDesignV1.uss has no imported rules.");
            var destination = System.Environment.GetEnvironmentVariable("AEROSIM_V1_BUILD_PATH");
            if (string.IsNullOrEmpty(destination) || !destination.StartsWith("/private/tmp/aerosim-v1-build.") || !destination.EndsWith("/AeroSim.app"))
                throw new BuildFailedException("AEROSIM_V1_BUILD_PATH must be a new isolated temporary AeroSim.app.");
            PlayerSettings.bundleVersion = "1.0.0"; AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/AeroSim/Scenes/Main.unity" }, locationPathName = destination,
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException("Design 1.0 candidate build failed: " + report.summary.result);
        }

        public static void BuildAllPlatforms()
        {
            AuditUiAssets();
            BuildMacPreview();
            BuildIosExport();
        }
    }
}
