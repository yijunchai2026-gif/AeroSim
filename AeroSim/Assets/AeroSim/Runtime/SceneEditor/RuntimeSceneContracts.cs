using System;
using UnityEngine;

namespace AeroSim.Runtime.SceneEditor
{
    public enum SandboxPrimitive
    {
        Cube,
        Sphere,
        Cylinder,
        RockLike
    }

    [Serializable]
    public struct RuntimeTransformState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public RuntimeTransformState(Transform transform)
        {
            Position = transform.position;
            Rotation = transform.rotation;
            Scale = transform.localScale;
        }

        public void Apply(Transform transform)
        {
            transform.SetPositionAndRotation(Position, Rotation);
            transform.localScale = Scale;
        }

        public bool IsFinite()
        {
            return Finite(Position.x) && Finite(Position.y) && Finite(Position.z) &&
                   Finite(Rotation.x) && Finite(Rotation.y) && Finite(Rotation.z) && Finite(Rotation.w) &&
                   Finite(Scale.x) && Finite(Scale.y) && Finite(Scale.z);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [DisallowMultipleComponent]
    public sealed class RuntimeSceneObject : MonoBehaviour
    {
        public const string DraftStatus = "DRAFT_NOT_USER_CONFIRMED";

        [SerializeField] private string runtimeId;
        [SerializeField] private string prefabId;
        [SerializeField] private string status = DraftStatus;
        [SerializeField] private bool draft = true;

        public string RuntimeId => runtimeId;
        public string PrefabId => prefabId;
        public string Status => status;
        public bool IsDraft => draft && status == DraftStatus;

        internal void Initialize(string id, string allowedPrefabId)
        {
            if (!string.IsNullOrEmpty(runtimeId)) return;
            runtimeId = id;
            prefabId = allowedPrefabId;
            status = DraftStatus;
            draft = true;
        }
    }

    public readonly struct SandboxObjectInfo
    {
        public readonly string RuntimeId;
        public readonly string Name;
        public readonly string PrefabId;
        public readonly string Status;
        public readonly bool Active;
        public readonly RuntimeTransformState Transform;

        public SandboxObjectInfo(RuntimeSceneObject item)
        {
            RuntimeId = item.RuntimeId;
            Name = item.name;
            PrefabId = item.PrefabId;
            Status = item.Status;
            Active = item.gameObject.activeSelf;
            Transform = new RuntimeTransformState(item.transform);
        }
    }

    public sealed class SandboxSelection
    {
        public RuntimeSceneObject Active { get; private set; }
        public event Action Changed;

        internal void Set(RuntimeSceneObject value)
        {
            if (ReferenceEquals(Active, value)) return;
            Active = value;
            Changed?.Invoke();
        }

        internal void Clear() => Set(null);
    }
}
