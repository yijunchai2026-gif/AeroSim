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
            PlayerSettings.bundleVersion = "0.3.0";
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

        public static void AuditUiAssets()
        {
            foreach(var file in new[]{"AeroSim", "Product"})
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/AeroSim/Resources/UI/"+file+".uss");
                if (sheet == null) throw new BuildFailedException(file+".uss could not be loaded.");
                var serialized = new SerializedObject(sheet);
                var rules = serialized.FindProperty("m_Rules");
                if(rules==null || rules.arraySize==0) throw new BuildFailedException(file+".uss has no imported rules.");
                Debug.Log($"AeroSim stylesheet audit: {file}, rules={rules.arraySize}");
            }
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

        public static void BuildAllPlatforms()
        {
            AuditUiAssets();
            BuildMacPreview();
            BuildIosExport();
        }
    }
}
