using System;
using System.Collections.Generic;
using AeroSim.Input;
using AeroSim.Simulation;
using UnityEngine;

namespace AeroSim.Runtime.SceneEditor
{
    [DisallowMultipleComponent]
    public sealed class RuntimeSceneEditorService : MonoBehaviour
    {
        public const float BoundaryRadius = 100f;
        public const float BoundaryMinY = -2f;
        public const float BoundaryMaxY = 80f;

        private readonly Dictionary<string, RuntimeSceneObject> objects = new(StringComparer.Ordinal);
        private readonly RuntimeCommandHistory history = new();
        private Transform sandboxRoot;
        private int nextId;
        private string sessionId;
        private FlightSimulator suspendedSimulator;
        private bool resumeFlightOnExit;

        public bool IsSandboxActive { get; private set; }
        public SandboxSelection Selection { get; } = new();
        public int UndoCount => history.UndoCount;
        public int RedoCount => history.RedoCount;
        public string UndoLabel => history.UndoLabel;
        public string RedoLabel => history.RedoLabel;
        public event Action Changed;
        public event Action StructureChanged;
        public event Action TransformChanged;

        private void Awake()
        {
            history.Changed += RaiseChanged;
            Selection.Changed += RaiseChanged;
        }

        private void OnDestroy()
        {
            history.Clear();
        }

        public void EnterSandbox()
        {
            if (IsSandboxActive) return;
            EnsureRoot();
            sessionId = Guid.NewGuid().ToString("N");
            nextId = 0;
            history.Clear();
            Selection.Clear();
            suspendedSimulator = FindFirstObjectByType<FlightSimulator>();
            resumeFlightOnExit = suspendedSimulator != null && suspendedSimulator.Running && !suspendedSimulator.Paused;
            if (resumeFlightOnExit) suspendedSimulator.Pause(true);
            FlightInput.ClearAndRequireNeutral();
            IsSandboxActive = true;
            sandboxRoot.gameObject.SetActive(true);
            RaiseChanged();
        }

        public void ExitSandbox(bool rollback = true)
        {
            if (!IsSandboxActive) return;
            if (rollback) history.RollbackAll();
            Selection.Clear();
            IsSandboxActive = false;
            if (sandboxRoot != null) sandboxRoot.gameObject.SetActive(false);
            history.Clear();
            objects.Clear();
            if (resumeFlightOnExit && suspendedSimulator != null && suspendedSimulator.Running)
                suspendedSimulator.Pause(false);
            suspendedSimulator = null;
            resumeFlightOnExit = false;
            FlightInput.ClearAndRequireNeutral();
            RaiseChanged();
        }

        public RuntimeSceneObject Spawn(SandboxPrimitive primitive)
        {
            RequireSandbox();
            EnsureRoot();
            var id = "draft-" + sessionId + "-" + (++nextId).ToString("D4");
            var item = CreatePrimitive(primitive, id);
            item.gameObject.SetActive(false);
            history.Execute(new SpawnDraftCommand(item, SetActive));
            Selection.Set(item);
            return item;
        }

        public bool Select(string runtimeId)
        {
            RequireSandbox();
            if (!objects.TryGetValue(runtimeId ?? string.Empty, out var item) || item == null || !item.gameObject.activeSelf)
                return false;
            Selection.Set(item);
            return true;
        }

        public bool DeleteSelected()
        {
            RequireSandbox();
            var item = RequireEditableSelection();
            history.Execute(new SetDraftActiveCommand(item, false, "Delete " + item.name, SetActive));
            return true;
        }

        public bool SetSelectedTransform(Vector3 position, Vector3 rotationEuler, Vector3 scale)
        {
            RequireSandbox();
            var item = RequireEditableSelection();
            var next = new RuntimeTransformState
            {
                Position = position,
                Rotation = Quaternion.Euler(rotationEuler),
                Scale = scale
            };
            if (!next.IsFinite()) throw new ArgumentException("Transform values must be finite.");
            if (!IsInsideBoundary(position))
                throw new ArgumentException("Position is outside the Sandbox boundary.");
            if (!IsScaleInsideEnvelope(scale))
                throw new ArgumentException("Scale is outside the Sandbox safety envelope (axis 0.02..50, anisotropy <= 100).");
            history.Execute(new TransformDraftCommand(item, new RuntimeTransformState(item.transform), next, _ => RaiseTransformChanged()));
            return true;
        }

        public bool ClearDrafts()
        {
            RequireSandbox();
            var commands = new List<IRuntimeSceneCommand>();
            foreach (var item in objects.Values)
                if (item != null && item.IsDraft && item.gameObject.activeSelf)
                    commands.Add(new SetDraftActiveCommand(item, false, "Clear " + item.name, SetActive));
            if (commands.Count == 0) return false;
            history.Execute(new CompositeDraftCommand("Clear all drafts", commands));
            return true;
        }

        public bool Undo()
        {
            RequireSandbox();
            return history.Undo();
        }

        public bool Redo()
        {
            RequireSandbox();
            return history.Redo();
        }

        public List<SandboxObjectInfo> Hierarchy()
        {
            var result = new List<SandboxObjectInfo>();
            foreach (var item in objects.Values)
                if (item != null && item.gameObject.activeSelf) result.Add(new SandboxObjectInfo(item));
            result.Sort((left, right) => string.CompareOrdinal(left.RuntimeId, right.RuntimeId));
            return result;
        }

        public static bool IsInsideBoundary(Vector3 position)
        {
            return Mathf.Abs(position.x) <= BoundaryRadius && Mathf.Abs(position.z) <= BoundaryRadius &&
                   position.y >= BoundaryMinY && position.y <= BoundaryMaxY;
        }

        public static bool IsScaleInsideEnvelope(Vector3 scale)
        {
            float min = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            float max = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return min >= .02f && max <= 50f && max / min <= 100f;
        }

        private RuntimeSceneObject CreatePrimitive(SandboxPrimitive primitive, string id)
        {
            GameObject root;
            if (primitive == SandboxPrimitive.RockLike)
            {
                root = new GameObject("Draft Rock-like");
                CreateRockPart(root.transform, PrimitiveType.Sphere, new Vector3(-.35f, .35f, 0), new Vector3(1.4f, .7f, 1.1f));
                CreateRockPart(root.transform, PrimitiveType.Sphere, new Vector3(.38f, .28f, .08f), new Vector3(1f, .55f, .9f));
                CreateRockPart(root.transform, PrimitiveType.Sphere, new Vector3(.05f, .58f, -.12f), new Vector3(.9f, .45f, .75f));
            }
            else
            {
                var type = primitive == SandboxPrimitive.Cube ? PrimitiveType.Cube :
                    primitive == SandboxPrimitive.Sphere ? PrimitiveType.Sphere : PrimitiveType.Cylinder;
                root = GameObject.CreatePrimitive(type);
                root.name = "Draft " + primitive;
            }
            root.transform.SetParent(sandboxRoot, false);
            root.transform.position = new Vector3(
                (objects.Count % 5) * 2f - 4f,
                primitive == SandboxPrimitive.Cylinder ? 1f : primitive == SandboxPrimitive.RockLike ? 0f : .5f,
                4f);
            var marker = root.AddComponent<RuntimeSceneObject>();
            marker.Initialize(id, primitive.ToString());
            root.AddComponent<DraftMaterialLifetime>().Initialize();
            objects.Add(id, marker);
            return marker;
        }

        private static void CreateRockPart(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = "Rock part";
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
        }

        private void EnsureRoot()
        {
            if (sandboxRoot != null) return;
            var root = new GameObject("AeroSim Runtime Sandbox - DRAFT_NOT_USER_CONFIRMED");
            root.transform.SetParent(transform, false);
            sandboxRoot = root.transform;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Sandbox Support Floor (Infrastructure - Read Only)";
            floor.transform.SetParent(sandboxRoot, false);
            floor.transform.position = new Vector3(0, -.25f, 0);
            floor.transform.localScale = new Vector3(BoundaryRadius * 2f, .5f, BoundaryRadius * 2f);
            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
            root.SetActive(false);
        }

        private RuntimeSceneObject RequireEditableSelection()
        {
            var item = Selection.Active;
            if (item == null || !item.IsDraft || !objects.ContainsKey(item.RuntimeId))
                throw new InvalidOperationException("Only an active draft sandbox object can be edited.");
            return item;
        }

        private void SetActive(RuntimeSceneObject item, bool active)
        {
            if (item == null || !item.IsDraft || !objects.ContainsKey(item.RuntimeId))
                throw new InvalidOperationException("Command target is not an owned draft sandbox object.");
            var materials = item.GetComponent<DraftMaterialLifetime>();
            if (active)
            {
                item.gameObject.SetActive(true);
                materials?.Acquire();
                Selection.Set(item);
            }
            else
            {
                materials?.Release();
                item.gameObject.SetActive(false);
                if (ReferenceEquals(Selection.Active, item)) Selection.Clear();
            }
            StructureChanged?.Invoke();
            RaiseChanged();
        }

        private void RaiseTransformChanged()
        {
            TransformChanged?.Invoke();
            RaiseChanged();
        }

        private void RequireSandbox()
        {
            if (!IsSandboxActive) throw new InvalidOperationException("Runtime scene editing is disabled outside Sandbox mode.");
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
