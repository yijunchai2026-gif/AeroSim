using System;
using AeroSim.Input;

internal static class InputRouterTests
{
    private static int _checks, _failures;
    private static void Check(string name, bool condition)
    {
        _checks++;
        if (!condition) { _failures++; Console.WriteLine("FAIL " + name); }
    }
    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .00001f;
    private static void Axes(string name, FlightControlFrame f, float throttle, float yaw, float pitch, float roll)
    {
        Check(name, Near(f.Throttle, throttle) && Near(f.Yaw, yaw) && Near(f.Pitch, pitch) && Near(f.Roll, roll));
    }
    private static InputRouter Linear(params string[] ids)
    {
        var router = new InputRouter();
        foreach (var id in ids) router.SetFilter(id, InputFilterSettings.Linear);
        return router;
    }
    private static JoystickCalibration Calibration(string device, AxisCalibration axis) =>
        new JoystickCalibration(device, axis, axis, axis, axis);
    private static int Main()
    {
        var r = Linear("keys", "pad", "stick", "touch", "pad-b", "pad-a");
        var keyboard = new InputSample("keys", FlightInputSourceKind.Keyboard, .8f, .6f, .4f, .2f);
        var pad = new InputSample("pad", FlightInputSourceKind.Gamepad, .3f, -.2f, .5f, -.4f);
        var stick = new InputSample("stick", FlightInputSourceKind.Joystick, 15, 10, 0, -10, "usb-stick-A");
        var touch = new InputSample("touch", FlightInputSourceKind.Touch, -.7f, .2f, -.3f, .4f);
        Check("no samples produce source-free neutral", !r.ReadFrame(null).HasSource && r.ReadFrame(new InputSample[0]).IsNeutral);
        Axes("keyboard four axes retain telemetry order", r.ReadFrame(new[] { keyboard }), .8f, .6f, .4f, .2f);
        var f = r.ReadFrame(new[] { keyboard, pad });
        Check("two active sources select exactly one", f.SourceId == "pad");
        Axes("two active sources never add values", f, .3f, -.2f, .5f, -.4f);
        Check("permutation preserves selection", r.ReadFrame(new[] { pad, keyboard }).SourceId == "pad");
        Check("touch takes precedence over active pad", r.ReadFrame(new[] { pad, keyboard, touch }).SourceId == "touch");
        Check("neutral connected pad does not suppress active keyboard",
            r.ReadFrame(new[] { keyboard, new InputSample("pad", FlightInputSourceKind.Gamepad, 0, 0, 0, 0) }).SourceId == "keys");
        Check("equal source kinds resolve by stable ordinal ID", r.ReadFrame(new[] {
            new InputSample("pad-b", FlightInputSourceKind.Gamepad, .7f, 0, 0, 0),
            new InputSample("pad-a", FlightInputSourceKind.Gamepad, .2f, 0, 0, 0) }).SourceId == "pad-a");
        Check("absent invalid or disconnected samples are neutral", r.ReadFrame(new[] {
            default(InputSample), new InputSample("gone", FlightInputSourceKind.Touch, 1, 1, 1, 1, available: false),
            new InputSample("none", FlightInputSourceKind.None, 1, 1, 1, 1),
            new InputSample("unknown", (FlightInputSourceKind)80, 1, 1, 1, 1) }).IsNeutral);
        Check("duplicate source IDs fail closed", !r.ReadFrame(new[] { pad, pad }).HasSource);

        r.SelectFixed("keys");
        Axes("fixed keyboard ignores higher-priority touch and pad", r.ReadFrame(new[] { keyboard, pad, touch }), .8f, .6f, .4f, .2f);
        Check("fixed missing source never silently falls back", !r.ReadFrame(new[] { pad, touch }).HasSource);
        Check("fixed unavailable source does not act", !r.ReadFrame(new[] {
            new InputSample("keys", FlightInputSourceKind.Keyboard, 1, 1, 1, 1, available: false), touch }).HasSource);
        r.SelectAutomatic();
        Check("automatic restored", r.ReadFrame(new[] { keyboard, touch }).SourceId == "touch");

        var axis = new AxisCalibration(-10, 10, 20);
        r.SetJoystickCalibration("stick", Calibration("usb-stick-A", axis));
        r.SelectFixed("stick");
        Axes("matching joystick normalizes asymmetric center/ranges before clamping",
            r.ReadFrame(new[] { stick }), .5f, 0, -.5f, -1);
        r.SetJoystickCalibration("keys", Calibration("usb-stick-A", axis));
        r.SelectFixed("keys");
        Axes("keyboard never receives joystick calibration", r.ReadFrame(new[] { keyboard, stick }), .8f, .6f, .4f, .2f);
        r.SetJoystickCalibration("pad", Calibration("usb-stick-A", axis));
        r.SelectFixed("pad");
        Axes("gamepad never receives joystick calibration", r.ReadFrame(new[] { pad, stick }), .3f, -.2f, .5f, -.4f);
        r.SetJoystickCalibration("touch", Calibration("usb-stick-A", axis));
        r.SelectFixed("touch");
        Axes("touch never receives joystick calibration", r.ReadFrame(new[] { touch, stick }), -.7f, .2f, -.3f, .4f);
        r.SelectFixed("stick");
        Axes("different joystick identity skips foreign calibration",
            r.ReadFrame(new[] { new InputSample("stick", FlightInputSourceKind.Joystick, .5f, -.3f, .2f, -.1f, "usb-stick-B") }), .5f, -.3f, .2f, -.1f);
        Axes("empty joystick identity skips unverified calibration",
            r.ReadFrame(new[] { new InputSample("stick", FlightInputSourceKind.Joystick, .5f, -.3f, .2f, -.1f) }), .5f, -.3f, .2f, -.1f);
        r.SetJoystickCalibration("stick", null);
        Axes("removing calibration returns raw normalized domain",
            r.ReadFrame(new[] { new InputSample("stick", FlightInputSourceKind.Joystick, .5f, -.3f, .2f, -.1f, "usb-stick-A") }), .5f, -.3f, .2f, -.1f);

        var invalidRanges = new[] { new AxisCalibration(0, 0, 0), new AxisCalibration(1, 0, -1),
            new AxisCalibration(-1, float.NaN, 1), new AxisCalibration(float.NegativeInfinity, 0, 1),
            new AxisCalibration(-1, 0, float.PositiveInfinity), new AxisCalibration(-1, 1, 1) };
        for (int i = 0; i < invalidRanges.Length; i++)
        {
            r.SetJoystickCalibration("stick", Calibration("usb-stick-A", invalidRanges[i]));
            Check("invalid calibration fails closed " + i, r.ReadFrame(new[] { stick }).IsNeutral);
        }
        r.SetJoystickCalibration("stick", new JoystickCalibration("usb-stick-A", invalidRanges[0],
            AxisCalibration.Symmetric, AxisCalibration.Symmetric, AxisCalibration.Symmetric));
        Axes("one invalid calibrated axis does not contaminate valid axes",
            r.ReadFrame(new[] { new InputSample("stick", FlightInputSourceKind.Joystick, 1, .2f, .3f, .4f, "usb-stick-A") }), 0, .2f, .3f, .4f);
        r.SetJoystickCalibration("stick", Calibration("usb-stick-A", new AxisCalibration(-float.MaxValue, 0, float.MaxValue)));
        Axes("extreme finite calibration uses safe double arithmetic",
            r.ReadFrame(new[] { new InputSample("stick", FlightInputSourceKind.Joystick, float.MaxValue, 0, -float.MaxValue, 0, "usb-stick-A") }), 1, 0, -1, 0);
        r.SetJoystickCalibration("stick", Calibration("usb-stick-A", new AxisCalibration(-.0001f, 0, .0001f)));
        Axes("small nonzero range remains normalized",
            r.ReadFrame(new[] { new InputSample("stick", FlightInputSourceKind.Joystick, .00005f, 0, -.00005f, 0, "usb-stick-A") }), .5f, 0, -.5f, 0);

        r.SelectFixed("keys");
        r.SetFilter("keys", new InputFilterSettings(.2f, 0, 1));
        Axes("deadzone threshold is neutral and signed range remaps", r.ReadFrame(new[] {
            new InputSample("keys", FlightInputSourceKind.Keyboard, .2f, -.2f, .6f, -.6f) }), 0, 0, .5f, -.5f);
        r.SetFilter("keys", new InputFilterSettings(0, 0, 1, true, true, true, true));
        Axes("each axis reverses independently", r.ReadFrame(new[] { keyboard }), -.8f, -.6f, -.4f, -.2f);
        r.SetFilter("keys", new InputFilterSettings(0, 1, 1));
        Axes("cubic expo keeps sign and endpoints", r.ReadFrame(new[] {
            new InputSample("keys", FlightInputSourceKind.Keyboard, .5f, -.5f, 1, -1) }), .125f, -.125f, 1, -1);
        r.SetFilter("keys", new InputFilterSettings(0, 0, 2));
        Axes("sensitivity output clamps once per chosen source", r.ReadFrame(new[] { keyboard }), 1, 1, .8f, .4f);
        r.SetFilter("keys", InputFilterSettings.Linear);
        Axes("nonfinite raw axes become neutral and finite overshoot clamps", r.ReadFrame(new[] {
            new InputSample("keys", FlightInputSourceKind.Keyboard, float.NaN, float.PositiveInfinity, -float.PositiveInfinity, 4) }), 0, 0, 0, 1);

        var weird = new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -float.MaxValue, float.MaxValue, -1f, 1f };
        for (int i = 0; i < weird.Length; i++)
        {
            r.SetFilter("keys", new InputFilterSettings(weird[i], weird[i], weird[i]));
            f = r.ReadFrame(new[] { keyboard });
            Check("malformed filter remains finite/bounded " + i, FiniteBounded(f));
        }
        var broken = new InputFilterSettings(1, float.NaN, -1);
        Check("malformed deadzone expo sensitivity have explicit defaults",
            Near(broken.Deadzone, .08f) && Near(broken.Expo, .3f) && Near(broken.Sensitivity, 1));
        r.SetFilter("keys", InputFilterSettings.Linear);
        r.SetContext(FlightInputContext.Menu);
        Check("menu context suppresses live flight axes", !r.ReadFrame(new[] { keyboard }).HasSource);
        r.SetContext(FlightInputContext.Replay);
        Check("replay context suppresses live flight axes", r.ReadFrame(new[] { keyboard }).IsNeutral);
        r.SetContext(FlightInputContext.Flight);
        r.ReadFrame(new[] { keyboard }); r.Suspend();
        Check("suspend clears remembered frame immediately", !r.LastFrame.HasSource);
        Check("suspend prevents subsequent held input", r.ReadFrame(new[] { keyboard }).IsNeutral);
        r.Resume();
        Check("explicit resume allows fresh sampled input", r.ReadFrame(new[] { keyboard }).SourceId == "keys");
        r.Clear(); Check("clear neutralizes output without deleting fixed source setting", r.LastFrame.IsNeutral && r.FixedSourceId == "keys");
        r.ReadFrame(new[] { keyboard }); r.ReadFrame(new InputSample[0]);
        Check("sample loss clears stale frame", !r.LastFrame.HasSource);

        // Seeded stress covers malformed calibration and filters jointly, plus all
        // source kinds and selection modes. Output must never leak NaN or infinity.
        var random = new Random(125);
        for (int i = 0; i < 3000; i++)
        {
            float raw = i % 7 == 0 ? weird[i % weird.Length] : (float)(random.NextDouble() * 50 - 25);
            var kind = (FlightInputSourceKind)(1 + i % 4);
            var rr = Linear("source");
            rr.SetFilter("source", new InputFilterSettings(weird[i % weird.Length], raw, weird[(i + 1) % weird.Length]));
            rr.SetJoystickCalibration("source", Calibration("unit", new AxisCalibration(-10, raw, 10)));
            if (i % 2 == 0) rr.SelectFixed("source");
            f = rr.ReadFrame(new[] { new InputSample("source", kind, raw, -raw, float.NaN, 1, "unit") });
            Check("stress bounded frame " + i, FiniteBounded(f));
        }
        Console.WriteLine("InputRouter pure C# checks: " + _checks + "; failures: " + _failures);
        Console.WriteLine("Scope: algorithms only; not wired into FlightInput, no Unity player, real device or visual QA.");
        return _failures == 0 ? 0 : 2;
    }
    private static bool FiniteBounded(FlightControlFrame frame)
    {
        foreach (var value in new[] { frame.Throttle, frame.Yaw, frame.Pitch, frame.Roll })
            if (float.IsNaN(value) || float.IsInfinity(value) || value < -1 || value > 1) return false;
        return true;
    }
}
