#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AeroSim.Editor.ReviewMcp
{
    [InitializeOnLoad]
    public static class AeroSimReviewBridge
    {
        private const string EnabledPreference = "AeroSim.ReviewMcp.QueueEnabled";
        private const int MaxRequestBytes = 4 * 1024 * 1024;
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string QueueRoot = RestrictedPath(ProjectRoot, "Library", "AeroSimReview");
        private static readonly string Inbox = RestrictedPath(QueueRoot, "inbox");
        private static readonly string Outbox = RestrictedPath(QueueRoot, "outbox");
        private static readonly string Runs = RestrictedPath(QueueRoot, "runs");
        private static bool processing;

        static AeroSimReviewBridge()
        {
            EnsureQueueDirectories();
            if (EditorPrefs.GetBool(EnabledPreference, false))
            {
                RegisterUpdate();
            }
        }

        [MenuItem("AeroSim/Review MCP/Start Queue")]
        public static void StartQueue()
        {
            EnsureQueueDirectories();
            EditorPrefs.SetBool(EnabledPreference, true);
            RegisterUpdate();
            Debug.Log("AeroSim Review MCP queue started: " + QueueRoot);
        }

        [MenuItem("AeroSim/Review MCP/Stop Queue")]
        public static void StopQueue()
        {
            EditorPrefs.SetBool(EnabledPreference, false);
            EditorApplication.update -= ProcessUpdate;
            Debug.Log("AeroSim Review MCP queue stopped.");
        }

        [MenuItem("AeroSim/Review MCP/Process One Request")]
        public static void ProcessOneRequest()
        {
            EnsureQueueDirectories();
            ProcessNextRequest();
        }

        // Batch entry: -executeMethod AeroSim.Editor.ReviewMcp.AeroSimReviewBridge.ProcessAllPending
        public static void ProcessAllPending()
        {
            EnsureQueueDirectories();
            int count = 0;
            while (ProcessNextRequest())
            {
                count++;
                if (count >= 10000)
                {
                    throw new InvalidOperationException("Review queue safety limit of 10000 requests was reached.");
                }
            }
            Debug.Log("AeroSim Review MCP processed " + count + " pending request(s). Responses: " + Outbox);
        }

        // Batch entry for exactly one item.
        public static void ProcessOnePending()
        {
            EnsureQueueDirectories();
            bool processed = ProcessNextRequest();
            Debug.Log("AeroSim Review MCP process-one result: " + processed);
        }

        internal static void WriteTextAtomically(string path, string content)
        {
            string fullPath = Path.GetFullPath(path);
            EnsureContained(QueueRoot, fullPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Atomic output path has no directory.");
            }
            Directory.CreateDirectory(directory);
            string temporary = RestrictedPath(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, content, new UTF8Encoding(false));
                if (File.Exists(fullPath))
                {
                    throw new IOException("Refusing to overwrite an existing queue artifact: " + fullPath);
                }
                File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static void RegisterUpdate()
        {
            EditorApplication.update -= ProcessUpdate;
            EditorApplication.update += ProcessUpdate;
        }

        private static void ProcessUpdate()
        {
            ProcessNextRequest();
        }

        private static bool ProcessNextRequest()
        {
            if (processing)
            {
                return false;
            }

            string source = Directory.EnumerateFiles(Inbox, "*", SearchOption.TopDirectoryOnly)
                .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                               (Path.GetFileName(path).StartsWith(".processing-", StringComparison.Ordinal) && path.EndsWith(".request", StringComparison.Ordinal)))
                .OrderBy(path => File.GetLastWriteTimeUtc(path))
                .ThenBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (source == null)
            {
                return false;
            }

            processing = true;
            bool isRecoveredClaim = Path.GetFileName(source).StartsWith(".processing-", StringComparison.Ordinal) && source.EndsWith(".request", StringComparison.Ordinal);
            string claim = isRecoveredClaim ? source : RestrictedPath(Inbox, ".processing-" + Guid.NewGuid().ToString("N") + ".request");
            try
            {
                if (!isRecoveredClaim)
                {
                    File.Move(source, claim);
                }
                ProcessClaim(claim, Path.GetFileName(source));
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException exception)
            {
                Debug.LogError("AeroSim Review MCP queue I/O failure: " + exception);
                return false;
            }
            finally
            {
                processing = false;
            }
        }

        private static void ProcessClaim(string claim, string originalFileName)
        {
            ReviewRequest request = null;
            ReviewResponse response = null;
            string runId = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'") + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            string runDirectory = RestrictedPath(Runs, runId);
            Directory.CreateDirectory(runDirectory);
            string archivedRequest = RestrictedPath(runDirectory, "request.json");

            try
            {
                FileInfo info = new FileInfo(claim);
                if (info.Length <= 0 || info.Length > MaxRequestBytes)
                {
                    throw new InvalidDataException("Request JSON must be between 1 byte and " + MaxRequestBytes + " bytes.");
                }
                string json = File.ReadAllText(claim, Encoding.UTF8);
                request = JsonUtility.FromJson<ReviewRequest>(json);
                if (request == null)
                {
                    throw new InvalidDataException("Request JSON could not be deserialized.");
                }
                File.Move(claim, archivedRequest);
                response = ExecuteRequest(request, runId, runDirectory);
            }
            catch (Exception exception)
            {
                if (File.Exists(claim))
                {
                    File.Move(claim, archivedRequest);
                }
                response = FailureResponse(request, runId, runDirectory, "INVALID_REQUEST", exception.Message, exception);
            }

            try
            {
                WriteManifest(request, response, runId, runDirectory);
            }
            catch (Exception exception)
            {
                response.success = false;
                response.status = "EVIDENCE_WRITE_FAILED";
                response.message = "Request executed, but the evidence manifest could not be written: " + exception.Message;
                response.errorType = exception.GetType().FullName;
                response.stackTrace = exception.StackTrace;
            }

            response.artifacts = Directory.EnumerateFiles(runDirectory, "*", SearchOption.AllDirectories)
                .Select(path => RelativeToProject(path))
                .Concat(new[]
                {
                    RelativeToProject(RestrictedPath(runDirectory, "run-index.json")),
                    RelativeToProject(RestrictedPath(runDirectory, "response.json"))
                })
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            WriteRunIndex(request, response, runId, runDirectory);
            response.artifacts = Directory.EnumerateFiles(runDirectory, "*", SearchOption.AllDirectories)
                .Select(path => RelativeToProject(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            string responseName = SanitizeFileStem(!string.IsNullOrEmpty(request != null ? request.id : null) ? request.id : Path.GetFileNameWithoutExtension(originalFileName));
            string output = RestrictedPath(Outbox, responseName + "-" + runId + ".json");
            WriteTextAtomically(output, JsonUtility.ToJson(response, true));
        }

        private static ReviewResponse ExecuteRequest(ReviewRequest request, string runId, string runDirectory)
        {
            if (!string.Equals(request.protocol, AeroSimReviewProtocol.Version, StringComparison.Ordinal))
            {
                return FailureResponse(request, runId, runDirectory, "PROTOCOL_MISMATCH", "Expected protocol " + AeroSimReviewProtocol.Version + ".", null);
            }
            if (string.IsNullOrWhiteSpace(request.id) || request.id.Length > 256)
            {
                return FailureResponse(request, runId, runDirectory, "INVALID_REQUEST", "Request id is required and must not exceed 256 characters.", null);
            }
            if (string.IsNullOrWhiteSpace(request.tool))
            {
                return FailureResponse(request, runId, runDirectory, "INVALID_REQUEST", "Request tool is required.", null);
            }
            if (AeroSimReviewProtocol.MutationTools.Contains(request.tool))
            {
                ReviewResponse mutationResponse = AeroSimReviewMutations.Execute(request, runDirectory);
                mutationResponse.runId = runId;
                mutationResponse.evidenceDirectory = RelativeToProject(runDirectory);
                mutationResponse.transactionId = string.IsNullOrEmpty(mutationResponse.transactionId)
                    ? request.transactionId
                    : mutationResponse.transactionId;
                return mutationResponse;
            }
            if (!AeroSimReviewProtocol.ReadTools.Contains(request.tool))
            {
                return FailureResponse(request, runId, runDirectory, "NOT_SUPPORTED", "NOT_SUPPORTED: tool is not part of the frozen Phase 1 ReadTools set.", null);
            }

            AeroSimReviewInspection.Result result;
            if (request.tool == "capture_scene_view" || request.tool == "capture_game_view" || request.tool == "capture_camera")
            {
                bool captured = AeroSimReviewCapture.TryCapture(request, runDirectory, out string artifact, out string message);
                result = new AeroSimReviewInspection.Result
                {
                    Success = captured,
                    Status = captured ? "OK" : (message != null && message.StartsWith("NOT_SUPPORTED", StringComparison.Ordinal) ? "NOT_SUPPORTED" : "CAPTURE_FAILED"),
                    Message = message
                };
                if (captured)
                {
                    result.Artifacts.Add(artifact);
                }
            }
            else if (request.tool == "capture_diagnostic_bundle")
            {
                result = AeroSimReviewInspection.CaptureDiagnosticBundle(request, runDirectory);
            }
            else
            {
                result = AeroSimReviewInspection.Execute(request, runDirectory);
            }

            return new ReviewResponse
            {
                requestId = request.id,
                tool = request.tool,
                success = result.Success,
                status = result.Status,
                message = result.Message,
                runId = runId,
                evidenceDirectory = RelativeToProject(runDirectory),
                transactionId = request.transactionId,
                artifacts = result.Artifacts.Select(name => RelativeToProject(RestrictedPath(runDirectory, name))).ToArray(),
                findings = result.Findings ?? Array.Empty<ReviewFinding>()
            };
        }

        private static ReviewResponse FailureResponse(ReviewRequest request, string runId, string runDirectory, string status, string message, Exception exception)
        {
            return new ReviewResponse
            {
                requestId = request != null ? request.id : string.Empty,
                tool = request != null ? request.tool : string.Empty,
                success = false,
                status = status,
                message = message,
                runId = runId,
                evidenceDirectory = RelativeToProject(runDirectory),
                transactionId = request != null ? request.transactionId : string.Empty,
                errorType = exception != null ? exception.GetType().FullName : string.Empty,
                stackTrace = exception != null ? exception.StackTrace : string.Empty
            };
        }

        private static void WriteManifest(ReviewRequest request, ReviewResponse response, string runId, string runDirectory)
        {
            EvidenceManifest manifest = new EvidenceManifest
            {
                runId = runId,
                generatedUtc = DateTime.UtcNow.ToString("o"),
                gitCommit = ReadGitCommit(),
                unityVersion = Application.unityVersion,
                scene = SceneManager.GetActiveScene().path,
                tool = request != null ? request.tool : string.Empty,
                requestId = request != null ? request.id : string.Empty,
                reviewStatus = "NOT_REVIEWED",
                gateStatus = "GATE_NOT_RUN",
                productionStatus = AeroSimReviewProtocol.GeneratedUnreviewed,
                artifacts = Directory.EnumerateFiles(runDirectory, "*", SearchOption.AllDirectories)
                    .Where(path => Path.GetFileName(path) != "request.json")
                    .Select(path => path.Substring(runDirectory.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace(Path.DirectorySeparatorChar, '/'))
                    .Concat(new[] { "manifest.json", "run-index.json", "response.json" })
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()
            };
            WriteTextAtomically(RestrictedPath(runDirectory, "manifest.json"), JsonUtility.ToJson(manifest, true));
        }

        private static void WriteRunIndex(ReviewRequest request, ReviewResponse response, string runId, string runDirectory)
        {
            ChangeRecord[] changes = Directory.Exists(Path.Combine(runDirectory, "changes"))
                ? Directory.EnumerateFiles(Path.Combine(runDirectory, "changes"), "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => JsonUtility.FromJson<ChangeRecord>(File.ReadAllText(path)))
                    .Where(change => change != null)
                    .ToArray()
                : Array.Empty<ChangeRecord>();
            ReviewRunIndex index = new ReviewRunIndex
            {
                runId = runId,
                requests = new[] { "request.json" },
                responses = new[] { "response.json" },
                changes = changes
            };
            WriteTextAtomically(RestrictedPath(runDirectory, "run-index.json"), JsonUtility.ToJson(index, true));
            WriteTextAtomically(RestrictedPath(runDirectory, "response.json"), JsonUtility.ToJson(response, true));
        }

        private static void EnsureQueueDirectories()
        {
            Directory.CreateDirectory(Inbox);
            Directory.CreateDirectory(Outbox);
            Directory.CreateDirectory(Runs);
            RejectReparsePoint(QueueRoot);
            RejectReparsePoint(Inbox);
            RejectReparsePoint(Outbox);
            RejectReparsePoint(Runs);
        }

        private static string RestrictedPath(string root, params string[] segments)
        {
            string candidate = segments.Aggregate(root, (current, segment) => Path.Combine(current, segment));
            string full = Path.GetFullPath(candidate);
            EnsureContained(root, full);
            return full;
        }

        private static void EnsureContained(string root, string candidate)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidate);
            if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Path escapes the AeroSim Review queue root: " + candidate);
            }
        }

        private static void RejectReparsePoint(string path)
        {
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Review queue directories must not be symbolic links or reparse points: " + path);
            }
        }

        private static string RelativeToProject(string path)
        {
            string project = ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(project, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Evidence path is outside the Unity project.");
            }
            return full.Substring(project.Length).Replace('\\', '/');
        }

        private static string SanitizeFileStem(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "request" : value;
            HashSet<char> invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            string result = new string(source.Select(character => invalid.Contains(character) || character == '/' || character == '\\' ? '_' : character).ToArray());
            result = result.Trim().Trim('.');
            if (result.Length == 0)
            {
                result = "request";
            }
            return result.Length <= 80 ? result : result.Substring(0, 80);
        }

        private static string ReadGitCommit()
        {
            try
            {
                string git = RestrictedProjectPath(".git");
                string headPath = RestrictedProjectPath(".git", "HEAD");
                if (!File.Exists(headPath))
                {
                    return string.Empty;
                }
                string head = File.ReadAllText(headPath).Trim();
                const string prefix = "ref: ";
                if (!head.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return head;
                }
                string reference = head.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                string referencePath = Path.GetFullPath(Path.Combine(git, reference));
                EnsureContained(git, referencePath);
                return File.Exists(referencePath) ? File.ReadAllText(referencePath).Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string RestrictedProjectPath(params string[] segments)
        {
            string path = segments.Aggregate(ProjectRoot, (current, segment) => Path.Combine(current, segment));
            string full = Path.GetFullPath(path);
            EnsureContained(ProjectRoot, full);
            return full;
        }
    }
}
#endif
