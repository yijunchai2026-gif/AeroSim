using System;

namespace AeroSim.Input
{
    public enum FlightInputSourceKind { None, Keyboard, Joystick, Gamepad, Touch }
    public enum FlightInputContext { Flight, Menu, Replay }
    public enum InputSelectionMode { Automatic, Fixed }

    // Output order matches existing telemetry: throttle, yaw, pitch, roll.
    // No Unity types or hardware polling: each platform supplies its own samples.
    public readonly struct FlightControlFrame
    {
        public float Throttle { get; }
        public float Yaw { get; }
        public float Pitch { get; }
        public float Roll { get; }
        public string SourceId { get; }
        public FlightInputSourceKind SourceKind { get; }
        public bool HasSource => !string.IsNullOrEmpty(SourceId);
        public bool IsNeutral => Throttle == 0 && Yaw == 0 && Pitch == 0 && Roll == 0;

        public FlightControlFrame(float throttle, float yaw, float pitch, float roll,
            string sourceId, FlightInputSourceKind sourceKind)
        {
            Throttle = SafeAxis(throttle); Yaw = SafeAxis(yaw);
            Pitch = SafeAxis(pitch); Roll = SafeAxis(roll);
            SourceId = sourceId ?? string.Empty; SourceKind = sourceKind;
        }

        public static FlightControlFrame Neutral => new FlightControlFrame(0, 0, 0, 0,
            string.Empty, FlightInputSourceKind.None);
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static float Clamp(float value, float minimum, float maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;
        internal static float SafeAxis(float value) => Finite(value) ? Clamp(value, -1, 1) : 0;
    }

    // Raw values deliberately remain unbounded until matching joystick calibration.
    public readonly struct InputSample
    {
        public string SourceId { get; }
        public string DeviceIdentity { get; }
        public FlightInputSourceKind Kind { get; }
        public float Throttle { get; }
        public float Yaw { get; }
        public float Pitch { get; }
        public float Roll { get; }
        public bool Available { get; }

        public InputSample(string sourceId, FlightInputSourceKind kind, float throttle,
            float yaw, float pitch, float roll, string deviceIdentity = null, bool available = true)
        {
            SourceId = sourceId; Kind = kind; DeviceIdentity = deviceIdentity;
            Throttle = throttle; Yaw = yaw; Pitch = pitch; Roll = roll; Available = available;
        }
    }

    public readonly struct InputFilterSettings
    {
        public float Deadzone { get; }
        public float Expo { get; }
        public float Sensitivity { get; }
        public bool ReverseThrottle { get; }
        public bool ReverseYaw { get; }
        public bool ReversePitch { get; }
        public bool ReverseRoll { get; }

        public InputFilterSettings(float deadzone, float expo, float sensitivity,
            bool reverseThrottle = false, bool reverseYaw = false,
            bool reversePitch = false, bool reverseRoll = false)
        {
            Deadzone = FlightControlFrame.Finite(deadzone) && deadzone >= 0 && deadzone < 1
                ? Math.Min(deadzone, .95f) : .08f;
            Expo = FlightControlFrame.Finite(expo) ? FlightControlFrame.Clamp(expo, 0, 1) : .3f;
            Sensitivity = FlightControlFrame.Finite(sensitivity) && sensitivity >= 0
                ? Math.Min(sensitivity, 2) : 1;
            ReverseThrottle = reverseThrottle; ReverseYaw = reverseYaw;
            ReversePitch = reversePitch; ReverseRoll = reverseRoll;
        }

        public static InputFilterSettings Default => new InputFilterSettings(.08f, .3f, 1);
        public static InputFilterSettings Linear => new InputFilterSettings(0, 0, 1);
    }

    public readonly struct AxisCalibration
    {
        public float Minimum { get; }
        public float Center { get; }
        public float Maximum { get; }
        public AxisCalibration(float minimum, float center, float maximum)
        {
            Minimum = minimum; Center = center; Maximum = maximum;
        }
        public static AxisCalibration Symmetric => new AxisCalibration(-1, 0, 1);
    }

    public sealed class JoystickCalibration
    {
        public string DeviceIdentity { get; }
        public AxisCalibration Throttle { get; }
        public AxisCalibration Yaw { get; }
        public AxisCalibration Pitch { get; }
        public AxisCalibration Roll { get; }

        public JoystickCalibration(string deviceIdentity, AxisCalibration throttle,
            AxisCalibration yaw, AxisCalibration pitch, AxisCalibration roll)
        {
            DeviceIdentity = deviceIdentity; Throttle = throttle; Yaw = yaw;
            Pitch = pitch; Roll = roll;
        }
    }
}
