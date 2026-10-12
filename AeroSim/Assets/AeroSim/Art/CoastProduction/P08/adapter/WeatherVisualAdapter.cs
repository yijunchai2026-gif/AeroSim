// Reference-only consumer adapter for P08. Place in a consumer assembly explicitly when wiring is approved.
// It reads EnvironmentState and returns weights; it does not mutate state or assemble scene objects.
using System;
using System.Collections.Generic;
using AeroSim.Domain;

namespace AeroSim.WeatherVisuals
{
    [Serializable]
    public sealed class WeatherVisualOptions
    {
        public bool enabled = true;
        public bool vfxEnabled = true;
        public float cameraExposure01 = .5f;
        public float altitudeMeters = 2f;
        public HashSet<string> disabledAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    [Serializable]
    public sealed class WeatherVisualFrame
    {
        public int seed;
        public int qualityRank;
        public float cloudAmount, precipitation, wetness, puddleAmount, fog01, wind01;
        public float windX, windZ;
        public float drizzle, downpour, lensDrops, surfaceStreaks, puddleRipples;
        public float lowFog, seaFog, cloudShadows, rainCurtain, windDrivenSpray;
        public bool IsOff => drizzle == 0 && downpour == 0 && lensDrops == 0 && surfaceStreaks == 0 &&
            puddleRipples == 0 && lowFog == 0 && seaFog == 0 && cloudShadows == 0 && rainCurtain == 0 && windDrivenSpray == 0;
    }

    public static class WeatherVisualAdapter
    {
        public const int Low = 0, Medium = 1, High = 2, Ultra = 3;

        public static WeatherVisualFrame Evaluate(EnvironmentState snapshot, int requestedQuality, WeatherVisualOptions options = null)
        {
            options = options ?? new WeatherVisualOptions();
            int quality = requestedQuality < Low || requestedQuality > Ultra ? Low : requestedQuality;
            var frame = new WeatherVisualFrame { qualityRank = quality };
            if (!options.enabled || snapshot == null) return frame;

            frame.seed = snapshot.seed;
            frame.cloudAmount = Safe01(snapshot.cloudAmount, .26f);
            frame.precipitation = Safe01(snapshot.precipitation, 0f);
            frame.wetness = Safe01(snapshot.wetness, 0f);
            frame.puddleAmount = Safe01(snapshot.puddleAmount, 0f);
            float density = Safe(snapshot.fogDensity, .00115f, 0f, .02f);
            float vfx = Safe01(snapshot.vfxDensity, 0f);
            frame.fog01 = Saturate((density - .001f) / .007f);
            frame.wind01 = Saturate(Safe(snapshot.windSpeedMps, 0f, 0f, 40f) / 18f);
            NormalizeWind(snapshot.windDirection.x, snapshot.windDirection.z, out frame.windX, out frame.windZ);

            float rain = options.vfxEnabled ? Saturate(frame.precipitation * vfx) : 0f;
            float drizzle = rain * (1f - SmoothStep(.35f, .70f, frame.precipitation));
            float downpour = rain * SmoothStep(.45f, .80f, frame.precipitation);
            frame.drizzle = drizzle;
            frame.downpour = downpour;
            frame.lensDrops = rain * SmoothStep(.08f, .55f, frame.precipitation);
            frame.surfaceStreaks = options.vfxEnabled ? frame.wetness * SmoothStep(.35f, .85f, frame.precipitation) : 0f;
            frame.puddleRipples = options.vfxEnabled ? frame.puddleAmount * rain : 0f;
            frame.lowFog = frame.fog01 * (1f - SmoothStep(30f, 120f, Safe(options.altitudeMeters, 2f, 0f, 500f)));
            frame.seaFog = frame.fog01 * SmoothStep(.45f, .90f, frame.cloudAmount);
            frame.cloudShadows = frame.cloudAmount * (1f - SmoothStep(.75f, 1f, frame.cloudAmount));
            frame.rainCurtain = options.vfxEnabled ? downpour * SmoothStep(.35f, .75f, frame.fog01) : 0f;
            frame.windDrivenSpray = options.vfxEnabled ? rain * frame.wetness * SmoothStep(.30f, .75f, frame.wind01) : 0f;

            ApplyQuality(frame, quality);
            ApplyAssetSwitches(frame, options.disabledAssets);
            return frame;
        }

        public static WeatherVisualFrame EvaluateOff(int requestedQuality = Low) =>
            new WeatherVisualFrame { qualityRank = requestedQuality < Low || requestedQuality > Ultra ? Low : requestedQuality };

        private static void ApplyQuality(WeatherVisualFrame f, int quality)
        {
            float particleScale = quality == Low ? .25f : quality == Medium ? .50f : quality == High ? .80f : 1f;
            f.drizzle *= particleScale;
            f.downpour *= particleScale;
            if (quality == Low) { f.lensDrops = 0; f.surfaceStreaks = 0; f.puddleRipples = 0; f.seaFog = 0; f.cloudShadows = 0; f.rainCurtain = 0; f.windDrivenSpray = 0; }
            else if (quality == Medium) { f.surfaceStreaks = 0; f.rainCurtain = 0; f.windDrivenSpray = 0; }
            else { f.windDrivenSpray *= quality == High ? .4f : 1f; }
        }

        private static void ApplyAssetSwitches(WeatherVisualFrame f, HashSet<string> disabled)
        {
            if (disabled == null) return;
            if (disabled.Contains("drizzle")) f.drizzle = 0;
            if (disabled.Contains("downpour")) f.downpour = 0;
            if (disabled.Contains("lensDrops")) f.lensDrops = 0;
            if (disabled.Contains("surfaceStreaks")) f.surfaceStreaks = 0;
            if (disabled.Contains("puddleRipples")) f.puddleRipples = 0;
            if (disabled.Contains("lowFog")) f.lowFog = 0;
            if (disabled.Contains("seaFog")) f.seaFog = 0;
            if (disabled.Contains("cloudShadows")) f.cloudShadows = 0;
            if (disabled.Contains("rainCurtain")) f.rainCurtain = 0;
            if (disabled.Contains("windDrivenSpray")) f.windDrivenSpray = 0;
        }

        private static void NormalizeWind(float x, float z, out float nx, out float nz)
        {
            if (!Finite(x) || !Finite(z) || x * x + z * z < .0001f) { x = 1f; z = -.35f; }
            float inverse = 1f / (float)Math.Sqrt(x * x + z * z);
            nx = x * inverse; nz = z * inverse;
        }

        private static float SmoothStep(float a, float b, float value)
        {
            float t = Saturate((value - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
        private static float Safe01(float value, float fallback) => Safe(value, fallback, 0f, 1f);
        private static float Safe(float value, float fallback, float lo, float hi)
        {
            if (!Finite(value)) value = fallback;
            return value < lo ? lo : value > hi ? hi : value;
        }
        private static float Saturate(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
