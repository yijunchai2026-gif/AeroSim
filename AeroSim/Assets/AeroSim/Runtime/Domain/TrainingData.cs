using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AeroSim.Domain
{
    public enum Page { Home, Onboarding, Controller, Academy, FreeFlight, Challenges, Hangar, Tuning, OsdEditor, Dashboard, Catalog, Brief, Preflight, MissionEditor, Instructor, Readiness, Settings, Records, Results, Replay, Report, Flight }

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
    [Serializable] public class FlightEvent { public float time; public string message; public string kind; }
    [Serializable] public class SessionRecord
    {
        public FlightConfig config;
        public string id, startedUtc, title, workspace, environment, aircraft, mode, courseId, physicsVersion = "AeroSim-0.2";
        public int score, collisions, photos, gates;
        public float duration, distance, maxAltitude;
        public bool passed;
        public string feedback, instructorNote = "";
        public List<TelemetryFrame> frames = new();
        public List<FlightEvent> events = new();
    }
    [Serializable] public class CourseProgress { public string id; public int bestScore, attempts; }
    [Serializable] public class Profile
    {
        public int schemaVersion = 2;
        public bool onboarded;
        public string workspace = "Aerial", selectedAircraft = "mini4";
        public UserSettings settings = new();
        public List<CourseProgress> courses = new();
        public List<string> sessions = new();
        public List<string> assignedCourses = new();
        public List<MissionDefinition> missions = new();
        public List<string> readinessCompleted = new();
    }

    public static class LocalStore
    {
        public static string DirectoryPath { get; private set; }
        public static Profile Profile { get; private set; }
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
            Profile.courses ??= new List<CourseProgress>(); Profile.sessions ??= new List<string>(); Profile.missions ??= new List<MissionDefinition>();
            Profile.assignedCourses ??= new List<string>(); Profile.readinessCompleted ??= new List<string>();
            if (Profile.missions.Count == 0) Profile.missions.Add(new MissionDefinition());
        }
        public static void Save()
        {
            var path = Path.Combine(DirectoryPath, "profile.json");
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(Profile, true));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".backup");
            else File.Move(path + ".tmp", path);
        }
        public static void SaveSession(SessionRecord record)
        {
            File.WriteAllText(Path.Combine(DirectoryPath, record.id + ".json"), JsonUtility.ToJson(record));
            if (!Profile.sessions.Contains(record.id)) Profile.sessions.Insert(0, record.id);
            if (!string.IsNullOrEmpty(record.courseId))
            {
                var progress = Profile.courses.Find(x => x.id == record.courseId);
                if (progress == null) { progress = new CourseProgress { id = record.courseId }; Profile.courses.Add(progress); }
                progress.attempts++; progress.bestScore = Math.Max(progress.bestScore, record.score);
            }
            Save();
        }
        public static List<SessionRecord> Sessions()
        {
            var result = new List<SessionRecord>();
            foreach (var id in Profile.sessions.Take(100))
            {
                try { var path = Path.Combine(DirectoryPath, id + ".json"); if (File.Exists(path)) result.Add(JsonUtility.FromJson<SessionRecord>(File.ReadAllText(path))); }
                catch (Exception ex) { Debug.LogWarning("Session load: " + ex.Message); }
            }
            return result;
        }
        public static string ExportCsv(SessionRecord r)
        {
            var path = Path.Combine(DirectoryPath, r.id + "-telemetry.csv");
            var lines = new List<string> { "time_s,x_m,y_m,z_m,speed_mps,battery_pct,throttle,yaw,pitch,roll" };
            foreach (var f in r.frames) lines.Add(string.Join(",", new[] {f.time,f.position.x,f.position.y,f.position.z,f.speed,f.battery,f.input.x,f.input.y,f.input.z,f.input.w}.Select(v => v.ToString("F3", CultureInfo.InvariantCulture))));
            File.WriteAllLines(path, lines); return path;
        }
        public static string ExportReport(SessionRecord r)
        {
            string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);
            var path = Path.Combine(DirectoryPath, r.id + "-report.html");
            var events = string.Join("", r.events.Select(e => $"<tr><td>{e.time:F1}s</td><td>{Escape(e.kind)}</td><td>{Escape(e.message)}</td></tr>"));
            File.WriteAllText(path, $"<!doctype html><meta charset='utf-8'><title>AeroSim Training Report</title><style>body{{font:16px system-ui;max-width:900px;margin:48px auto;color:#162b3e}}h1{{font-size:36px}}table{{width:100%;border-collapse:collapse}}td,th{{padding:12px;border-bottom:1px solid #ccd7df}}.score{{font-size:64px;color:#0070cf}}@media print{{button{{display:none}}}}</style><button onclick='print()'>Print / Save as PDF</button><h1>{Escape(r.title)}</h1><p>AeroSim local training report · {Escape(r.startedUtc)} · {Escape(r.physicsVersion)}</p><div class='score'>{r.score}/100</div><p>{(r.passed ? "PASSED" : "PRACTICE AGAIN")} · {r.duration:F0}s · {r.collisions} collisions · {r.photos} captures</p><p>{Escape(r.feedback)}</p><h2>Event evidence</h2><table><tr><th>Time</th><th>Type</th><th>Event</th></tr>{events}</table><h2>Instructor notes</h2><p>{Escape(r.instructorNote)}</p><p>Training record only. This document is not an FAA certificate.</p>");
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
