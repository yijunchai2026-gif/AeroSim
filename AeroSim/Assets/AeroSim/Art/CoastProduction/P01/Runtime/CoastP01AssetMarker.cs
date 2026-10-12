using UnityEngine;

namespace AeroSim.CoastP01
{
    [DisallowMultipleComponent]
    public sealed class CoastP01AssetMarker : MonoBehaviour
    {
        public string assetId;
        [TextArea] public string provenance = "PROCEDURAL / GENERATED SOURCE — NOT A SCAN";
    }
}
