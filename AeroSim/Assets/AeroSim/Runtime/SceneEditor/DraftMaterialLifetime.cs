using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Runtime.SceneEditor
{
    [DisallowMultipleComponent]
    public sealed class DraftMaterialLifetime : MonoBehaviour
    {
        private Renderer[] renderers;
        private Material[][] originals;
        private readonly List<Material> owned = new();
        private bool acquired;

        public int OwnedMaterialCount => owned.Count;

        internal void Initialize()
        {
            if (renderers != null) return;
            renderers = GetComponentsInChildren<Renderer>(true);
            originals = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
                originals[i] = renderers[i].sharedMaterials;
            Acquire();
        }

        public void Acquire()
        {
            if (acquired) return;
            if (renderers == null) Initialize();
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer == null) continue;
                var source = originals[rendererIndex];
                var instances = new Material[source.Length];
                for (int materialIndex = 0; materialIndex < source.Length; materialIndex++)
                {
                    var original = source[materialIndex];
                    if (original == null) continue;
                    var instance = new Material(original)
                    {
                        name = original.name + " (AeroSim Draft)",
                        hideFlags = HideFlags.DontSave
                    };
                    instance.color = new Color(.34f, .72f, 1f, 1f);
                    instances[materialIndex] = instance;
                    owned.Add(instance);
                }
                renderer.sharedMaterials = instances;
            }
            acquired = true;
        }

        public void Release()
        {
            if (!acquired && owned.Count == 0) return;
            if (renderers != null && originals != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].sharedMaterials = originals[i];
            }
            foreach (var material in owned)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            owned.Clear();
            acquired = false;
        }

        private void OnDestroy() => Release();
    }
}
