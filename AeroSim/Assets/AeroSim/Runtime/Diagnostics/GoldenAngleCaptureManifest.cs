using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroSim.Domain;
using AeroSim.Simulation;
using UnityEngine;

namespace AeroSim.Diagnostics
{
    [Serializable]
    public sealed class GoldenAngleCapturePackage
    {
        public string schema = "aerosim.rd06-golden-angle-capture/v1";
        public string generatedUtc;
        public string manifestVersion;
        public string runtimeId;
        public string assetId;
        public string sourceSha256;
        public string contentStatus;
        public string statusDetail;
        public GoldenAngleCaptureCase[] captureCases;
    }

    [Serializable]
    public sealed class GoldenAngleCaptureCase
    {
        public string caseId;
        public string angleId;
        public Vector3 euler;
        public float distanceMultiplier;
        public string outputFile;
        public string captureStatus;
    }

    [Serializable]
    public sealed class BlindTestPackageManifest
    {
        public string schema = "aerosim.rd06-blind-test-package/v1";
        public string generatedUtc;
        public string manifestVersion;
        public int randomSeed;
        public string packageStatus;
        public string instructions;
        public BlindParticipantCase[] participantCases;
        public BlindOrganizerTruth[] organizerTruth;
    }

    [Serializable]
    public sealed class BlindParticipantCase
    {
        public string blindId;
        public string imageFile;
        public string captureStatus;
    }

    [Serializable]
    public sealed class BlindOrganizerTruth
    {
        public string blindId;
        public string runtimeId;
        public string assetId;
        public string angleId;
        public string sourceSha256;
        public string contentStatus;
    }

    /// <summary>
    /// RD-06 evidence skeleton. It describes captures and blind-test inputs; it does not move cameras,
    /// render images, or promote missing/programmatic content to production status.
    /// </summary>
    public static class GoldenAngleCaptureManifest
    {
        public const string NotCaptured = "NOT_CAPTURED";

        public static GoldenAngleCapturePackage Build(string runtimeId)
        {
            var asset = AircraftAssetContracts.Resolve(runtimeId);
            var load = ProductionAssetLoader.Inspect(runtimeId);
            var angles = asset?.goldenAngles ?? Array.Empty<GoldenAngleContract>();
            return new GoldenAngleCapturePackage
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                manifestVersion = AircraftAssetContracts.Manifest?.manifestVersion ?? string.Empty,
                runtimeId = runtimeId ?? string.Empty,
                assetId = asset?.assetId ?? string.Empty,
                sourceSha256 = asset?.sourceSha256 ?? string.Empty,
                contentStatus = load.State == ProductionAssetLoadState.Ready
                    ? "READY_FOR_CAPTURE"
                    : ProductionAssetLoader.ContentNotDelivered,
                statusDetail = load.Message,
                captureCases = angles.Select((angle, index) => new GoldenAngleCaptureCase
                {
                    caseId = "G" + (index + 1).ToString("00"),
                    angleId = angle.id,
                    euler = angle.euler,
                    distanceMultiplier = angle.distanceMultiplier,
                    outputFile = Safe(runtimeId) + "/G" + (index + 1).ToString("00") + "-" + Safe(angle.id) + ".png",
                    captureStatus = NotCaptured
                }).ToArray()
            };
        }

        public static bool Validate(GoldenAngleCapturePackage package, out string report)
        {
            var errors = new List<string>();
            if (package == null) errors.Add("Capture package is null.");
            else
            {
                if (package.schema != "aerosim.rd06-golden-angle-capture/v1") errors.Add("Unsupported capture schema.");
                if (string.IsNullOrWhiteSpace(package.runtimeId)) errors.Add("runtimeId is required.");
                if (package.captureCases == null || package.captureCases.Length != 12)
                    errors.Add("Exactly 12 RD-02 golden-angle cases are required.");
                else
                {
                    if (package.captureCases.Select(value => value.caseId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 12)
                        errors.Add("Capture case IDs must be unique.");
                    if (package.captureCases.Select(value => value.angleId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 12)
                        errors.Add("Golden angle IDs must be unique.");
                    if (package.captureCases.Any(value => value.distanceMultiplier <= 0 || value.captureStatus != NotCaptured))
                        errors.Add("New capture cases must have a positive distance and NOT_CAPTURED status.");
                }
            }
            report = errors.Count == 0
                ? "RD-06 golden-angle manifest PASS · 12 cases."
                : "RD-06 golden-angle manifest FAIL\n- " + string.Join("\n- ", errors);
            return errors.Count == 0;
        }

        public static GoldenAngleCapturePackage Export(string runtimeId, string outputPath)
        {
            var package = Build(runtimeId);
            if (!Validate(package, out string report)) throw new InvalidDataException(report);
            WriteJson(outputPath, JsonUtility.ToJson(package, true));
            return package;
        }

        public static BlindTestPackageManifest ExportBlindTestPackage(
            IEnumerable<string> runtimeIds, string outputPath, int randomSeed)
        {
            var source = (runtimeIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(Build)
                .ToArray();
            if (source.Length == 0) throw new InvalidDataException("At least one runtimeId is required.");
            foreach (var package in source)
                if (!Validate(package, out string report)) throw new InvalidDataException(report);

            var truth = source.SelectMany(package => package.captureCases.Select(capture => new BlindOrganizerTruth
            {
                runtimeId = package.runtimeId,
                assetId = package.assetId,
                angleId = capture.angleId,
                sourceSha256 = package.sourceSha256,
                contentStatus = package.contentStatus
            })).ToList();
            Shuffle(truth, randomSeed);

            var participants = new BlindParticipantCase[truth.Count];
            for (int i = 0; i < truth.Count; i++)
            {
                string blindId = "B" + (i + 1).ToString("000");
                truth[i].blindId = blindId;
                participants[i] = new BlindParticipantCase
                {
                    blindId = blindId,
                    imageFile = "images/" + blindId + ".png",
                    captureStatus = NotCaptured
                };
            }

            var manifest = new BlindTestPackageManifest
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                manifestVersion = AircraftAssetContracts.Manifest?.manifestVersion ?? string.Empty,
                randomSeed = randomSeed,
                packageStatus = source.All(value => value.contentStatus == "READY_FOR_CAPTURE")
                    ? "SKELETON_READY"
                    : ProductionAssetLoader.ContentNotDelivered,
                instructions = "Distribute participantCases only. Keep organizerTruth private. Missing images remain NOT_CAPTURED and cannot pass blind review.",
                participantCases = participants,
                organizerTruth = truth.ToArray()
            };
            WriteJson(outputPath, JsonUtility.ToJson(manifest, true));
            return manifest;
        }

        private static void Shuffle<T>(IList<T> values, int seed)
        {
            var random = new System.Random(seed);
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                (values[i], values[swap]) = (values[swap], values[i]);
            }
        }

        private static void WriteJson(string outputPath, string json)
        {
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));
            string fullPath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidDataException("Output directory is invalid.");
            Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, json);
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "missing";
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        }
    }
}
