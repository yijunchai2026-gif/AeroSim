using System;
using System.Collections.Generic;

namespace AeroSim.Input
{
    // Single-source four-axis arbitration. This class does not poll InputSystem,
    // write LocalStore, launch sessions, or decide pointer ownership.
    public sealed class InputRouter
    {
        private readonly Dictionary<string, InputFilterSettings> _filters =
            new Dictionary<string, InputFilterSettings>(StringComparer.Ordinal);
        private readonly Dictionary<string, JoystickCalibration> _calibrations =
            new Dictionary<string, JoystickCalibration>(StringComparer.Ordinal);

        public InputSelectionMode SelectionMode { get; private set; } = InputSelectionMode.Automatic;
        public string FixedSourceId { get; private set; } = string.Empty;
        public FlightInputContext Context { get; private set; } = FlightInputContext.Flight;
        public bool IsSuspended { get; private set; }
        public FlightControlFrame LastFrame { get; private set; } = FlightControlFrame.Neutral;

        public void SelectAutomatic()
        {
            SelectionMode = InputSelectionMode.Automatic; FixedSourceId = string.Empty; Clear();
        }

        public void SelectFixed(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A source ID is required.", nameof(sourceId));
            SelectionMode = InputSelectionMode.Fixed; FixedSourceId = sourceId; Clear();
        }

        public void SetFilter(string sourceId, InputFilterSettings filter)
        {
            RequireId(sourceId); _filters[sourceId] = filter;
        }

        public void SetJoystickCalibration(string sourceId, JoystickCalibration calibration)
        {
            RequireId(sourceId);
            if (calibration == null) _calibrations.Remove(sourceId);
            else _calibrations[sourceId] = calibration;
        }

        public void SetContext(FlightInputContext context)
        {
            Context = context; Clear();
        }
        public void Suspend() { IsSuspended = true; Clear(); }
        public void Resume() { IsSuspended = false; Clear(); }
        public void Clear() { LastFrame = FlightControlFrame.Neutral; }

        public FlightControlFrame ReadFrame(IReadOnlyList<InputSample> samples)
        {
            if (IsSuspended || Context != FlightInputContext.Flight || samples == null || samples.Count == 0)
                return Store(FlightControlFrame.Neutral);

            FlightControlFrame chosen = FlightControlFrame.Neutral;
            bool chosenActive = false;
            for (int i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                if (!sample.Available || string.IsNullOrWhiteSpace(sample.SourceId) ||
                    sample.Kind < FlightInputSourceKind.Keyboard || sample.Kind > FlightInputSourceKind.Touch ||
                    HasDuplicateId(samples, sample.SourceId)) continue;
                if (SelectionMode == InputSelectionMode.Fixed &&
                    !string.Equals(sample.SourceId, FixedSourceId, StringComparison.Ordinal)) continue;
                var filtered = Filter(sample);
                bool active = !filtered.IsNeutral;
                if (!chosen.HasSource || (active && !chosenActive) ||
                    (active == chosenActive && ComesFirst(filtered, chosen)))
                {
                    chosen = filtered; chosenActive = active;
                }
            }
            // Fixed source absent/disconnected: neutral, never fall back to another device.
            return Store(chosen);
        }

        private FlightControlFrame Filter(InputSample sample)
        {
            var filter = _filters.TryGetValue(sample.SourceId, out var configured)
                ? configured : InputFilterSettings.Default;
            JoystickCalibration calibration = null;
            if (sample.Kind == FlightInputSourceKind.Joystick &&
                _calibrations.TryGetValue(sample.SourceId, out var saved) &&
                !string.IsNullOrWhiteSpace(sample.DeviceIdentity) &&
                string.Equals(sample.DeviceIdentity, saved.DeviceIdentity, StringComparison.Ordinal)) calibration = saved;
            return new FlightControlFrame(
                FilterAxis(sample.Throttle, filter, filter.ReverseThrottle, calibration?.Throttle),
                FilterAxis(sample.Yaw, filter, filter.ReverseYaw, calibration?.Yaw),
                FilterAxis(sample.Pitch, filter, filter.ReversePitch, calibration?.Pitch),
                FilterAxis(sample.Roll, filter, filter.ReverseRoll, calibration?.Roll),
                sample.SourceId, sample.Kind);
        }

        private static float FilterAxis(float raw, InputFilterSettings filter, bool reverse, AxisCalibration? calibration)
        {
            if (!FlightControlFrame.Finite(raw)) return 0;
            if (calibration.HasValue)
            {
                var c = calibration.Value;
                // A corrupt saved axis fails closed, instead of dividing by zero or
                // applying a guessed calibration. Other independently valid axes remain usable.
                if (!FlightControlFrame.Finite(c.Minimum) || !FlightControlFrame.Finite(c.Center) ||
                    !FlightControlFrame.Finite(c.Maximum) || c.Minimum >= c.Center || c.Center >= c.Maximum) return 0;
                double centered = (double)raw - c.Center;
                double range = centered >= 0 ? (double)c.Maximum - c.Center : (double)c.Center - c.Minimum;
                double normalized = centered / range;
                raw = (float)Math.Max(-1d, Math.Min(1d, normalized));
            }
            raw = FlightControlFrame.SafeAxis(raw);
            float absolute = Math.Abs(raw);
            if (absolute <= filter.Deadzone) return 0;
            float value = Math.Sign(raw) * (absolute - filter.Deadzone) / (1 - filter.Deadzone);
            if (reverse) value = -value;
            value = (value * (1 - filter.Expo) + value * value * value * filter.Expo) * filter.Sensitivity;
            return FlightControlFrame.SafeAxis(value);
        }

        private static bool ComesFirst(FlightControlFrame candidate, FlightControlFrame current)
        {
            // Activity first, then Touch > Gamepad > Joystick > Keyboard. Ties use
            // ordinal source ID, not enumeration order or hardware arrival order.
            return candidate.SourceKind > current.SourceKind ||
                (candidate.SourceKind == current.SourceKind &&
                 string.CompareOrdinal(candidate.SourceId, current.SourceId) < 0);
        }

        private static bool HasDuplicateId(IReadOnlyList<InputSample> samples, string sourceId)
        {
            int count = 0;
            for (int i = 0; i < samples.Count; i++)
                if (string.Equals(sourceId, samples[i].SourceId, StringComparison.Ordinal) && ++count > 1) return true;
            return false;
        }

        private FlightControlFrame Store(FlightControlFrame frame) { LastFrame = frame; return frame; }
        private static void RequireId(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A source ID is required.", nameof(sourceId));
        }
    }
}
