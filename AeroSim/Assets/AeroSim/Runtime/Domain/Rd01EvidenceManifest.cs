using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.Domain
{
    [Serializable]
    public sealed class Rd01EvidenceManifest
    {
        public string schemaVersion;
        public string manifestId;
        public string prdVersion;
        public string workPackage;
        public string milestone;
        public string frozenAtUtc;
        public Rd01SourceBaseline sourceBaseline;
        public Rd01BuildIdentity build;
        public Rd01QualityDeclaration qualityDeclaration;
        public Rd01TargetHardware targetHardware;
        public Rd01RenderPipelineAdr renderPipelineAdr;
        public Rd01ReplayCase fixedReplay;
        public Rd01Case[] goldenShotCases;
        public Rd01Case[] aircraftShotCases;
        public Rd01Case[] cameraMatrixCases;
        public Rd01RuntimeObservation runtimeObservation;
        public Rd01EvidenceArtifact[] artifacts;
    }

    [Serializable]
    public sealed class Rd01SourceBaseline
    {
        public string branch;
        public string headCommit;
        public bool worktreeDirtyAtFreeze;
        public string note;
    }

    [Serializable]
    public sealed class Rd01BuildIdentity
    {
        public string buildId;
        public string status;
        public string sourceCommit;
        public string unityVersion;
        public string productVersion;
    }

    [Serializable]
    public sealed class Rd01QualityDeclaration
    {
        public string levelId;
        public string levelName;
        public float weightedScore;
        public string rcDecision;
        public string gateStatus;
        public string statement;
    }

    [Serializable]
    public sealed class Rd01TargetHardware
    {
        public string profileId;
        public string status;
        public string os;
        public string cpu;
        public string gpu;
        public string memory;
        public string storage;
        public string resolution;
        public string quality;
        public int targetFps;
        public string driverPolicy;
        public string performanceEvidence;
    }

    [Serializable]
    public sealed class Rd01RenderPipelineAdr
    {
        public string adrId;
        public string status;
        public string mainlinePipeline;
        public string decision;
        public string migrationApproval;
        public Rd01PipelineSpike[] spikes;
        public string[] consequences;
    }

    [Serializable]
    public sealed class Rd01PipelineSpike
    {
        public string pipeline;
        public string status;
        public string evidence;
    }

    [Serializable]
    public sealed class Rd01ReplayCase
    {
        public string caseId;
        public string status;
        public string scene;
        public string weather;
        public string timeOfDay;
        public int seed;
        public int durationSeconds;
        public string route;
    }

    [Serializable]
    public sealed class Rd01Case
    {
        public string caseId;
        public string status;
        public string label;
    }

    [Serializable]
    public sealed class Rd01RuntimeObservation
    {
        public string capturedAtUtc;
        public string unityVersion;
        public string productVersion;
        public string platform;
        public string operatingSystem;
        public string cpu;
        public string gpu;
        public int memoryMb;
        public string activeRenderPipeline;
        public int width;
        public int height;
        public string quality;
        public string validationStatus;
    }

    [Serializable]
    public sealed class Rd01EvidenceArtifact
    {
        public string caseId;
        public string kind;
        public string relativePath;
        public string sha256;
        public long bytes;
        public string status;
    }

    // RD-01 is infrastructure only: this API records facts and NOT_RUN states. It never
    // promotes automated checks into a visual, comfort, performance, or 3A gate PASS.
    public static class Rd01Manifest
    {
        public const string ResourcePath = "RD01/rd01-baseline-manifest";
        public const string ExpectedSchemaVersion = "aerosim.evidence/v1";
        private static Rd01EvidenceManifest _current;

        public static Rd01EvidenceManifest Current
        {
            get
            {
                if (_current != null) return _current;
                var asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset == null) throw new InvalidOperationException("RD-01 manifest resource is missing: " + ResourcePath);
                _current = JsonUtility.FromJson<Rd01EvidenceManifest>(asset.text);
                if (_current == null) throw new InvalidDataException("RD-01 manifest JSON could not be parsed.");
                return _current;
            }
        }

        public static IReadOnlyList<string> Validate(Rd01EvidenceManifest manifest)
        {
            var errors = new List<string>();
            if (manifest == null) { errors.Add("manifest is null"); return errors; }
            Require(errors, manifest.schemaVersion == ExpectedSchemaVersion, "schemaVersion must be " + ExpectedSchemaVersion);
            Require(errors, manifest.prdVersion == "4.0", "prdVersion must be 4.0");
            Require(errors, manifest.workPackage == "RD-01" && manifest.milestone == "M0", "ownership must be RD-01/M0");
            Require(errors, manifest.sourceBaseline != null && IsFullSha(manifest.sourceBaseline.headCommit), "baseline HEAD must be a full Git SHA");
            Require(errors, manifest.build != null && manifest.build.status == "NOT_RUN", "unbuilt manifest must remain NOT_RUN");
            Require(errors, manifest.qualityDeclaration != null && manifest.qualityDeclaration.levelId == "L1", "quality level must remain L1");
            Require(errors, manifest.qualityDeclaration != null && manifest.qualityDeclaration.rcDecision == "NO-GO", "RC decision must remain NO-GO");
            Require(errors, manifest.qualityDeclaration != null && manifest.qualityDeclaration.gateStatus == "NOT_RUN", "quality gates must remain NOT_RUN");
            Require(errors, manifest.targetHardware != null && manifest.targetHardware.status == "CANDIDATE_NOT_BENCHMARKED", "target hardware must be marked candidate/not benchmarked");
            Require(errors, manifest.renderPipelineAdr != null && manifest.renderPipelineAdr.adrId == "ADR-RND-001", "render pipeline ADR is required");
            Require(errors, manifest.renderPipelineAdr != null && manifest.renderPipelineAdr.migrationApproval == "NOT_APPROVED", "pipeline migration must not be approved");
            Require(errors, manifest.fixedReplay != null && manifest.fixedReplay.caseId == "RPL-MAIN-001", "fixed replay case RPL-MAIN-001 is required");
            ValidateCases(errors, manifest.goldenShotCases, "G3-GOLDEN-", 8);
            ValidateCases(errors, manifest.aircraftShotCases, "G1-GOLDEN-", 8);
            ValidateCases(errors, manifest.cameraMatrixCases, "G2-CAMERA-", 18);
            var allCases = Cases(manifest).ToArray();
            Require(errors, allCases.Select(value => value.caseId).Distinct(StringComparer.Ordinal).Count() == allCases.Length, "case IDs must be unique");
            Require(errors, allCases.All(value => value.status == "NOT_RUN"), "baseline cases must not claim PASS");
            return errors;
        }

        public static Rd01EvidenceManifest CreateRuntimeSnapshot(string buildId = null, string sourceCommit = null)
        {
            bool hasBuildIdentity = !string.IsNullOrWhiteSpace(buildId) || !string.IsNullOrWhiteSpace(sourceCommit);
            if (hasBuildIdentity && (string.IsNullOrWhiteSpace(buildId) || !IsFullSha(sourceCommit)))
                throw new ArgumentException("A runtime evidence build requires both a build ID and a full lowercase source commit SHA.");
            var snapshot = JsonUtility.FromJson<Rd01EvidenceManifest>(JsonUtility.ToJson(Current));
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var errors = Validate(snapshot);
            if (hasBuildIdentity)
            {
                snapshot.build.buildId = buildId;
                snapshot.build.status = "CAPTURED_UNREVIEWED";
                snapshot.build.sourceCommit = sourceCommit;
                snapshot.build.unityVersion = Application.unityVersion;
                snapshot.build.productVersion = Application.version;
            }
            snapshot.runtimeObservation = new Rd01RuntimeObservation
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                productVersion = Application.version,
                platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName,
                memoryMb = SystemInfo.systemMemorySize,
                activeRenderPipeline = pipeline == null ? "Built-in" : pipeline.GetType().FullName,
                width = Screen.width,
                height = Screen.height,
                quality = AeroSimQuality.Name(LocalStore.Profile.settings.quality),
                validationStatus = errors.Count == 0 ? "VALID" : "INVALID"
            };
            return snapshot;
        }

        public static void WriteRuntimeSnapshot(string path, string buildId = null, string sourceCommit = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Evidence manifest output path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, JsonUtility.ToJson(CreateRuntimeSnapshot(buildId, sourceCommit), true));
        }

        public static Rd01EvidenceArtifact InspectArtifact(string caseId, string kind, string path, string packageRoot)
        {
            if (!Cases(Current).Any(value => value.caseId == caseId)) throw new ArgumentException("Unknown fixed evidence case ID.", nameof(caseId));
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.Ordinal) || !File.Exists(fullPath)) throw new ArgumentException("Artifact must be an existing file inside the evidence package.", nameof(path));
            using (var stream = File.OpenRead(fullPath))
            using (var sha = SHA256.Create())
            {
                return new Rd01EvidenceArtifact
                {
                    caseId = caseId,
                    kind = kind,
                    relativePath = fullPath.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/'),
                    sha256 = string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2"))),
                    bytes = stream.Length,
                    status = "CAPTURED_UNREVIEWED"
                };
            }
        }

        public static IEnumerable<Rd01Case> Cases(Rd01EvidenceManifest manifest)
        {
            if (manifest == null) yield break;
            if (manifest.fixedReplay != null) yield return new Rd01Case { caseId = manifest.fixedReplay.caseId, status = manifest.fixedReplay.status, label = manifest.fixedReplay.route };
            foreach (var item in manifest.goldenShotCases ?? Array.Empty<Rd01Case>()) yield return item;
            foreach (var item in manifest.aircraftShotCases ?? Array.Empty<Rd01Case>()) yield return item;
            foreach (var item in manifest.cameraMatrixCases ?? Array.Empty<Rd01Case>()) yield return item;
        }

        private static void ValidateCases(List<string> errors, Rd01Case[] cases, string prefix, int count)
        {
            Require(errors, cases != null && cases.Length == count, prefix + " matrix must contain " + count + " cases");
            if (cases != null) Require(errors, cases.All(value => value != null && value.caseId != null && value.caseId.StartsWith(prefix, StringComparison.Ordinal)), prefix + " case ID prefix is invalid");
        }

        private static bool IsFullSha(string value) => value != null && value.Length == 40 && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');
        private static void Require(List<string> errors, bool condition, string message) { if (!condition) errors.Add(message); }
    }
}
