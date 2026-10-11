using System;
using System.Collections.Generic;
using System.Linq;

namespace AeroSim.Domain
{
    public enum AirframeShape { Mini, Air, Mavic, Ducted, Racer, Micro }

    [Flags]
    public enum AircraftWorkspace
    {
        None = 0,
        Aerial = 1,
        Fpv = 2,
        Professional = 4,
        All = Aerial | Fpv | Professional
    }

    public enum AircraftCatalogStatus { Runtime, Candidate, Research, Locked, Deprecated }

    /// <summary>Versioned, deliberately approximate training values; they are not manufacturer flight-controller data.</summary>
    public sealed class AircraftFlightParameters
    {
        public float MassKg, MaxHorizontalSpeedMps, MaxAscentSpeedMps, MaxDescentSpeedMps;
        public float MaxTiltDegrees, MaxAngularSpeedDegreesPerSecond, NominalFlightTimeMinutes;

        public AircraftFlightParameters(float massKg, float maxHorizontalSpeedMps, float maxAscentSpeedMps,
            float maxDescentSpeedMps, float maxTiltDegrees, float maxAngularSpeedDegreesPerSecond,
            float nominalFlightTimeMinutes)
        {
            MassKg = massKg;
            MaxHorizontalSpeedMps = maxHorizontalSpeedMps;
            MaxAscentSpeedMps = maxAscentSpeedMps;
            MaxDescentSpeedMps = maxDescentSpeedMps;
            MaxTiltDegrees = maxTiltDegrees;
            MaxAngularSpeedDegreesPerSecond = maxAngularSpeedDegreesPerSecond;
            NominalFlightTimeMinutes = nominalFlightTimeMinutes;
        }
    }

    public sealed class AircraftProfile
    {
        // Id remains the persisted/runtime identity used by existing callers. CatalogId is the canonical v2 identity.
        public string Id, CatalogId, Name, ShortName, Brand, Family, Model, Category, Description, Texture, Reference, Source;
        public string MaterialTheme, VisualAssetId, VisualVersion, PhysicsProfileId, PhysicsVersion;
        public string IncompatibleReasonKey, ReviewStatus, PerformanceTier, Difficulty;
        public string[] LegacyIds, Capabilities, CompatibleTaskTags, InputRequirements, FlightModes, UseCases;
        public AirframeShape Shape;
        public AircraftWorkspace Workspaces;
        public AircraftCatalogStatus Status;
        public AircraftFlightParameters FlightParameters;
        public float VisualScale, ChaseDistance;
        public bool Fpv, IsDownloaded, HasVisualAsset, HasPhysicsProfile, HasThumbnail, HasCompatibilityProfile;

        // Kept for source compatibility with the original catalog model.
        public AircraftProfile(string id, string name, string category, AirframeShape shape, string texture,
            float scale, float chase, bool fpv, string description, string reference, string source = "")
            : this(id, id, name, name, "", "", name, category, shape, texture, scale, chase, fpv,
                description, reference, source, fpv ? AircraftWorkspace.Fpv : AircraftWorkspace.Aerial,
                AircraftCatalogStatus.Runtime, new string[0], new string[0], new string[0], new string[0],
                new string[0], new string[0], "legacy", "legacy-visual", "1", "legacy-physics", "1",
                "", "legacy", "Medium", "Intermediate", null, true, true, true, true, true)
        {
        }

        public AircraftProfile(string id, string catalogId, string name, string shortName, string brand,
            string family, string model, string category, AirframeShape shape, string texture, float scale,
            float chase, bool fpv, string description, string reference, string source,
            AircraftWorkspace workspaces, AircraftCatalogStatus status, string[] legacyIds,
            string[] capabilities, string[] compatibleTaskTags, string[] inputRequirements,
            string[] flightModes, string[] useCases, string materialTheme, string visualAssetId,
            string visualVersion, string physicsProfileId, string physicsVersion, string incompatibleReasonKey,
            string reviewStatus, string performanceTier, string difficulty,
            AircraftFlightParameters flightParameters, bool isDownloaded, bool hasVisualAsset,
            bool hasPhysicsProfile, bool hasThumbnail, bool hasCompatibilityProfile)
        {
            Id = id;
            CatalogId = catalogId;
            Name = name;
            ShortName = shortName;
            Brand = brand;
            Family = family;
            Model = model;
            Category = category;
            Shape = shape;
            Texture = texture;
            VisualScale = scale;
            ChaseDistance = chase;
            Fpv = fpv;
            Description = description;
            Reference = reference;
            Source = source;
            Workspaces = workspaces;
            Status = status;
            LegacyIds = legacyIds ?? new string[0];
            Capabilities = capabilities ?? new string[0];
            CompatibleTaskTags = compatibleTaskTags ?? new string[0];
            InputRequirements = inputRequirements ?? new string[0];
            FlightModes = flightModes ?? new string[0];
            UseCases = useCases ?? new string[0];
            MaterialTheme = materialTheme;
            VisualAssetId = visualAssetId;
            VisualVersion = visualVersion;
            PhysicsProfileId = physicsProfileId;
            PhysicsVersion = physicsVersion;
            IncompatibleReasonKey = incompatibleReasonKey;
            ReviewStatus = reviewStatus;
            PerformanceTier = performanceTier;
            Difficulty = difficulty;
            FlightParameters = flightParameters;
            IsDownloaded = isDownloaded;
            HasVisualAsset = hasVisualAsset;
            HasPhysicsProfile = hasPhysicsProfile;
            HasThumbnail = hasThumbnail;
            HasCompatibilityProfile = hasCompatibilityProfile;
        }

        public bool IsRuntime => Status == AircraftCatalogStatus.Runtime;
        public bool IsSelectable => IsRuntime && IsDownloaded && HasVisualAsset && HasPhysicsProfile &&
                                    HasThumbnail && HasCompatibilityProfile;
    }

    public static class AircraftCatalog
    {
        public const string CatalogVersion = "2.0.0";

        private static readonly string[] CameraModes = { "Normal", "Sport", "Cinematic" };
        private static readonly string[] FpvModes = { "Normal", "Sport", "Manual" };
        private static readonly string[] StandardInput = { "four-axis" };
        private static readonly string[] CameraTasks = { "free-flight", "camera", "course", "challenge" };

        // Artist-built procedural visual approximations, not licensed meshes or flight-controller emulation.
        // Existing six Id values intentionally remain unchanged for saved profiles and replays.
        public static readonly AircraftProfile[] All = {
            Profile("mini4", "dji-mini-4-pro", "DJI Mini 4 Pro", "Mini 4 Pro", "Mini", "Mini 4 Pro",
                "LIGHTWEIGHT CAMERA", AirframeShape.Mini, "mini-shell-v3", .24f, 2.4f, false,
                "Light-gray folding frame · single gimbal camera · compact arms",
                "Mini-series visual reference", "https://www.dji.com/mini-4-pro/specs",
                AircraftWorkspace.Aerial, new[] { "Camera Drone" },
                new[] { "camera", "gimbal", "obstacle-avoidance", "return-to-home" }, CameraTasks,
                CameraModes, new[] { "entry aerial photography", "lightweight travel" },
                "warm-light polymer / dark sensor glass", "Easy",
                Flight(.249f, 16f, 5f, 5f, 35f, 150f, 34f)),
            Profile("air3s", "dji-air-3s", "DJI Air 3S", "Air 3S", "Air", "Air 3S",
                "DUAL-CAMERA AERIAL", AirframeShape.Air, "air-shell-v3", .32f, 3.1f, false,
                "Cool-gray shell · stacked dual cameras · forward sensor module",
                "Reference takeoff weight: 1.60 lb / 724 g", "https://www.dji.com/air-3s/specs",
                AircraftWorkspace.Aerial, null,
                new[] { "dual-camera", "gimbal", "obstacle-avoidance", "return-to-home" }, CameraTasks,
                CameraModes, new[] { "dual-camera aerial photography", "obstacle training" },
                "cool-gray polymer / graphite optics", "Intermediate",
                Flight(.724f, 21f, 6f, 6f, 36f, 170f, 40f)),
            Profile("mavic3p", "dji-mavic-3-pro", "DJI Mavic 3 Pro", "Mavic 3 Pro", "Mavic", "Mavic 3 Pro",
                "TRIPLE-CAMERA FLAGSHIP", AirframeShape.Mavic, "mavic-shell-v3", .35f, 3.4f, false,
                "Charcoal elongated body · three-lens gimbal · broad folding arms",
                "Reference takeoff weight: 2.11 lb / 958 g", "https://www.dji.com/mavic-3-pro/specs",
                AircraftWorkspace.Aerial | AircraftWorkspace.Professional, null,
                new[] { "triple-camera", "gimbal", "obstacle-avoidance", "return-to-home" }, CameraTasks,
                CameraModes, new[] { "multi-focal photography", "cinematic aerial work" },
                "charcoal polymer / satin metal / optical glass", "Advanced",
                Flight(.958f, 21f, 8f, 6f, 37f, 180f, 43f)),
            Profile("avata2", "dji-avata-2", "DJI Avata 2", "Avata 2", "Avata", "Avata 2",
                "GUARDED FPV", AirframeShape.Ducted, "avata-shell-v3", .24f, 2.2f, true,
                "Four integrated prop guards · dark shell · front FPV camera",
                "Reference takeoff weight: 0.83 lb / approx. 377 g", "https://www.dji.com/avata-2/specs",
                AircraftWorkspace.Fpv, null,
                new[] { "fpv-camera", "propeller-guards", "manual-mode", "return-to-home" },
                new[] { "free-flight", "fpv", "course", "challenge" }, FpvModes,
                new[] { "guarded FPV", "immersive training" },
                "graphite polymer / integrated guards / optical glass", "Intermediate",
                Flight(.377f, 27f, 9f, 9f, 55f, 260f, 23f)),
            Profile("racer", "generic-5in-racer", "5-inch Racer", "5-inch Racer", "AeroSim", "Generic Racer",
                "GENERIC OPEN FPV", AirframeShape.Racer, "carbon-weave-v3", .28f, 2.6f, true,
                "Exposed carbon X-frame · battery strap · three-blade props",
                "Original AeroSim training model", "",
                AircraftWorkspace.Fpv, null,
                new[] { "fpv-camera", "manual-mode", "rates", "race-timing" },
                new[] { "free-flight", "fpv", "race", "gates", "challenge" }, new[] { "Manual", "Acro" },
                new[] { "racing", "freestyle", "rates practice" },
                "2x2 carbon weave / anodized metal / red accent", "Expert",
                Flight(.68f, 38f, 14f, 12f, 75f, 520f, 8f)),
            Profile("micro", "generic-micro-trainer", "Micro Trainer", "Micro Trainer", "AeroSim", "Micro Trainer",
                "GENERIC PRACTICE FPV", AirframeShape.Micro, "mini-shell-v3", .17f, 1.9f, true,
                "Small guarded trainer · orange canopy · four compact ducts",
                "Original AeroSim training model", "",
                AircraftWorkspace.Fpv | AircraftWorkspace.Aerial, null,
                new[] { "fpv-camera", "propeller-guards", "beginner-mode" },
                new[] { "free-flight", "fpv", "course" }, new[] { "Normal", "Manual" },
                new[] { "indoor practice", "low-speed FPV introduction" },
                "orange polymer canopy / carbon subframe", "Easy",
                Flight(.09f, 8f, 3f, 3f, 40f, 220f, 7f)),
            Profile("dji-mavic-4-pro", "dji-mavic-4-pro", "DJI Mavic 4 Pro", "Mavic 4 Pro", "Mavic", "Mavic 4 Pro",
                "FLAGSHIP TRIPLE-CAMERA AERIAL", AirframeShape.Mavic, "mavic-shell-v3", .37f, 3.6f, false,
                "Flagship folding aerial platform · triple-camera gimbal · panoramic gimbal training profile",
                "Procedural L1 approximation; independent production asset revisions remain gated",
                "https://www.dji.com/sg/mavic-4-pro", AircraftWorkspace.Aerial | AircraftWorkspace.Professional, null,
                new[] { "triple-camera", "gimbal", "panoramic-gimbal", "obstacle-avoidance", "return-to-home" },
                CameraTasks, CameraModes, new[] { "flagship aerial photography", "multi-focal cinematography" },
                "graphite magnesium / dark polymer / optical glass", "Advanced",
                Flight(1.063f, 25f, 10f, 8f, 40f, 190f, 45f), AircraftCatalogStatus.Candidate,
                "aircraft.locked.runtime_visual_assets_not_ready"),
            Profile("dji-matrice-350-rtk", "dji-matrice-350-rtk", "DJI Matrice 350 RTK", "Matrice 350 RTK",
                "Matrice", "Matrice 350 RTK", "PROFESSIONAL INSPECTION", AirframeShape.Air, "air-shell-v3", .52f,
                5.0f, false, "Industrial quadrotor training profile · payload and inspection workflow emphasis",
                "Procedural L1 professional-training approximation", "https://enterprise.dji.com/matrice-350-rtk",
                AircraftWorkspace.Professional, null,
                new[] { "gimbal", "payload", "inspection", "search-and-rescue", "rtk", "obstacle-avoidance" },
                new[] { "free-flight", "inspection", "search-and-rescue", "evidence-capture" },
                new[] { "Normal", "Sport" }, new[] { "infrastructure inspection", "search and rescue", "payload training" },
                "industrial charcoal polymer / aluminum / safety accent", "Advanced",
                Flight(6.47f, 23f, 6f, 5f, 30f, 120f, 44f), AircraftCatalogStatus.Candidate,
                "aircraft.locked.runtime_visual_assets_not_ready"),
            Profile("dji-agras-t50", "dji-agras-t50", "DJI Agras T50", "Agras T50", "Agras", "Agras T50",
                "PROFESSIONAL AGRICULTURE", AirframeShape.Air, "air-shell-v3", .62f, 5.8f, false,
                "Agricultural multirotor training profile · route, payload and operation-safety emphasis",
                "Procedural L1 professional-training approximation", "https://ag.dji.com/t50",
                AircraftWorkspace.Professional, null,
                new[] { "agriculture", "payload", "route-planning", "terrain-following", "obstacle-avoidance" },
                new[] { "free-flight", "agriculture", "route", "safety" }, new[] { "Normal", "Operation" },
                new[] { "agricultural routes", "payload awareness", "operation safety" },
                "white engineering polymer / black structure / green safety accent", "Advanced",
                Flight(52f, 10f, 3f, 3f, 20f, 70f, 18f), AircraftCatalogStatus.Candidate,
                "aircraft.locked.runtime_visual_assets_not_ready"),
            Profile("dji-inspire-3", "dji-inspire-3", "DJI Inspire 3", "Inspire 3", "Inspire", "Inspire 3",
                "PROFESSIONAL CINEMA", AirframeShape.Mavic, "mavic-shell-v3", .48f, 4.5f, false,
                "Cinema aircraft training profile · gimbal, precision route and dual-operator workflow emphasis",
                "Procedural L1 professional-training approximation", "https://www.dji.com/inspire-3",
                AircraftWorkspace.Aerial | AircraftWorkspace.Professional, null,
                new[] { "cinema-camera", "gimbal", "dual-operator", "precision-route", "rtk", "obstacle-avoidance" },
                new[] { "free-flight", "camera", "cinema", "precision-route" }, CameraModes,
                new[] { "cinema production", "dual-operator coordination", "precision camera routes" },
                "black magnesium / carbon composite / optical glass", "Expert",
                Flight(3.995f, 26f, 8f, 8f, 35f, 140f, 25f), AircraftCatalogStatus.Candidate,
                "aircraft.locked.runtime_visual_assets_not_ready")
        };

        // Future candidate/research entries can be appended here without entering legacy runtime UI via All.
        public static readonly AircraftProfile[] Entries = All;

        public static AircraftProfile Resolve(string value)
        {
            bool ignored;
            return Resolve(value, out ignored);
        }

        /// <summary>Compatibility resolver. Unknown values retain the legacy Mini 4 Pro fallback.</summary>
        public static AircraftProfile Resolve(string value, out bool usedFallback)
        {
            AircraftProfile profile;
            if (TryResolve(value, out profile))
            {
                usedFallback = false;
                return profile;
            }

            usedFallback = true;
            return All[0];
        }

        /// <summary>Strict lookup for new selection/migration code; never silently substitutes another aircraft.</summary>
        public static bool TryResolve(string value, out AircraftProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string key = value.Trim();
            profile = Entries.FirstOrDefault(p => Matches(p.Id, key) || Matches(p.CatalogId, key) ||
                Matches(p.Name, key) || Matches(p.ShortName, key) || p.LegacyIds.Any(id => Matches(id, key)));
            return profile != null;
        }

        public static AircraftProfile[] Search(string query, AircraftWorkspace workspace = AircraftWorkspace.All,
            bool runtimeOnly = false)
        {
            string key = (query ?? "").Trim();
            return Filter(workspace, runtimeOnly ? AircraftCatalogStatus.Runtime : (AircraftCatalogStatus?)null)
                .Where(p => key.Length == 0 || Contains(p.Name, key) || Contains(p.ShortName, key) ||
                    Contains(p.Family, key) || Contains(p.Model, key) || Contains(p.Category, key) ||
                    p.Capabilities.Any(c => Contains(c, key)) || p.UseCases.Any(u => Contains(u, key)))
                .ToArray();
        }

        public static AircraftProfile[] Filter(AircraftWorkspace workspace = AircraftWorkspace.All,
            AircraftCatalogStatus? status = AircraftCatalogStatus.Runtime, string[] requiredCapabilities = null)
        {
            IEnumerable<AircraftProfile> result = Entries;
            if (workspace != AircraftWorkspace.All)
                result = result.Where(p => (p.Workspaces & workspace) != 0);
            if (status.HasValue)
                result = result.Where(p => p.Status == status.Value);
            if (requiredCapabilities != null && requiredCapabilities.Length > 0)
                result = result.Where(p => SupportsCapabilities(p, requiredCapabilities));
            return result.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static bool SupportsCapabilities(AircraftProfile profile, IEnumerable<string> requiredCapabilities)
        {
            if (profile == null || requiredCapabilities == null) return profile != null;
            return requiredCapabilities.Where(c => !string.IsNullOrWhiteSpace(c))
                .All(required => profile.Capabilities.Any(actual => Matches(actual, required.Trim())));
        }

        public static bool SupportsTask(AircraftProfile profile, string taskTag,
            IEnumerable<string> requiredCapabilities = null)
        {
            if (profile == null || !profile.IsSelectable) return false;
            bool taskMatches = string.IsNullOrWhiteSpace(taskTag) ||
                               profile.CompatibleTaskTags.Any(tag => Matches(tag, taskTag.Trim()));
            return taskMatches && SupportsCapabilities(profile, requiredCapabilities);
        }

        public static AircraftProfile Selected => Resolve(LocalStore.Profile?.selectedAircraft);
        // Legacy runtime UI (including Free Flight) consumes Names as selectable choices.
        public static string[] Names => All.Where(p => p.Status == AircraftCatalogStatus.Runtime && p.IsSelectable)
            .Select(p => p.Name).ToArray();
        public static string[] AllNames => All.Select(p => p.Name).ToArray();
        public static string[] CatalogIds => Entries.Select(p => p.CatalogId).ToArray();

        private static AircraftProfile Profile(string id, string catalogId, string name, string shortName,
            string family, string model, string category, AirframeShape shape, string texture, float scale,
            float chase, bool fpv, string description, string reference, string source,
            AircraftWorkspace workspaces, string[] legacyIds, string[] capabilities, string[] compatibleTaskTags,
            string[] flightModes, string[] useCases, string materialTheme, string difficulty,
            AircraftFlightParameters flightParameters, AircraftCatalogStatus status = AircraftCatalogStatus.Runtime,
            string lockedReasonKey = "")
        {
            bool runtimeReady = status == AircraftCatalogStatus.Runtime;
            return new AircraftProfile(id, catalogId, name, shortName, id.StartsWith("generic-", StringComparison.Ordinal)
                    || id == "racer" || id == "micro" ? "AeroSim" : "DJI",
                family, model, category, shape, texture, scale, chase, fpv, description, reference, source,
                workspaces, status, legacyIds, capabilities, compatibleTaskTags,
                StandardInput, flightModes, useCases, materialTheme, catalogId,
                runtimeReady ? "prototype-1" : "pending-runtime-visual", catalogId + "-training", "1.0",
                runtimeReady ? "aircraft.incompatible.capability" : lockedReasonKey,
                runtimeReady ? "runtime_prototype" : "pending_user", workspaces == AircraftWorkspace.Fpv ? "Low" : "Medium",
                difficulty, flightParameters, runtimeReady, runtimeReady, true, runtimeReady, runtimeReady);
        }

        private static AircraftFlightParameters Flight(float mass, float speed, float ascent, float descent,
            float tilt, float angularSpeed, float minutes)
        {
            return new AircraftFlightParameters(mass, speed, ascent, descent, tilt, angularSpeed, minutes);
        }

        private static bool Matches(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static bool Contains(string value, string query) =>
            !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
