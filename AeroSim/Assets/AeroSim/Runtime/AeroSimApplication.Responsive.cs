using System;
using System.Collections.Generic;
using System.Globalization;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        private readonly Dictionary<TextElement, float> _baseFonts = new();
        private bool _typographyScheduled;
        private int _lastWindowWidth, _lastWindowHeight;
        private readonly Dictionary<VisualElement, StyleLength> _designColumnWidths = new();

        private void SaveUiPreferences() { LocalStore.Save(); RefreshUiPreferences(); }

        // Public entry also lets the QA harness exercise actual product preferences.
        public void RefreshUiPreferences()
        {
            if (_document == null || LocalStore.Profile == null) return;
            var settings = LocalStore.Profile.settings;
            bool flight = _page == Page.Flight || _page == Page.Replay;
            string preference = flight ? settings.hudScale : settings.uiScale;
            float scale = ParseScale(preference);
            _document.panelSettings.scale = scale;
            _lastWindowWidth = Screen.width; _lastWindowHeight = Screen.height;
            OnGeometryChanged(null);
            _root.schedule.Execute(() => OnGeometryChanged(null));
        }

        private static float ParseScale(string preference)
        {
            float device = Application.isMobilePlatform && Screen.dpi > 0 ? Mathf.Clamp(Screen.dpi / 163f, 1, 4) : 1;
            if (string.IsNullOrEmpty(preference) || preference == "Auto")
                return device * (Application.isMobilePlatform ? 1 : Screen.width >= 3200 ? 1.5f : Screen.width >= 2400 ? 1.25f : 1);
            if (float.TryParse(preference.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent))
                return device * Mathf.Clamp(percent / 100f, .8f, 3);
            return device;
        }

        private void ApplyTypography()
        {
            if (_typographyScheduled) return;
            _typographyScheduled = true;
            _root.schedule.Execute(() => {
                _typographyScheduled = false;
                // Release removed page elements without losing bases on a live font change.
                var stale = new List<TextElement>();
                foreach (var pair in _baseFonts) if (pair.Key.panel == null) stale.Add(pair.Key);
                foreach (var item in stale) _baseFonts.Remove(item);
                float multiplier = Mathf.Clamp(LocalStore.Profile.settings.textScale, 1, 2);
                foreach (var element in _root.Query<TextElement>().ToList())
                {
                    if (element.ClassListContains("nav-icon") || element.ClassListContains("brand-mark-glyph") ||
                        element.ClassListContains("device-icon-glyph") || element.ClassListContains("workspace-icon") ||
                        element.ClassListContains("workspace-chevron") || element.ClassListContains("brand-name") ||
                        element.ClassListContains("row-icon") || element.ClassListContains("row-chevron")) continue;
                    if (!_baseFonts.TryGetValue(element, out float font))
                    {
                        font = element.resolvedStyle.fontSize;
                        if (float.IsNaN(font) || font <= 0) continue;
                        font = Mathf.Max(12, font); _baseFonts[element] = font;
                    }
                    if (Mathf.Abs(element.resolvedStyle.fontSize - font * multiplier) > .1f)
                        element.style.fontSize = font * multiplier;
                }
            }).ExecuteLater(30); // Read USS font tokens after the first style/layout pass, not default 14px.
        }

        private void LayoutDesignPages(float width, float height)
        {
            if (!_root.ClassListContains("design-v1")) return;
            bool stack = width < 1280 || height < 500 || LocalStore.Profile.settings.textScale > 1.1f;
            var stale = new List<VisualElement>();
            foreach (var entry in _designColumnWidths) if (entry.Key.panel == null) stale.Add(entry.Key);
            foreach (var element in stale) _designColumnWidths.Remove(element);
            foreach (var columns in _root.Query<VisualElement>(className: "design-columns").ToList())
            {
                columns.style.flexDirection = stack ? FlexDirection.Column : FlexDirection.Row;
                foreach (var child in columns.Children())
                {
                    if (!_designColumnWidths.ContainsKey(child)) _designColumnWidths[child] = child.style.width;
                    child.style.width = stack ? UnityEngine.UIElements.Length.Percent(100) : _designColumnWidths[child];
                    child.style.flexBasis = stack ? new StyleLength(StyleKeyword.Auto) : new StyleLength(StyleKeyword.Null);
                    child.style.flexGrow = stack ? new StyleFloat(0f) : new StyleFloat(StyleKeyword.Null);
                    child.style.minWidth = 0;
                }
            }
        }

        private void LayoutFlightHud(float width, float height)
        {
            if (_root.ClassListContains("v1-flight-hud") || _root.ClassListContains("v1-replay"))
            {
                LayoutDesignFlightHud(width, height);
                return;
            }
            var settings = LocalStore.Profile.settings;
            float text = Mathf.Clamp(settings.textScale, 1, 2);
            bool compact = width < 1280 || height < 620 || text > 1.1f;
            _root.EnableInClassList("hud-compact", compact);
            bool touch = Application.isMobilePlatform || width < 760 || height < 500 || settings.showTouchControls;
            _root.EnableInClassList("hud-touch", touch && _page != Page.Replay);
            var occupied = new List<Rect>();
            var center = new Rect(width * .3f, height * .31f, width * .4f, height * .38f);
            var objective = _root.Q<VisualElement>(className:"objective-card");
            if (!compact && objective != null)
            {
                float cardWidth = Mathf.Min(400, width * .26f - 24);
                objective.style.width = cardWidth;
                occupied.Add(new Rect(24, 128, cardWidth, 200));
                occupied.Add(new Rect(24, height - 252, 240, 180));
                occupied.Add(new Rect(width * .32f, 128, width * .36f, 48));
            }
            if (touch)
            {
                occupied.Add(new Rect(24, height - 184, 100, 116));
                occupied.Add(new Rect(width - 124, height - 184, 100, 116));
            }
            int columns = width < 760 ? 2 : 4;
            float chipWidth = Mathf.Min(144 * text, (width - 48 - (columns - 1) * 8) / columns);
            float chipHeight = text > 1.1f ? 100 : 56;
            var labels = new[] { _speed, _altitude, _timer, _battery };
            var preferences = new[] { settings.speedPosition, settings.altitudePosition, settings.timerPosition, settings.batteryPosition };
            for (int i = 0; i < labels.Length; i++)
            {
                var metric = labels[i]?.parent; if (metric == null || metric.resolvedStyle.display == DisplayStyle.None) continue;
                var preferred = new Rect(preferences[i].x * width, preferences[i].y * height, chipWidth, chipHeight);
                bool Fits(Rect r) {
                    if (r.xMin < 24 || r.xMax > width - 24 || r.yMin < 64 || r.yMax > height - 64 || r.Overlaps(center)) return false;
                    foreach (var other in occupied) if (r.Overlaps(other)) return false;
                    // Reserve fallback slots so a custom anchor cannot collide with a later chip.
                    for (int j = 0; j < labels.Length; j++)
                        if (j != i && r.Overlaps(new Rect(24 + (j % columns) * (chipWidth + 8), 64 + (j / columns) * (chipHeight + 8), chipWidth, chipHeight))) return false;
                    return true;
                }
                Rect fallback = new Rect(24 + (i % columns) * (chipWidth + 8), 64 + (i / columns) * (chipHeight + 8), chipWidth, chipHeight);
                Rect placement = !compact && Fits(preferred) ? preferred : fallback;
                occupied.Add(placement);
                metric.style.left = placement.x; metric.style.top = placement.y;
                metric.style.width = chipWidth; metric.style.height = chipHeight;
            }
        }

        private static void LayoutOsdPreview(VisualElement preview)
        {
            float width = preview.contentRect.width;
            if (float.IsNaN(width) || width < 32) return;
            var tokens = preview.Query<Label>(className: "osd-token").ToList();
            var rectangles = new List<Rect>();
            bool stack = false;
            float tallest = 44;
            foreach (var token in tokens)
            {
                token.style.maxWidth = width - 24;
                float w = Mathf.Clamp(token.layout.width, 1, width - 24);
                float h = Mathf.Max(44, token.layout.height);
                if (float.IsNaN(w) || float.IsNaN(h)) return;
                tallest = Mathf.Max(tallest, h);
                var anchor = token.userData is Vector2 v ? v : Vector2.zero;
                var r = new Rect(Mathf.Clamp(anchor.x * width, 12, Mathf.Max(12, width-w-12)),
                    Mathf.Clamp(anchor.y * 360, 12, Mathf.Max(12, 360-h-12)), w, h);
                foreach (var other in rectangles) if (r.Overlaps(other)) stack = true;
                rectangles.Add(r);
            }
            // Preserve valid anchors. On narrow previews, unsafe overlaps become a readable stack.
            // Do not overwrite saved anchors merely because the window changed size.
            float required = stack ? 24 + tokens.Count * (tallest + 8) : 360;
            preview.style.height = Mathf.Max(360, required);
            for (int i = 0; i < tokens.Count; i++)
            {
                tokens[i].style.left = stack ? 12 : rectangles[i].x;
                tokens[i].style.top = stack ? 12 + i * (tallest + 8) : rectangles[i].y;
            }
        }
    }
}
