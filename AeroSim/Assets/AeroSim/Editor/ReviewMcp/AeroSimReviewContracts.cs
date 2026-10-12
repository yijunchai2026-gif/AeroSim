#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Editor.ReviewMcp
{
    internal static class AeroSimReviewProtocol
    {
        public const string Version = "aerosim.review-mcp/v1";
        public const string GeneratedUnreviewed = "GENERATED_UNREVIEWED";
        public static readonly HashSet<string> ReadTools = new HashSet<string>(StringComparer.Ordinal)
        {
            "capture_scene_view", "capture_game_view", "capture_camera", "capture_diagnostic_bundle",
            "inspect_scene", "inspect_object", "inspect_material", "inspect_lod_group", "inspect_colliders",
            "inspect_prefab", "get_console_logs", "get_shader_errors", "get_performance_metrics",
            "raycast_from_view", "list_visible_objects", "run_validator"
        };

        public static readonly HashSet<string> MutationTools = new HashSet<string>(StringComparer.Ordinal)
        {
            "set_transform", "set_camera", "set_light_property", "set_material_property",
            "set_lod_thresholds", "add_or_update_collider", "toggle_object", "undo_change",
            "rollback_transaction", "replace_material", "replace_mesh", "add_component",
            "update_component", "edit_prefab", "update_model_importer", "reimport_asset",
            "run_asset_gate", "save_scene", "save_prefab"
        };
    }

    [Serializable]
    internal sealed class ReviewRequest
    {
        public string protocol = AeroSimReviewProtocol.Version;
        public string id;
        public string tool;
        public string target;
        public string transactionId;
        public string property;
        public string stringValue;
        public string assetPath;
        public string secondaryAssetPath;
        public string componentType;
        public Vector3 vectorValue;
        public Vector3 rotationEuler;
        public Color colorValue = Color.white;
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public float[] floatValues = Array.Empty<float>();
        public string[] arguments = Array.Empty<string>();
        public CaptureOptions capture = new CaptureOptions();
    }

    [Serializable]
    internal sealed class CaptureOptions
    {
        public int width = 1920;
        public int height = 1080;
        public string cameraPath;
        public string[] channels = Array.Empty<string>();
        public bool includeUi = true;
    }

    [Serializable]
    internal sealed class ReviewResponse
    {
        public string protocol = AeroSimReviewProtocol.Version;
        public string requestId;
        public string tool;
        public bool success;
        public string status;
        public string message;
        public string runId;
        public string evidenceDirectory;
        public string transactionId;
        public string[] artifacts = Array.Empty<string>();
        public ReviewFinding[] findings = Array.Empty<ReviewFinding>();
        public string errorType;
        public string stackTrace;
    }

    [Serializable]
    internal sealed class ReviewFinding
    {
        public string id;
        public string severity;
        public string category;
        public string target;
        public string evidence;
        public string expected;
        public string actual;
        public string recommendation;
        public float confidence;
    }

    [Serializable]
    internal sealed class EvidenceManifest
    {
        public string schema = "aerosim.review-evidence/v1";
        public string runId;
        public string generatedUtc;
        public string gitCommit;
        public string unityVersion;
        public string scene;
        public string tool;
        public string requestId;
        public string reviewStatus = "NOT_REVIEWED";
        public string gateStatus = "GATE_NOT_RUN";
        public string productionStatus = AeroSimReviewProtocol.GeneratedUnreviewed;
        public string[] artifacts = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class SceneSnapshot
    {
        public string scene;
        public string generatedUtc;
        public SceneObjectSnapshot[] objects = Array.Empty<SceneObjectSnapshot>();
    }

    [Serializable]
    internal sealed class SceneObjectSnapshot
    {
        public string globalObjectId;
        public string hierarchyPath;
        public string name;
        public string tag;
        public int layer;
        public bool active;
        public Vector3 position;
        public Vector3 rotationEuler;
        public Vector3 scale;
        public string[] components = Array.Empty<string>();
        public string[] materials = Array.Empty<string>();
        public Bounds bounds;
    }

    [Serializable]
    internal sealed class ChangeRecord
    {
        public string transactionId;
        public string requestId;
        public string tool;
        public string target;
        public string generatedUtc;
        public string beforeJson;
        public string afterJson;
        public int undoGroup;
        public bool verified;
        public bool rolledBack;
    }

    [Serializable]
    internal sealed class ReviewRunIndex
    {
        public string schema = "aerosim.review-run-index/v1";
        public string runId;
        public string[] requests = Array.Empty<string>();
        public string[] responses = Array.Empty<string>();
        public ChangeRecord[] changes = Array.Empty<ChangeRecord>();
    }
}
#endif
