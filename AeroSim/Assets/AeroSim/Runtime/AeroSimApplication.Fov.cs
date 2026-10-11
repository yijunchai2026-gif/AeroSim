using System;
using System.Collections.Generic;
using System.Linq;
using AeroSim.Domain;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        private Dictionary<string, float> _fovSnapshot;
        private Dictionary<string, float> _fovDraft;
        private Dictionary<string, float> _fovUndoSnapshot;
        private float _fovUndoExpiresAt;
        private readonly Dictionary<string, string> _fovDraftSource = new();
        private string _fovOpeningViewId;
        internal static Func<bool> FovXrActiveProbe = () => XRSettings.enabled && XRSettings.isDeviceActive;
        internal static bool IsFovXrControlled => FovXrActiveProbe?.Invoke() == true;

        private void BeginFovEditing()
        {
            if (_fovDraft != null) return;
            _fovSnapshot = FovViewDefinitions.All.ToDictionary(definition => definition.viewId, definition => LocalStore.GetVerticalFov(definition.viewId));
            _fovDraft = new Dictionary<string, float>(_fovSnapshot);
            _fovDraftSource.Clear();
            foreach (var definition in FovViewDefinitions.All) _fovDraftSource[definition.viewId] = "custom";
            _fovOpeningViewId = _sim?.ActiveFovViewId ?? "near-follow";
        }

        private VisualElement BuildFovSettingsPanel(bool inFlight)
        {
            BeginFovEditing();
            bool xrControlled = IsFovXrControlled;
            var panel = Panel("Camera & Comfort", "Vertical FOV is the saved source of truth. Horizontal FOV is derived live from the current window aspect ratio.");
            panel.AddToClassList("fov-settings-panel");
            if (xrControlled) panel.Add(Text("FOV is controlled by the active XR headset runtime. Desktop FOV controls are disabled until XR is inactive.", "warning-text", "wrap", "fov-xr-reason"));
            if (LocalStore.FovDiagnostics.Count > 0) panel.Add(Text("Runtime diagnostic · " + LocalStore.FovDiagnostics[LocalStore.FovDiagnostics.Count - 1], "muted", "wrap"));
            var derivedLabels = new Dictionary<string, Label>();
            var valueLabels = new Dictionary<string, Label>();
            var sliders = new Dictionary<string, Slider>();

            void Refresh(string viewId)
            {
                if (_fovDraft == null || !derivedLabels.TryGetValue(viewId, out var derived)) return;
                float value = _fovDraft[viewId];
                float aspect = FovPreviewAspect();
                float horizontal = FlightSimulator.VerticalToHorizontalFov(value, aspect);
                valueLabels[viewId].text = value.ToString("F1") + "° vFOV";
                string warning = horizontal > 150 ? " · Use a multi-projection setup for this very wide view" : horizontal > 140 ? " · Strong edge-distortion warning" : value > 80 ? " · Wider view may increase edge distortion and speed sensation" : value < 45 ? " · Narrow view may limit peripheral awareness" : "";
                derived.text = "Derived hFOV  " + horizontal.ToString("F1") + "°  ·  Aspect " + aspect.ToString("F2") + ":1" + warning;
                derived.EnableInClassList("warning-text", horizontal > 140 || value > 80 || value < 45);
            }

            void SetDraft(string viewId, float value, string source)
            {
                var definition = LocalStore.Definition(viewId);
                if (float.IsNaN(value) || float.IsInfinity(value)) value = definition.defaultDeg;
                value = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, definition.minimumDeg, definition.maximumDeg);
                _fovDraft[viewId] = value;
                _fovDraftSource[viewId] = source;
                sliders[viewId].SetValueWithoutNotify(value);
                Refresh(viewId);
                if (!xrControlled && _sim != null && _sim.View != null && _sim.ActiveFovViewId == viewId) _sim.PreviewVerticalFov(value);
            }

            foreach (var definition in FovViewDefinitions.All)
            {
                string viewId = definition.viewId;
                var section = Element("fov-view-section");
                var heading = Element("product-row");
                heading.Add(Text(definition.displayName, "panel-title"));
                valueLabels[viewId] = Text("", "fov-value"); heading.Add(valueLabels[viewId]); section.Add(heading);
                var slider = new Slider("Vertical FOV (" + definition.minimumDeg.ToString("F0") + "°–" + definition.maximumDeg.ToString("F0") + "°)", definition.minimumDeg, definition.maximumDeg)
                {
                    value = _fovDraft[viewId], showInputField = true
                };
                slider.AddToClassList("product-field"); slider.AddToClassList("design-range-field"); sliders[viewId] = slider;
                slider.RegisterValueChangedCallback(evt => SetDraft(viewId, evt.newValue, "custom")); section.Add(slider);
                var presets = Element("product-row");
                string[] presetNames = { "Narrow", "Balanced", "Wide" };
                for (int i = 0; i < definition.presetsDeg.Length; i++)
                {
                    float preset = definition.presetsDeg[i];
                    string presetName = i < presetNames.Length ? presetNames[i] : "Preset " + (i + 1);
                    presets.Add(ActionButton(presetName + " " + preset.ToString("F0") + "°", () => SetDraft(viewId, preset, "preset")));
                }
                if (inFlight) presets.Add(ActionButton("Preview This View", () => _sim.PreviewCameraView(viewId, _fovDraft[viewId])));
                section.Add(presets);
                derivedLabels[viewId] = Text("", "muted", "wrap", "fov-derived"); section.Add(derivedLabels[viewId]);
                section.Add(ActionButton("Reset " + definition.displayName, () => SetDraft(viewId, definition.defaultDeg, "default")));
                section.SetEnabled(!xrControlled);
                panel.Add(section); Refresh(viewId);
            }
            panel.RegisterCallback<GeometryChangedEvent>(_ => { foreach (var definition in FovViewDefinitions.All) Refresh(definition.viewId); });

            var resetConfirm = Element("product-row", "is-hidden");
            resetConfirm.Add(Text("Reset all six views to their calibrated defaults?", "wrap"));
            resetConfirm.Add(ActionButton("Confirm Reset All", () => { foreach (var definition in FovViewDefinitions.All) SetDraft(definition.viewId, definition.defaultDeg, "default"); resetConfirm.AddToClassList("is-hidden"); }));
            resetConfirm.Add(ActionButton("Keep Values", () => resetConfirm.AddToClassList("is-hidden")));
            panel.Add(resetConfirm);
            var actions = Element("product-row");
            var resetAll = ActionButton("Reset All…", () => resetConfirm.RemoveFromClassList("is-hidden"));
            var apply = ActionButton("Apply FOV", () => { ApplyFovEditing(); if (!inFlight) ShowPage(Page.Settings); }, true);
            resetAll.SetEnabled(!xrControlled); apply.SetEnabled(!xrControlled);
            actions.Add(resetAll); actions.Add(apply);
            actions.Add(ActionButton("Cancel FOV Changes", () => { CancelFovEditing(); if (inFlight) _flightDialogClose?.Invoke(); else ShowPage(Page.Settings); }));
            panel.Add(actions);
            panel.Add(Text("Each view uses its own hard range and presets. Presets only update the preview; Apply writes all six isolated values to this device.", "muted", "wrap"));
            return panel;
        }

        private float FovPreviewAspect()
        {
            if (_sim?.View != null && _sim.View.aspect > 0) return _sim.View.aspect;
            return Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
        }

        private void ApplyFovEditing()
        {
            if (_fovDraft == null) return;
            if (IsFovXrControlled)
            {
                LocalStore.RecordFovDiagnostic("FOV Apply was blocked because the active XR runtime owns projection.");
                ShowToast("FOV is controlled by the active XR headset runtime.");
                return;
            }
            float aspect = FovPreviewAspect();
            _fovUndoSnapshot = _fovSnapshot == null ? null : new Dictionary<string, float>(_fovSnapshot);
            foreach (var definition in FovViewDefinitions.All)
                LocalStore.SetVerticalFov(definition.viewId, _fovDraft[definition.viewId], _fovDraftSource[definition.viewId], aspect);
            LocalStore.Save();
            _fovUndoExpiresAt = Time.realtimeSinceStartup + 10f;
            LocalStore.RecordFovDiagnostic("Applied six-view FOV profile; Undo is available for 10 seconds.");
            _sim?.ApplyConfiguredFieldOfView();
            ClearFovEditing();
            ShowFovUndoToast();
            if (_page == Page.Flight && _flightDialogClose != null) _flightDialogClose();
        }

        internal bool UndoLastFovApply()
        {
            if (_fovUndoSnapshot == null || Time.realtimeSinceStartup > _fovUndoExpiresAt) return false;
            float aspect = FovPreviewAspect();
            foreach (var definition in FovViewDefinitions.All)
                LocalStore.SetVerticalFov(definition.viewId, _fovUndoSnapshot[definition.viewId], "custom", aspect);
            LocalStore.Save();
            _fovUndoSnapshot = null;
            _fovUndoExpiresAt = 0;
            if (!IsFovXrControlled) _sim?.ApplyConfiguredFieldOfView();
            _root?.Q<VisualElement>("fov-undo-toast")?.RemoveFromHierarchy();
            LocalStore.RecordFovDiagnostic("Undid the most recent FOV Apply.");
            ShowToast("Previous camera FOV values restored.");
            return true;
        }

        private void ShowFovUndoToast()
        {
            if (_root == null || _fovUndoSnapshot == null) return;
            _root.Q<VisualElement>("fov-undo-toast")?.RemoveFromHierarchy();
            var toast = Element("toast", "fov-undo-toast"); toast.name = "fov-undo-toast";
            toast.Add(Text("Camera FOV settings applied.", "toast-label"));
            toast.Add(ActionButton("Undo", () => UndoLastFovApply()));
            _root.Add(toast);
            toast.schedule.Execute(() => {
                if (toast.panel != null) toast.RemoveFromHierarchy();
                if (Time.realtimeSinceStartup >= _fovUndoExpiresAt) _fovUndoSnapshot = null;
            }).ExecuteLater(10000);
        }

        internal void DiagnosticBeginFovEdit() => BeginFovEditing();
        internal VisualElement DiagnosticBuildFovPanel() => BuildFovSettingsPanel(false);
        internal void DiagnosticSetFovDraft(string viewId, float value)
        {
            if (_fovDraft == null) BeginFovEditing();
            var definition = LocalStore.Definition(viewId);
            _fovDraft[definition.viewId] = Mathf.Clamp(value, definition.minimumDeg, definition.maximumDeg);
            _fovDraftSource[definition.viewId] = "custom";
        }
        internal void DiagnosticApplyFovEdit() => ApplyFovEditing();
        internal void DiagnosticCancelFovEdit() => CancelFovEditing();
        internal void DiagnosticExpireFovUndo() => _fovUndoExpiresAt = Time.realtimeSinceStartup - 1f;

        private void CancelFovEditing()
        {
            if (_fovDraft == null) return;
            LocalStore.RecordFovDiagnostic("Cancelled FOV draft and restored its opening snapshot.");
            if (_sim != null && _sim.View != null)
            {
                _sim.SetCameraView(_fovOpeningViewId);
                if (_fovSnapshot != null) _sim.PreviewVerticalFov(_fovSnapshot[_sim.ActiveFovViewId]);
                else _sim.ApplyConfiguredFieldOfView();
            }
            ClearFovEditing();
        }

        private void ClearFovEditing()
        {
            _fovDraft = null;
            _fovSnapshot = null;
            _fovDraftSource.Clear();
        }

        private void ShowFlightFovSettings()
        {
            if (_page != Page.Flight || _sim == null) return;
            _flightDialogClose?.Invoke();
            bool pauseForReading = !_sim.Paused;
            if (pauseForReading) _sim.Pause(true);
            DismissToast();
            _flightInputInterrupt = "Camera settings open · touch again after closing";
            ReleaseTouchControls("Paused");
            var overlay = Element("modal-overlay");
            var panel = BuildFovSettingsPanel(true);
            void Close(bool cancel)
            {
                if (cancel) CancelFovEditing();
                overlay.RemoveFromHierarchy(); _flightDialogClose = null;
                if (pauseForReading) _sim?.Pause(false);
                RestoreFlightDialogFocus();
            }
            _flightDialogClose = () => Close(true);
            panel.Add(ActionButton("Back to Flight", () => Close(true)));
            overlay.Add(ModalScroll(panel)); InstallDesignModalFocus(overlay); _root.Add(overlay);
            ApplyTypography(overlay);
            overlay.Query<Slider>().First()?.Focus();
        }
    }
}
