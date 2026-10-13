using System;
using System.Collections.Generic;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace AeroSim.Runtime.SceneEditor
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RuntimeSceneEditorPanel : MonoBehaviour
    {
        private RuntimeSceneEditorService service;
        private UIDocument document;
        private VisualElement panel;
        private VisualElement hierarchy;
        private Button toggle;
        private Button undo;
        private Button redo;
        private Button deleteSelected;
        private Label status;
        private FloatField px, py, pz, rx, ry, rz, sx, sy, sz;
        private bool refreshing;
        private RuntimeSelectionGizmo gizmo;
        private FlightSimulator simulator;
        private float nextSimulatorLookup;

        public void Initialize(RuntimeSceneEditorService editorService, UIDocument uiDocument)
        {
            service = editorService;
            document = uiDocument;
        }

        private void Start()
        {
            if (service == null || document == null)
            {
                enabled = false;
                return;
            }
            service.Changed += Refresh;
            simulator = FindFirstObjectByType<FlightSimulator>();
            nextSimulatorLookup = Time.unscaledTime + 1f;
            Build();
            gizmo = gameObject.AddComponent<RuntimeSelectionGizmo>();
            gizmo.Initialize(service);
            Refresh();
        }

        private void OnDestroy()
        {
            if (service != null) service.Changed -= Refresh;
        }

        private void Update()
        {
            if (toggle == null || service == null) return;
            if (simulator == null && Time.unscaledTime >= nextSimulatorLookup)
            {
                simulator = FindFirstObjectByType<FlightSimulator>();
                nextSimulatorLookup = Time.unscaledTime + 1f;
            }
            // If a flight is launched from the underlying application while the panel is open,
            // fail closed: rollback the sandbox before any normal flight input is accepted.
            if (service.IsSandboxActive && simulator != null && simulator.Running && !simulator.Paused)
                service.ExitSandbox(true);
            bool liveFlight = simulator != null && simulator.Running && !simulator.Paused && !service.IsSandboxActive;
            toggle.style.display = liveFlight ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void Build()
        {
            var root = document.rootVisualElement;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;

            toggle = Button("Scene Sandbox", () =>
            {
                if (service.IsSandboxActive) service.ExitSandbox(true);
                else service.EnterSandbox();
            });
            toggle.name = "runtime-scene-editor-toggle";
            toggle.style.position = Position.Absolute;
            toggle.style.right = 18;
            toggle.style.top = 18;
            toggle.style.height = 38;
            toggle.style.backgroundColor = new Color(.04f, .24f, .42f, .96f);
            toggle.style.color = Color.white;
            root.Add(toggle);

            panel = new VisualElement { name = "runtime-scene-editor-panel" };
            panel.style.position = Position.Absolute;
            panel.style.right = 18;
            panel.style.top = 64;
            panel.style.width = 360;
            panel.style.maxHeight = Length.Percent(88);
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;
            panel.style.backgroundColor = new Color(.025f, .045f, .07f, .97f);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = 8;
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 8;
            panel.style.color = Color.white;
            root.Add(panel);

            panel.Add(Label("RUNTIME SCENE EDITOR · SANDBOX", 15, FontStyle.Bold));
            status = Label("", 11, FontStyle.Normal);
            status.style.color = new Color(.45f, .82f, 1f);
            panel.Add(status);

            var history = Row();
            undo = Button("Undo", () => service.Undo());
            redo = Button("Redo", () => service.Redo());
            history.Add(undo); history.Add(redo);
            history.Add(Button("Clear drafts", () => service.ClearDrafts()));
            panel.Add(history);

            panel.Add(Label("SPAWN WHITELIST", 11, FontStyle.Bold));
            var spawn = Row();
            foreach (SandboxPrimitive primitive in Enum.GetValues(typeof(SandboxPrimitive)))
            {
                var captured = primitive;
                spawn.Add(Button(primitive == SandboxPrimitive.RockLike ? "Rock-like" : primitive.ToString(), () => service.Spawn(captured)));
            }
            spawn.style.flexWrap = Wrap.Wrap;
            panel.Add(spawn);

            panel.Add(Label("DRAFT HIERARCHY", 11, FontStyle.Bold));
            var hierarchyScroll = new ScrollView { style = { maxHeight = 150 } };
            hierarchy = hierarchyScroll.contentContainer;
            panel.Add(hierarchyScroll);

            panel.Add(Label("TRANSFORM", 11, FontStyle.Bold));
            px = Field("P X"); py = Field("P Y"); pz = Field("P Z");
            rx = Field("R X"); ry = Field("R Y"); rz = Field("R Z");
            sx = Field("S X"); sy = Field("S Y"); sz = Field("S Z");
            panel.Add(VectorRow(px, py, pz));
            panel.Add(VectorRow(rx, ry, rz));
            panel.Add(VectorRow(sx, sy, sz));
            panel.Add(Button("Apply Transform", ApplyTransform));
            deleteSelected = Button("Delete Selected Draft", DeleteSelected);
            panel.Add(deleteSelected);
            var warning = Label(RuntimeSceneObject.DraftStatus + "\nExit rolls back every sandbox command. Production scene objects are read-only.", 10, FontStyle.Normal);
            warning.style.whiteSpace = WhiteSpace.Normal;
            warning.style.color = new Color(1f, .74f, .3f);
            panel.Add(warning);
            panel.Add(Button("Exit & Roll Back", () => service.ExitSandbox(true)));
        }

        private void Refresh()
        {
            if (service == null || panel == null) return;
            refreshing = true;
            try
            {
                panel.style.display = service.IsSandboxActive ? DisplayStyle.Flex : DisplayStyle.None;
                toggle.text = service.IsSandboxActive ? "Close Sandbox" : "Scene Sandbox";
                status.text = service.IsSandboxActive
                    ? $"DRAFT ONLY · {service.Hierarchy().Count} object(s) · {RuntimeSceneObject.DraftStatus}"
                    : "Sandbox disabled";
                undo.SetEnabled(service.UndoCount > 0);
                redo.SetEnabled(service.RedoCount > 0);
                undo.text = service.UndoCount > 0 ? "Undo " + service.UndoLabel : "Undo";
                redo.text = service.RedoCount > 0 ? "Redo " + service.RedoLabel : "Redo";
                hierarchy.Clear();
                foreach (var info in service.Hierarchy())
                {
                    var captured = info.RuntimeId;
                    var selected = service.Selection.Active != null && service.Selection.Active.RuntimeId == captured;
                    var button = Button((selected ? "▶ " : "  ") + info.Name + "  [" + captured.Substring(captured.Length - 4) + "]", () => service.Select(captured));
                    button.style.unityTextAlign = TextAnchor.MiddleLeft;
                    hierarchy.Add(button);
                }
                var item = service.Selection.Active;
                bool hasSelection = item != null && item.gameObject.activeSelf;
                SetFieldsEnabled(hasSelection);
                deleteSelected.SetEnabled(hasSelection);
                if (hasSelection)
                {
                    var transform = item.transform;
                    SetVector(px, py, pz, transform.position);
                    SetVector(rx, ry, rz, transform.eulerAngles);
                    SetVector(sx, sy, sz, transform.localScale);
                }
            }
            finally { refreshing = false; }
        }

        private void DeleteSelected()
        {
            var selected = service?.Selection.Active;
            if (selected == null || !selected.gameObject.activeSelf) return;
            try { service.DeleteSelected(); }
            catch (Exception exception) { status.text = "Rejected: " + exception.Message; }
        }

        private void ApplyTransform()
        {
            if (refreshing || service.Selection.Active == null) return;
            try
            {
                service.SetSelectedTransform(
                    new Vector3(px.value, py.value, pz.value),
                    new Vector3(rx.value, ry.value, rz.value),
                    new Vector3(sx.value, sy.value, sz.value));
            }
            catch (Exception exception) { status.text = "Rejected: " + exception.Message; }
        }

        private void SetFieldsEnabled(bool value)
        {
            foreach (var field in new[] { px, py, pz, rx, ry, rz, sx, sy, sz }) field.SetEnabled(value);
        }

        private static void SetVector(FloatField x, FloatField y, FloatField z, Vector3 value)
        {
            x.SetValueWithoutNotify(value.x); y.SetValueWithoutNotify(value.y); z.SetValueWithoutNotify(value.z);
        }

        private static FloatField Field(string label)
        {
            var field = new FloatField(label) { isDelayed = true };
            field.style.width = Length.Percent(33.3f);
            return field;
        }

        private static VisualElement VectorRow(params VisualElement[] fields)
        {
            var row = Row();
            foreach (var field in fields) row.Add(field);
            return row;
        }

        private static VisualElement Row()
        {
            return new VisualElement { style = { flexDirection = FlexDirection.Row, flexShrink = 0 } };
        }

        private static Button Button(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            button.style.minHeight = 28;
            return button;
        }

        private static Label Label(string text, float size, FontStyle style)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = style;
            label.style.marginBottom = 6;
            return label;
        }
    }

    internal sealed class RuntimeSelectionGizmo : MonoBehaviour
    {
        private RuntimeSceneEditorService service;
        private LineRenderer[] axes;
        private Material[] materials;
        private Renderer[] selectedRenderers = Array.Empty<Renderer>();
        private RuntimeSceneObject selected;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private Vector3 lastScale;
        private bool dirty = true;
        private bool renderersDirty = true;
        private readonly Color[] colors = { Color.red, Color.green, Color.blue };

        internal void Initialize(RuntimeSceneEditorService value)
        {
            service = value;
            service.Selection.Changed += MarkSelectionDirty;
            service.StructureChanged += MarkStructureDirty;
            service.TransformChanged += MarkBoundsDirty;
            axes = new LineRenderer[3];
            materials = new Material[3];
            var shader = Shader.Find("Sprites/Default");
            for (int i = 0; i < axes.Length; i++)
            {
                var axis = new GameObject("Sandbox Gizmo Axis " + i);
                axis.transform.SetParent(transform, false);
                var line = axis.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = line.endWidth = .035f;
                var material = new Material(shader)
                {
                    name = "AeroSim Sandbox Gizmo " + i,
                    hideFlags = HideFlags.DontSave
                };
                line.sharedMaterial = material;
                materials[i] = material;
                line.startColor = line.endColor = colors[i];
                line.enabled = false;
                axes[i] = line;
            }
        }

        private void LateUpdate()
        {
            if (axes == null) return;
            var current = service != null && service.IsSandboxActive ? service.Selection.Active : null;
            if (current != selected)
            {
                selected = current;
                renderersDirty = true;
                dirty = true;
            }
            bool show = selected != null && selected.gameObject.activeInHierarchy;
            if (!show)
            {
                if (dirty) SetAxesEnabled(false);
                dirty = false;
                return;
            }
            var selectedTransform = selected.transform;
            if (selectedTransform.position != lastPosition || selectedTransform.rotation != lastRotation || selectedTransform.lossyScale != lastScale)
                dirty = true;
            if (!dirty) return;
            UpdateGeometry(selectedTransform);
            dirty = false;
        }

        private void UpdateGeometry(Transform selectedTransform)
        {
            if (renderersDirty)
            {
                selectedRenderers = selected == null ? Array.Empty<Renderer>() : selected.GetComponentsInChildren<Renderer>(true);
                renderersDirty = false;
            }
            var bounds = new Bounds(selectedTransform.position, Vector3.one);
            bool initialized = false;
            foreach (var renderer in selectedRenderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!initialized) { bounds = renderer.bounds; initialized = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            float length = Mathf.Max(.75f, bounds.extents.magnitude);
            var origin = bounds.center;
            SetAxesEnabled(true);
            axes[0].SetPosition(0, origin); axes[0].SetPosition(1, origin + Vector3.right * length);
            axes[1].SetPosition(0, origin); axes[1].SetPosition(1, origin + Vector3.up * length);
            axes[2].SetPosition(0, origin); axes[2].SetPosition(1, origin + Vector3.forward * length);
            lastPosition = selectedTransform.position;
            lastRotation = selectedTransform.rotation;
            lastScale = selectedTransform.lossyScale;
        }

        private void SetAxesEnabled(bool enabledValue)
        {
            foreach (var axis in axes)
                if (axis != null) axis.enabled = enabledValue;
        }

        private void MarkSelectionDirty()
        {
            dirty = true;
            renderersDirty = true;
        }

        private void MarkStructureDirty()
        {
            dirty = true;
            renderersDirty = true;
        }

        private void MarkBoundsDirty() => dirty = true;

        private void OnDestroy()
        {
            if (service != null)
            {
                service.Selection.Changed -= MarkSelectionDirty;
                service.StructureChanged -= MarkStructureDirty;
                service.TransformChanged -= MarkBoundsDirty;
            }
            if (materials == null) return;
            foreach (var material in materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            materials = null;
        }
    }
}
