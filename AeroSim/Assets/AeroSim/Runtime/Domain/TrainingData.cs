using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AeroSim.Domain
{
    public enum Page { Home, Onboarding, Controller, Academy, FreeFlight, Challenges, Hangar, Tuning, OsdEditor, Dashboard, Catalog, Brief, Preflight, MissionEditor, Instructor, Readiness, Settings, Records, Results, Replay, Report, Flight }

    public static class AeroSimQuality
    {
        public const int Low = 0, Medium = 1, High = 2, Ultra = 3;
        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };
        public static int Normalize(int value) => Mathf.Clamp(value, Low, Ultra);
        public static string Name(int value) => Names[Normalize(value)];
        public static int Parse(string value)
        {
            int index = Array.IndexOf(Names, value);
            return index >= 0 ? index : High;
        }
    }

    public static class FlightWeather
    {
        public static readonly string[] Names = { "Clear", "Overcast", "Rain" };
        public static bool IsSupported(string value) => Names.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    [Serializable] public class Calibration
    {
        public string device = "";
        public string savedUtc = "";
        public float deadzone = .08f;
        public int throttleAxis = 1, yawAxis = 0, pitchAxis = 3, rollAxis = 2;
        public bool reverseThrottle, reverseYaw, reversePitch, reverseRoll;
        public float[] center = new float[4];
        public float[] minimum = { -1, -1, -1, -1 };
        public float[] maximum = { 1, 1, 1, 1 };
    }

    [Serializable] public sealed class FovViewDefinition
    {
        public int schemaVersion;
        public string viewId;
        public string displayName;
        public float minimumDeg;
        public float maximumDeg;
        public float defaultDeg;
        public float[] presetsDeg;

        public FovViewDefinition(int schemaVersion, string viewId, string displayName, float minimumDeg, float maximumDeg, float defaultDeg, params float[] presetsDeg)
        {
            this.schemaVersion = schemaVersion;
            this.viewId = viewId;
            this.displayName = displayName;
            this.minimumDeg = minimumDeg;
            this.maximumDeg = maximumDeg;
            this.defaultDeg = defaultDeg;
            this.presetsDeg = presetsDeg;
        }
    }

    public static class FovViewDefinitions
    {
        public const int CurrentVersion = 1;
        public static readonly FovViewDefinition[] All =
        {
            new(CurrentVersion, "fpv", "FPV / Pilot View", 45, 95, 60, 50, 60, 75),
            new(CurrentVersion, "near-follow", "Near Follow View", 50, 100, 70, 60, 70, 85),
            new(CurrentVersion, "cockpit", "Cockpit / Driver View", 35, 85, 55, 45, 55, 70),
            new(CurrentVersion, "gimbal", "Gimbal Close-up", 30, 65, 45, 35, 45, 55),
            new(CurrentVersion, "far-follow", "Far Follow View", 45, 95, 65, 55, 65, 80),
            new(CurrentVersion, "external", "External Observer", 35, 100, 60, 45, 60, 80)
        };

        public static FovViewDefinition Find(string viewId) => All.FirstOrDefault(value => value.viewId == viewId);
    }

    [Serializable] public class CameraFovSetting
    {
        public int schemaVersion = 1;
        public int definitionVersion = FovViewDefinitions.CurrentVersion;
        public string viewId = "";
        public string axis = "vertical";
        public float valueDeg;
        public string source = "default";
        public float displayAspectAtSave;
        public string updatedAt = "";
    }

    [Serializable] public class UserSettings
    {
        public string units = "U.S. Customary";
        public string pilotName = "Local Pilot";
        public string purpose = "Recreational";
        public float sensitivity = 1, cameraAngle = 25, expo = .3f, rate = 360, thrust = 2.4f;
        public float volume = .45f, textScale = 1;
        public string uiScale = "Auto", hudScale = "100%";
        public bool showTouchControls;
        public int quality = 2, targetFps = 60;
        public bool highContrast, reducedMotion, showControls = true, captions = true;
        public bool osdSpeed = true, osdAltitude = true, osdTimer = true, osdBattery = true;
        public List<CameraFovSetting> cameraFov = new();
        // Read only during migration from the pre-v4 horizontal-FOV profile shape.
        public float horizontalFov, fovAspect;
        public Vector2 speedPosition = new(.6f, .78f), altitudePosition = new(.23f, .78f), timerPosition = new(.46f, .13f), batteryPosition = new(.78f, .32f);
        public Calibration calibration = new();
    }

    [Serializable] public class MissionDefinition
    {
        public string id = "bridge-inspection", title = "Bridge Inspection", environment = "City Park";
        public float rthAltitude = 25, windMph = 4, timeLimit = 300, safeDistance = 5;
        public List<Vector3> targets = new() { new(0, 15, 35), new(20, 18, 55), new(-20, 16, 65) };
        public List<string> targetNames = new() { "North pier", "Deck joint", "South pier" };
        public string instructorNote = "";
    }

    [Serializable] public class FlightConfig
    {
        public string title = "Free Flight", courseId = "", workspace = "Aerial", environment = "Pacific Coast", aircraft = "Camera Drone", mode = "Normal", weather = "Clear";
        public float windMph = 6, timeOfDay = 16.5f, rthAltitude = 25;
        public bool race, professional, demo;
        public string objective = "Explore the training environment. Return home and land when you are ready.";
        public string task = "explore";
        public float targetAltitude = 10, requiredTime = 8;
        public MissionDefinition mission;
    }

    [Serializable] public class TelemetryFrame
    {
        public float time, battery, speed;
        public Vector3 position, rotation;
        public Vector4 input;
    }
    [Serializable] public class FlightEvent { public float time; public string message; public string kind; public string photoEvidenceId; }
    [Serializable] public class PhotoEvidence
    {
        public string id, sessionId, fileName, capturedUtc, missionId, targetName;
        public string status = "pending", failureReason = "", imageQuality = "not-evaluated";
        public int sequence, targetIndex = -1, width, height;
        public float time, targetDistance, targetFacingAngle;
        public bool hasTarget;
        public Vector3 aircraftPosition, aircraftRotation, cameraPosition, cameraRotation, targetPosition;
    }
    [Serializable] public class AircraftSessionSnapshot
    {
        public string catalogId, displayName, catalogVersion, visualVersion, physicsVersion;
        public bool availableAtCapture;
        public List<string> capabilities = new();
    }
    [Serializable] public class SessionRecord
    {
        public FlightConfig config;
        public AircraftSessionSnapshot aircraftSnapshot;
        public string id, startedUtc, title, workspace, environment, aircraft, mode, courseId, physicsVersion = "AeroSim-0.2";
        public int score, collisions, photos, gates;
        public float duration, distance, maxAltitude;
        public bool passed;
        public string feedback, instructorNote = "";
        public List<TelemetryFrame> frames = new();
        public List<FlightEvent> events = new();
        public List<PhotoEvidence> photoEvidence = new();
    }
    [Serializable] public class CourseProgress { public string id; public int bestScore, attempts; }
    [Serializable] public class Profile
    {
        public int schemaVersion = 4;
        public bool onboarded;
        public string workspace = "Aerial", selectedAircraft = "mini4";
        public string selectedAircraftCatalogVersion = "", selectedAircraftVisualVersion = "", selectedAircraftPhysicsVersion = "";
        public string selectedAircraftSource = "default", selectedAircraftUpdatedUtc = "";
        public string selectedAircraftFallbackFrom = "", selectedAircraftResolution = "current";
        public UserSettings settings = new();
        public List<CourseProgress> courses = new();
        public List<string> sessions = new();
        public List<string> assignedCourses = new();
        public List<MissionDefinition> missions = new();
        public List<string> readinessCompleted = new();
    }

    public static class LocalStore
    {
        private const int CurrentSchemaVersion = 4;
        private const string DefaultAircraftId = "mini4";
        public const float MinimumVerticalFov = 30f;
        public const float MaximumVerticalFov = 100f;
        private static string _lastSelectedAircraftId;
        public static string DirectoryPath { get; private set; }
        public static Profile Profile { get; private set; }
        public static bool SelectedAircraftNeedsConfirmation =>
            Profile != null && string.Equals(Profile.selectedAircraftResolution, "fallback", StringComparison.Ordinal);

        public static float DefaultVerticalFov(string viewId) => Definition(viewId).defaultDeg;
        public static FovViewDefinition Definition(string viewId) => FovViewDefinitions.Find(NormalizeFovViewId(viewId)) ?? FovViewDefinitions.Find("fpv");
        public static IReadOnlyList<string> FovDiagnostics => _fovDiagnostics;
        private static readonly List<string> _fovDiagnostics = new();

        public static float GetVerticalFov(string viewId)
        {
            var definition = Definition(viewId);
            if (Profile?.settings == null) return definition.defaultDeg;
            var setting = Profile.settings.cameraFov?.Find(value => value != null && NormalizeFovViewId(value.viewId) == definition.viewId);
            return IsValidVerticalFov(definition, setting?.valueDeg ?? float.NaN) ? setting.valueDeg : definition.defaultDeg;
        }

        public static void SetVerticalFov(string viewId, float valueDeg, string source, float displayAspect)
        {
            var definition = Definition(viewId);
            Profile.settings.cameraFov ??= new List<CameraFovSetting>();
            var setting = Profile.settings.cameraFov.Find(value => value != null && NormalizeFovViewId(value.viewId) == definition.viewId);
            if (setting == null)
            {
                setting = new CameraFovSetting { viewId = definition.viewId };
                Profile.settings.cameraFov.Add(setting);
            }
            setting.schemaVersion = 1;
            setting.definitionVersion = definition.schemaVersion;
            setting.viewId = definition.viewId;
            setting.axis = "vertical";
            if (!IsValidVerticalFov(definition, valueDeg))
            {
                RecordFovDiagnostic("Invalid vertical FOV for '" + definition.viewId + "'; restored " + definition.defaultDeg + "° default.");
                valueDeg = definition.defaultDeg;
                source = "default";
            }
            setting.valueDeg = valueDeg;
            setting.source = string.IsNullOrEmpty(source) ? "custom" : source;
            setting.displayAspectAtSave = IsFinite(displayAspect) && displayAspect > 0 ? displayAspect : 0;
            setting.updatedAt = DateTime.UtcNow.ToString("O");
        }

        public static void ResetVerticalFov(string viewId) => SetVerticalFov(viewId, DefaultVerticalFov(viewId), "default", CurrentAspect());
        public static void ResetAllVerticalFov()
        {
            foreach (var definition in FovViewDefinitions.All) ResetVerticalFov(definition.viewId);
        }

        public static bool IsValidVerticalFov(string viewId, float value) => IsValidVerticalFov(Definition(viewId), value);
        public static void RecordFovDiagnostic(string message)
        {
            _fovDiagnostics.Add(DateTime.UtcNow.ToString("O") + " " + message);
            if (_fovDiagnostics.Count > 32) _fovDiagnostics.RemoveAt(0);
            Debug.LogWarning(message);
        }

        private static string NormalizeFovViewId(string viewId) => viewId == "chase" ? "near-follow" : viewId;
        private static float CurrentAspect() => Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsValidVerticalFov(FovViewDefinition definition, float value) => IsFinite(value) && value >= definition.minimumDeg && value <= definition.maximumDeg;
        private static void NormalizeGraphicsSettings() => Profile.settings.quality = AeroSimQuality.Normalize(Profile.settings.quality);

        private static void NormalizeCameraFov()
        {
            var settings = Profile.settings;
            settings.cameraFov ??= new List<CameraFovSetting>();
            foreach (var legacy in settings.cameraFov.Where(value => value != null && value.viewId == "chase")) legacy.viewId = "near-follow";
            bool migrateHorizontal = Profile.schemaVersion < 4 && IsFinite(settings.horizontalFov) && settings.horizontalFov > 0 &&
                IsFinite(settings.fovAspect) && settings.fovAspect > 0;
            float migrated = migrateHorizontal
                ? Mathf.Rad2Deg * 2f * Mathf.Atan(Mathf.Tan(settings.horizontalFov * Mathf.Deg2Rad * .5f) / settings.fovAspect)
                : float.NaN;
            if (migrateHorizontal) RecordFovDiagnostic("Migrating legacy horizontal FOV using saved aspect " + settings.fovAspect.ToString("F3", CultureInfo.InvariantCulture) + ".");
            foreach (var definition in FovViewDefinitions.All)
            {
                var matches = settings.cameraFov.Where(value => value != null && value.viewId == definition.viewId).ToList();
                var setting = matches.FirstOrDefault();
                foreach (var duplicate in matches.Skip(1)) settings.cameraFov.Remove(duplicate);
                if (setting == null)
                {
                    bool validMigration = migrateHorizontal && IsValidVerticalFov(definition, migrated);
                    SetVerticalFov(definition.viewId, validMigration ? migrated : definition.defaultDeg, validMigration ? "calculated" : "default", validMigration ? settings.fovAspect : CurrentAspect());
                    continue;
                }
                if (!IsValidVerticalFov(definition, setting.valueDeg))
                {
                    RecordFovDiagnostic("Stored vertical FOV for '" + definition.viewId + "' is illegal; restoring its default.");
                    SetVerticalFov(definition.viewId, definition.defaultDeg, "default", CurrentAspect());
                    continue;
                }
                setting.schemaVersion = 1;
                setting.definitionVersion = definition.schemaVersion;
                setting.axis = "vertical";
                if (string.IsNullOrEmpty(setting.source)) setting.source = "custom";
            }
            settings.cameraFov.RemoveAll(value => value == null || FovViewDefinitions.Find(value.viewId) == null);
            settings.horizontalFov = 0;
            settings.fovAspect = 0;
        }

        private static AircraftProfile FindAvailableAircraft(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return AircraftCatalog.All.FirstOrDefault(profile => profile.IsSelectable &&
                (string.Equals(profile.Id, value, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(profile.CatalogId, value, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(profile.Name, value, StringComparison.OrdinalIgnoreCase) ||
                 (profile.LegacyIds ?? Array.Empty<string>()).Any(alias =>
                     string.Equals(alias, value, StringComparison.OrdinalIgnoreCase))));
        }

        private static void NormalizeSelectedAircraft()
        {
            string stored = Profile.selectedAircraft;
            bool migrating = Profile.schemaVersion < CurrentSchemaVersion;
            bool persistedFallback = string.Equals(Profile.selectedAircraftResolution, "fallback", StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(Profile.selectedAircraftFallbackFrom);
            bool selectionChanged = !migrating && _lastSelectedAircraftId != null &&
                !string.Equals(stored, _lastSelectedAircraftId, StringComparison.OrdinalIgnoreCase);
            var selected = FindAvailableAircraft(stored);
            if (selected == null && string.Equals(stored, "Camera Drone", StringComparison.OrdinalIgnoreCase))
                selected = FindAvailableAircraft(DefaultAircraftId);
            if (selected == null)
            {
                Profile.selectedAircraftFallbackFrom = stored ?? "";
                Profile.selectedAircraftResolution = "fallback";
                selected = FindAvailableAircraft(DefaultAircraftId) ?? AircraftCatalog.All.First();
                Debug.LogWarning("Selected aircraft '" + (stored ?? "<null>") + "' is unavailable; using " + selected.Id + " until the pilot confirms another aircraft.");
            }
            else if (!persistedFallback || selectionChanged || !string.Equals(selected.Id, DefaultAircraftId, StringComparison.OrdinalIgnoreCase))
            {
                Profile.selectedAircraftFallbackFrom = "";
                Profile.selectedAircraftResolution = "current";
            }
            Profile.selectedAircraft = selected.CatalogId;
            Profile.selectedAircraftCatalogVersion = AircraftCatalog.CatalogVersion;
            Profile.selectedAircraftVisualVersion = selected.VisualVersion ?? selected.Texture ?? "";
            Profile.selectedAircraftPhysicsVersion = selected.PhysicsVersion ?? "AeroSim-0.2";
            if (migrating) Profile.selectedAircraftSource = "migration";
            else if (selectionChanged && (string.IsNullOrEmpty(Profile.selectedAircraftSource) ||
                     Profile.selectedAircraftSource == "default" || Profile.selectedAircraftSource == "migration"))
                Profile.selectedAircraftSource = "user";
            if (migrating || selectionChanged || string.IsNullOrEmpty(Profile.selectedAircraftUpdatedUtc))
                Profile.selectedAircraftUpdatedUtc = DateTime.UtcNow.ToString("O");
            Profile.schemaVersion = CurrentSchemaVersion;
            _lastSelectedAircraftId = Profile.selectedAircraft;
        }

        private static void EnsureAircraftSnapshot(SessionRecord record, bool capturedNow)
        {
            if (record.aircraftSnapshot != null) return;
            string identity = record.config?.aircraft ?? record.aircraft;
            var profile = FindAvailableAircraft(identity);
            record.aircraftSnapshot = new AircraftSessionSnapshot
            {
                catalogId = profile?.CatalogId ?? identity ?? "",
                displayName = profile?.Name ?? record.aircraft ?? identity ?? "",
                catalogVersion = capturedNow ? AircraftCatalog.CatalogVersion : "legacy-unversioned",
                visualVersion = capturedNow ? profile?.VisualVersion ?? profile?.Texture ?? "unavailable" : "legacy-unversioned",
                physicsVersion = capturedNow ? profile?.PhysicsVersion ?? (string.IsNullOrEmpty(record.physicsVersion) ? "AeroSim-0.2" : record.physicsVersion) : record.physicsVersion ?? "legacy-unversioned",
                availableAtCapture = capturedNow && profile != null,
                capabilities = capturedNow && profile != null
                    ? new List<string>(profile.Capabilities ?? Array.Empty<string>())
                    : new List<string>()
            };
        }
        public static void Initialize()
        {
            DirectoryPath = Path.Combine(Application.persistentDataPath, "AeroSim");
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--data-dir") DirectoryPath = args[i + 1];
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, "profile.json");
            try { Profile = File.Exists(path) ? JsonUtility.FromJson<Profile>(File.ReadAllText(path)) : new Profile(); }
            catch (Exception ex) { Debug.LogWarning("Profile recovery: " + ex.Message); Profile = new Profile(); }
            Profile ??= new Profile(); Profile.settings ??= new UserSettings(); Profile.settings.calibration ??= new Calibration();
            bool persistMigration = Profile.schemaVersion < CurrentSchemaVersion;
            int storedQuality = Profile.settings.quality;
            Profile.courses ??= new List<CourseProgress>(); Profile.sessions ??= new List<string>(); Profile.missions ??= new List<MissionDefinition>();
            Profile.assignedCourses ??= new List<string>(); Profile.readinessCompleted ??= new List<string>();
            NormalizeGraphicsSettings();
            NormalizeCameraFov();
            NormalizeSelectedAircraft();
            if (Profile.missions.Count == 0) Profile.missions.Add(new MissionDefinition());
            if (persistMigration || storedQuality != Profile.settings.quality) Save();
        }
        public static void Save()
        {
            NormalizeGraphicsSettings();
            NormalizeCameraFov();
            NormalizeSelectedAircraft();
            var path = Path.Combine(DirectoryPath, "profile.json");
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(Profile, true));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".backup");
            else File.Move(path + ".tmp", path);
        }
        public static void SaveSession(SessionRecord record)
        {
            if (record == null) throw new ArgumentException("A session record is required.");
            EvidencePaths.ValidateSessionId(record.id);
            EnsureAircraftSnapshot(record, true);
            File.WriteAllText(EvidencePaths.ResolveOutput(record.id + ".json"), JsonUtility.ToJson(record));
            if (!Profile.sessions.Contains(record.id)) Profile.sessions.Insert(0, record.id);
            if (!string.IsNullOrEmpty(record.courseId))
            {
                var progress = Profile.courses.Find(x => x.id == record.courseId);
                if (progress == null) { progress = new CourseProgress { id = record.courseId }; Profile.courses.Add(progress); }
                progress.attempts++; progress.bestScore = Math.Max(progress.bestScore, record.score);
            }
            Save();
        }
        public static void SaveNotes(SessionRecord record)
        {
            if (record == null) throw new ArgumentException("A session record is required.");
            EvidencePaths.ValidateSessionId(record.id);
            File.WriteAllText(EvidencePaths.ResolveOutput(record.id + ".json"), JsonUtility.ToJson(record));
            // Updating annotations is not a new flight attempt and must not modify course progress.
        }
        public static List<SessionRecord> Sessions()
        {
            var result = new List<SessionRecord>();
            foreach (var id in Profile.sessions.Take(100))
            {
                try
                {
                    EvidencePaths.ValidateSessionId(id);
                    var path = EvidencePaths.ResolveOutput(id + ".json");
                    if (!File.Exists(path)) continue;
                    var record = JsonUtility.FromJson<SessionRecord>(File.ReadAllText(path));
                    if (record == null || record.id != id) { Debug.LogWarning("Session identity does not match its file."); continue; }
                    record.frames ??= new List<TelemetryFrame>(); record.events ??= new List<FlightEvent>();
                    record.photoEvidence ??= new List<PhotoEvidence>(); // Old records have no evidence metadata; do not fabricate it.
                    EnsureAircraftSnapshot(record, false); // Preserve identity without inventing versions for a legacy record.
                    result.Add(record);
                }
                catch (Exception ex) { Debug.LogWarning("Session load: " + ex.Message); }
            }
            return result;
        }
        public static string ExportCsv(SessionRecord r)
        {
            if (r == null) throw new ArgumentException("A session record is required.");
            EvidencePaths.ValidateSessionId(r.id);
            var path = EvidencePaths.ResolveOutput(r.id + "-telemetry.csv");
            var lines = new List<string> { "time_s,x_m,y_m,z_m,speed_mps,battery_pct,throttle,yaw,pitch,roll" };
            foreach (var f in r.frames) lines.Add(string.Join(",", new[] {f.time,f.position.x,f.position.y,f.position.z,f.speed,f.battery,f.input.x,f.input.y,f.input.z,f.input.w}.Select(v => v.ToString("F3", CultureInfo.InvariantCulture))));
            File.WriteAllLines(path, lines); return path;
        }
        public static string ExportReport(SessionRecord r)
        {
            if (r == null) throw new ArgumentException("A session record is required.");
            EvidencePaths.ValidateSessionId(r.id);
            string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);
            var path = EvidencePaths.ResolveOutput(r.id + "-report.html");
            var events = string.Join("", r.events.Select(e => $"<tr><td>{e.time:F1}s</td><td>{Escape(e.kind)}</td><td>{Escape(e.message)}</td></tr>"));
            var evidenceRows = string.Join("", (r.photoEvidence ?? new List<PhotoEvidence>()).Where(photo => photo != null).Select(photo =>
            {
                bool available = EvidencePaths.ResolvePhoto(r, photo) != null;
                string media = available ? $"<a href='{Uri.EscapeDataString(photo.fileName)}'><img style='max-width:280px;height:auto' src='{Uri.EscapeDataString(photo.fileName)}' alt='Training capture {photo.sequence}'></a>" : Escape(photo.status == "saved" ? "Image file missing" : photo.failureReason);
                string target = photo.hasTarget ? Escape(photo.targetName) : "Free-flight capture";
                return $"<tr><td>{photo.sequence}</td><td>{photo.time.ToString("F1", CultureInfo.InvariantCulture)}s</td><td>{target}</td><td>{Escape(photo.status)}</td><td>{media}</td></tr>";
            }));
            string evidenceSection = string.IsNullOrEmpty(evidenceRows) ? "<p>This legacy record has no linked photo metadata. Capture counts alone do not establish image evidence.</p>" : $"<table><tr><th>Capture</th><th>Time</th><th>Target</th><th>Status</th><th>Evidence</th></tr>{evidenceRows}</table>";
            File.WriteAllText(path, $"<!doctype html><meta charset='utf-8'><title>AeroSim Training Report</title><style>body{{font:16px system-ui;max-width:900px;margin:48px auto;color:#162b3e}}h1{{font-size:36px}}table{{width:100%;border-collapse:collapse}}td,th{{padding:12px;border-bottom:1px solid #ccd7df}}.score{{font-size:64px;color:#0070cf}}@media print{{button{{display:none}}}}</style><button onclick='print()'>Print / Save as PDF</button><h1>{Escape(r.title)}</h1><p>AeroSim local training report · {Escape(r.startedUtc)} · {Escape(r.physicsVersion)}</p><div class='score'>{r.score}/100</div><p>{(r.passed ? "PASSED" : "PRACTICE AGAIN")} · {r.duration:F0}s · {r.collisions} collisions · {r.photos} saved captures</p><p>{Escape(r.feedback)}</p><h2>Event evidence</h2><table><tr><th>Time</th><th>Type</th><th>Event</th></tr>{events}</table><h2>Photo evidence</h2>{evidenceSection}<p>Images are simulator-camera captures. Photographic image quality is not evaluated. Keep image files beside this report when sharing it.</p><h2>Instructor notes</h2><p>{Escape(r.instructorNote)}</p><p>Training record only. This document is not an FAA certificate.</p>");
            return path;
        }
    }

    public sealed class Course
    {
        public string Id, Title, Category, Workspace, Objective, Task;
        public int Minutes;
        public Course(string id, string title, string category, string workspace, string task, string objective, int minutes = 4)
        { Id=id;Title=title;Category=category;Workspace=workspace;Task=task;Objective=objective;Minutes=minutes; }
        public FlightConfig Config() => new() { title=Title, courseId=Id, workspace=Workspace, objective=Objective, task=Task, mode=Workspace=="FPV"?"Manual":"Normal", aircraft=AircraftCatalog.Selected.Name, environment=Workspace=="FPV"?"Desert Range":"Pacific Coast", race=Task=="gates" };
    }

    public static class CourseCatalog
    {
        public static readonly List<Course> All = new()
        {
            new("a01","Know Your Sticks","Flight Basics","Aerial","move","Move 65 ft from the takeoff point, then return."),
            new("a02","Takeoff & Landing","Flight Basics","Aerial","land","Take off above 16 ft, then land on the home pad."),
            new("a03","Steady Hover","Flight Basics","Aerial","hover","Hold altitude between 26 and 39 ft for 8 seconds."),
            new("a04","Orientation","Flight Basics","Aerial","yaw","Rotate through 180 degrees while holding altitude."),
            new("a05","Rectangle Route","Flight Basics","Aerial","gates","Fly through the illuminated route gates in order."),
            new("a06","Altitude Control","Flight Basics","Aerial","altitude","Climb to 65 ft and hold for 8 seconds."),
            new("a07","Smooth Braking","Flight Basics","Aerial","brake","Reach 10 mph, then release the sticks and stop."),
            new("a08","Camera Capture","Flight Basics","Aerial","photo","Take off and capture 3 training photographs."),
            new("a09","Home Point","Flight Basics","Aerial","move","Fly 65 ft away and return within 10 ft of home."),
            new("a10","Return-to-Home","Flight Basics","Aerial","rth","Fly 65 ft away, start RTH, and let the aircraft land."),
            new("a11","Obstacle Awareness","Flight Basics","Aerial","gates","Navigate the gates without colliding with terrain."),
            new("a12","Aerial Skills Check","Flight Basics","Aerial","gates","Complete the full route and return home safely.",6),
            new("c01","Lateral Reveal","Camera Skills","Aerial","photo","Fly a smooth lateral route and capture 3 frames."),
            new("c02","Cinematic Orbit","Camera Skills","Aerial","gates","Follow the circular route around the landmark."),
            new("c03","Push & Pull","Camera Skills","Aerial","move","Move out and back while keeping the camera steady."),
            new("c04","Descent Transition","Camera Skills","Aerial","altitude","Climb above 65 ft and control your descent."),
            new("c05","Subject Tracking","Camera Skills","Aerial","gates","Follow the landmark route with controlled speed."),
            new("c06","One-Take Sequence","Camera Skills","Aerial","photo","Capture 3 usable photographs without a collision."),
            new("s01","Wind Recovery","Safety & Emergencies","Aerial","rth","Fly into the wind and return home."),
            new("s02","Low Battery","Safety & Emergencies","Aerial","rth","Practice RTH after a low-battery warning."),
            new("s03","Signal Loss","Safety & Emergencies","Aerial","rth","Trigger simulated signal loss and recover safely."),
            new("s04","Emergency Landing","Safety & Emergencies","Aerial","land","Take off and perform a controlled emergency landing."),
            new("f01","Throttle Control","FPV Foundations","FPV","hover","Use Normal mode first and hold altitude for 8 seconds."),
            new("f02","Roll & Pitch","FPV Foundations","FPV","move","Learn the aircraft response with a short flight."),
            new("f03","Coordinated Turns","FPV Foundations","FPV","yaw","Turn through 180 degrees with coordinated sticks."),
            new("f04","Camera Angle","FPV Foundations","FPV","photo","Fly with your chosen camera angle and capture a frame."),
            new("f05","First Gates","Manual Flight","FPV","gates","Fly through all 6 gates in order."),
            new("f06","Figure Eight","Manual Flight","FPV","gates","Practice the crossing route through each gate."),
            new("f07","Low-Level Flight","Manual Flight","FPV","gates","Follow the low-altitude course."),
            new("f08","Split-S","Acro Skills","FPV","acro","Roll and recover from an inverted attitude."),
            new("f09","Power Loop","Acro Skills","FPV","acro","Pitch through a full loop and recover above ground."),
            new("f10","Precision Gap","Acro Skills","FPV","gates","Use controlled throttle to pass the narrow course."),
            new("f11","Racing Lines","Race Ready","FPV","gates","Complete the course with minimal collisions."),
            new("f12","Timed Circuit","Race Ready","FPV","gates","Finish the full timed circuit.",5)
        };
        public static Course Find(string id) => All.Find(c => c.Id == id) ?? All[0];
    }
}
