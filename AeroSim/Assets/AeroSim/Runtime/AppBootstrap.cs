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
        }
    }
}
