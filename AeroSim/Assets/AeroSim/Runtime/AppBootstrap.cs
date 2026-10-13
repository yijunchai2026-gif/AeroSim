using AeroSim.Runtime.Mcp;
using AeroSim.Runtime.SceneEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AeroSim
{
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateApplication()
        {
            if (Object.FindFirstObjectByType<AeroSimApplication>() != null)
            {
                return;
            }

            var appObject = new GameObject("AeroSim Application");
            Object.DontDestroyOnLoad(appObject);

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "AeroSim Runtime Panel";
            // Layout uses logical points, rather than shrinking a desktop canvas onto a phone.
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = Application.isMobilePlatform && Screen.dpi > 0 ? Mathf.Clamp(Screen.dpi / 163f, 1, 4) : 1;
            panelSettings.sortingOrder = 10;
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/AeroSimTheme");

            var document = appObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            appObject.AddComponent<AeroSimApplication>();

            var sceneEditor = appObject.AddComponent<RuntimeSceneEditorService>();
            appObject.AddComponent<RuntimeMcpBridge>();
            CreateSceneEditorPanel(appObject.transform, sceneEditor, panelSettings.themeStyleSheet);
        }

        private static void CreateSceneEditorPanel(Transform parent, RuntimeSceneEditorService sceneEditor, ThemeStyleSheet theme)
        {
            var panelObject = new GameObject("AeroSim Runtime Scene Editor UI");
            panelObject.transform.SetParent(parent, false);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "AeroSim Runtime Scene Editor Panel";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = Application.isMobilePlatform && Screen.dpi > 0 ? Mathf.Clamp(Screen.dpi / 163f, 1, 4) : 1;
            settings.sortingOrder = 100;
            settings.themeStyleSheet = theme;
            var document = panelObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var panel = panelObject.AddComponent<RuntimeSceneEditorPanel>();
            panel.Initialize(sceneEditor, document);
        }
    }
}
