using System;
using AeroSim.Input;

internal static class TouchInputStateDeveloperTests
{
    private static int _checks, _failures;

    private readonly struct RecordedState
    {
        internal readonly int? LeftId, RightId;
        internal readonly float LeftX, LeftY, RightX, RightY;
        internal readonly bool Suspended;
        internal RecordedState(TouchInputState state)
        {
            LeftId = state.LeftPointerId; RightId = state.RightPointerId;
            LeftX = state.LeftX; LeftY = state.LeftY;
            RightX = state.RightX; RightY = state.RightY;
            Suspended = state.IsSuspended;
        }
        internal bool Matches(TouchInputState state) =>
            LeftId == state.LeftPointerId && RightId == state.RightPointerId &&
            LeftX == state.LeftX && LeftY == state.LeftY &&
            RightX == state.RightX && RightY == state.RightY && Suspended == state.IsSuspended;
    }

    private static void Check(string name, bool condition)
    {
        _checks++;
        if (!condition) { _failures++; Console.WriteLine("FAIL " + name); }
    }
    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .00001f;
    private static void State(string name, TouchInputState state, int? leftId, int? rightId,
        float leftX, float leftY, float rightX, float rightY, bool suspended = false)
    {
        Check(name, state.LeftPointerId == leftId && state.RightPointerId == rightId &&
            Near(state.LeftX, leftX) && Near(state.LeftY, leftY) &&
            Near(state.RightX, rightX) && Near(state.RightY, rightY) && state.IsSuspended == suspended);
    }
    private static void Sample(string name, TouchInputState state, bool available,
        float throttle, float yaw, float pitch, float roll)
    {
        var sample = state.Snapshot();
        Check(name + " identity", sample.SourceId == state.SourceId &&
            sample.Kind == FlightInputSourceKind.Touch && sample.DeviceIdentity == null && sample.Available == available);
        Check(name + " axes", Near(sample.Throttle, throttle) && Near(sample.Yaw, yaw) &&
            Near(sample.Pitch, pitch) && Near(sample.Roll, roll));
    }
    private static void Reject(string name, TouchInputState state, Func<bool> action)
    {
        var before = new RecordedState(state);
        Check(name + " returns false", !action());
        Check(name + " leaves entire state unchanged", before.Matches(state));
    }
    private static TouchStickSide Other(TouchStickSide side) =>
        side == TouchStickSide.Left ? TouchStickSide.Right : TouchStickSide.Left;
    private static TouchInputState Both()
    {
        var state = new TouchInputState();
        Check("fixture left accepted", state.Begin(TouchStickSide.Left, 11, .25f, -.5f));
        Check("fixture right accepted", state.Begin(TouchStickSide.Right, 12, -.75f, 1));
        return state;
    }

    private static void InitialAndIds()
    {
        var state = new TouchInputState();
        Check("default source ID", state.SourceId == "touch:dual");
        State("initial state", state, null, null, 0, 0, 0, 0);
        Sample("initial unavailable sample", state, false, 0, 0, 0, 0);
        foreach (var id in new[] { null, "", " ", "\t\r\n" })
        {
            bool threw = false;
            try { new TouchInputState(id); }
            catch (ArgumentException exception) { threw = exception.ParamName == "sourceId"; }
            Check("invalid source ID throws argument exception", threw);
        }
        var custom = new TouchInputState(" touch:ipad ");
        Check("nonblank explicit ID preserved verbatim", custom.SourceId == " touch:ipad ");
        Sample("custom idle identity", custom, false, 0, 0, 0, 0);
        Check("zero pointer ID accepted", state.Begin(TouchStickSide.Left, 0, .2f, .3f));
        Check("maximum pointer ID accepted", state.Begin(TouchStickSide.Right, int.MaxValue, .4f, .5f));
        State("full nonnegative ID domain", state, 0, int.MaxValue, .2f, .3f, .4f, .5f);
        Check("zero ID owner release", state.End(TouchStickSide.Left, 0));
        Check("maximum ID owner release", state.End(TouchStickSide.Right, int.MaxValue));
        State("ID boundary releases", state, null, null, 0, 0, 0, 0);
    }

    private static void RequiredSequence()
    {
        var state = new TouchInputState();
        Check("sequence left Down 11", state.Begin(TouchStickSide.Left, 11, .1f, .2f));
        Check("sequence right Down 12", state.Begin(TouchStickSide.Right, 12, .3f, .4f));
        Check("sequence left Move", state.Move(TouchStickSide.Left, 11, -.5f, .6f));
        Check("sequence right Move", state.Move(TouchStickSide.Right, 12, .7f, -.8f));
        Sample("two sticks one source in telemetry order", state, true, .6f, -.5f, -.8f, .7f);
        Reject("third Down left", state, () => state.Begin(TouchStickSide.Left, 13, 1, 1));
        Reject("third Down right", state, () => state.Begin(TouchStickSide.Right, 13, 1, 1));
        Check("sequence left Cancel", state.Cancel(TouchStickSide.Left, 11));
        State("left cancellation preserves right", state, null, 12, 0, 0, .7f, -.8f);
        Reject("unknown capture out does not clear right", state, () => state.CaptureOut(TouchStickSide.Right, 13));
        Reject("wrong side late left Move", state, () => state.Move(TouchStickSide.Right, 11, 1, 1));
        Reject("late ended left Move", state, () => state.Move(TouchStickSide.Left, 11, 1, 1));
        Reject("late ended left End", state, () => state.End(TouchStickSide.Left, 11));
        Check("sequence right Up", state.End(TouchStickSide.Right, 12));
        State("sequence neutral and owner-free", state, null, null, 0, 0, 0, 0);
        Sample("sequence idle unavailable", state, false, 0, 0, 0, 0);
    }

    private static void OwnershipAndTermination()
    {
        foreach (TouchStickSide side in new[] { TouchStickSide.Left, TouchStickSide.Right })
        {
            var state = new TouchInputState();
            Check("owner setup " + side, state.Begin(side, 11, .2f, .4f));
            Reject("same pointer cannot cross to empty stick " + side, state,
                () => state.Begin(Other(side), 11, .9f, -.9f));
            Reject("repeated Down never resets owner axes " + side, state,
                () => state.Begin(side, 11, -.9f, .9f));
            Reject("later Down never replaces owner " + side, state,
                () => state.Begin(side, 12, -.9f, .9f));
            Reject("nonowner Move " + side, state, () => state.Move(side, 12, 1, 1));
            Reject("nonowner invalid Move " + side, state, () => state.Move(side, 12, float.NaN, 1));
            Reject("wrong-side owner Move " + side, state, () => state.Move(Other(side), 11, 1, 1));
            Reject("nonowner End " + side, state, () => state.End(side, 12));
            Reject("wrong-side owner End " + side, state, () => state.End(Other(side), 11));
            Reject("wrong-side owner Cancel " + side, state, () => state.Cancel(Other(side), 11));
            Reject("wrong-side owner CaptureOut " + side, state, () => state.CaptureOut(Other(side), 11));
            Check("owner capture out releases " + side, state.CaptureOut(side, 11));
            State("owner capture out neutral " + side, state, null, null, 0, 0, 0, 0);
            Reject("repeat CaptureOut " + side, state, () => state.CaptureOut(side, 11));
            Reject("repeat Cancel " + side, state, () => state.Cancel(side, 11));
            Reject("repeat End " + side, state, () => state.End(side, 11));
            Check("pointer reusable after release " + side, state.Begin(Other(side), 11, .7f, -.2f));
            Reject("old side cannot end reassigned pointer " + side, state, () => state.End(side, 11));
            Reject("old side cannot move reassigned pointer " + side, state, () => state.Move(side, 11, 1, 1));
            Check("reassigned owner cancel " + side, state.Cancel(Other(side), 11));
        }

        var both = Both();
        Reject("left cannot use right owner Down", both, () => both.Begin(TouchStickSide.Left, 12, 0, 0));
        Reject("right cannot use left owner Down", both, () => both.Begin(TouchStickSide.Right, 11, 0, 0));
        Reject("left cannot terminate right owner", both, () => both.End(TouchStickSide.Left, 12));
        Reject("right cannot terminate left owner", both, () => both.Cancel(TouchStickSide.Right, 11));
        Check("right End leaves left owner", both.End(TouchStickSide.Right, 12));
        State("right End preserves left values", both, 11, null, .25f, -.5f, 0, 0);
        Check("left End releases last owner", both.End(TouchStickSide.Left, 11));
    }

    private static void InvalidEventsAndCoordinates()
    {
        var state = Both();
        foreach (var side in new[] { (TouchStickSide)(-1), (TouchStickSide)2, (TouchStickSide)int.MaxValue })
        {
            Reject("invalid enum Begin", state, () => state.Begin(side, 13, 1, 1));
            Reject("invalid enum Move", state, () => state.Move(side, 11, 1, 1));
            Reject("invalid enum End", state, () => state.End(side, 11));
            Reject("invalid enum Cancel", state, () => state.Cancel(side, 11));
            Reject("invalid enum CaptureOut", state, () => state.CaptureOut(side, 11));
        }
        foreach (TouchStickSide side in new[] { TouchStickSide.Left, TouchStickSide.Right })
        {
            foreach (int id in new[] { -1, int.MinValue })
            {
                Reject("negative pointer Begin " + side, state, () => state.Begin(side, id, 1, 1));
                Reject("negative pointer Move " + side, state, () => state.Move(side, id, 1, 1));
                Reject("negative pointer End " + side, state, () => state.End(side, id));
                Reject("negative pointer Cancel " + side, state, () => state.Cancel(side, id));
                Reject("negative pointer CaptureOut " + side, state, () => state.CaptureOut(side, id));
            }
            var free = new TouchInputState();
            Check("other stick coordinate fixture " + side, free.Begin(Other(side), 12, .3f, -.6f));
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Reject("invalid Begin x cannot acquire " + side, free, () => free.Begin(side, 11, bad, .9f));
                Reject("invalid Begin y cannot acquire " + side, free, () => free.Begin(side, 11, .9f, bad));
                Reject("invalid Begin pair cannot acquire " + side, free, () => free.Begin(side, 11, bad, bad));
            }
            Check("finite Begin after invalid accepts " + side, free.Begin(side, 11, .5f, -.7f));
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Check("owner restore before invalid x " + side, free.Move(side, 11, .5f, -.7f));
                Check("owner invalid x returns true " + side, free.Move(side, 11, bad, .8f));
                CheckPairZeroWithOwner("invalid x zeros pair retains owner " + side, free, side, 11);
                Check("owner restore before invalid y " + side, free.Move(side, 11, .5f, -.7f));
                Check("owner invalid y returns true " + side, free.Move(side, 11, .8f, bad));
                CheckPairZeroWithOwner("invalid y zeros pair retains owner " + side, free, side, 11);
                Check("owner invalid pair returns true " + side, free.Move(side, 11, bad, bad));
                CheckPairZeroWithOwner("invalid pair retains owner " + side, free, side, 11);
                Check("invalid owner Move preserves other stick " + side,
                    side == TouchStickSide.Left ? free.RightPointerId == 12 && Near(free.RightX, .3f) && Near(free.RightY, -.6f) :
                    free.LeftPointerId == 12 && Near(free.LeftX, .3f) && Near(free.LeftY, -.6f));
            }
            Check("owner recovers after invalid geometry " + side, free.Move(side, 11, .4f, -.2f));
            var clamped = new TouchInputState();
            Check("finite extremes Begin accepts " + side, clamped.Begin(side, 11, float.MaxValue, -float.MaxValue));
            CheckPair("finite extremes clamp by axis " + side, clamped, side, 1, -1);
            Check("Move finite extremes accepts " + side, clamped.Move(side, 11, -float.MaxValue, float.MaxValue));
            CheckPair("Move finite extremes clamp by axis " + side, clamped, side, -1, 1);
            Check("axis corner Move accepts " + side, clamped.Move(side, 11, 1, 1));
            CheckPair("no circular renormalization " + side, clamped, side, 1, 1);
            Check("raw small Move accepts " + side, clamped.Move(side, 11, .01f, -.02f));
            CheckPair("core does not apply deadzone or expo " + side, clamped, side, .01f, -.02f);
        }
        var neutralOwner = new TouchInputState();
        Check("zero coordinate Begin still owns", neutralOwner.Begin(TouchStickSide.Left, 11, 0, 0));
        Sample("neutral owner remains available", neutralOwner, true, 0, 0, 0, 0);
        Check("invalid owner Move still handled", neutralOwner.Move(TouchStickSide.Left, 11, float.NaN, 1));
        Sample("invalid owner coordinates remain available zero", neutralOwner, true, 0, 0, 0, 0);
    }

    private static void CheckPair(string name, TouchInputState state, TouchStickSide side, float x, float y)
    {
        Check(name, side == TouchStickSide.Left ? Near(state.LeftX, x) && Near(state.LeftY, y) :
            Near(state.RightX, x) && Near(state.RightY, y));
    }
    private static void CheckPairZeroWithOwner(string name, TouchInputState state, TouchStickSide side, int id)
    {
        Check(name, side == TouchStickSide.Left ? state.LeftPointerId == id && state.LeftX == 0 && state.LeftY == 0 :
            state.RightPointerId == id && state.RightX == 0 && state.RightY == 0);
    }

    private static void Lifecycle()
    {
        var state = Both();
        var saved = state.Snapshot();
        state.Clear();
        State("Clear active state neutral but not suspended", state, null, null, 0, 0, 0, 0);
        Sample("Clear unavailable", state, false, 0, 0, 0, 0);
        Check("Snapshot is an immutable value", saved.Available && Near(saved.Throttle, -.5f) && Near(saved.Roll, -.75f));
        Reject("late Move after Clear", state, () => state.Move(TouchStickSide.Left, 11, 1, 1));
        Reject("late End after Clear", state, () => state.End(TouchStickSide.Right, 12));
        state.Clear();
        Check("pointer reusable after Clear", state.Begin(TouchStickSide.Right, 11, .8f, -.4f));
        Reject("late former right ID cannot affect new owner", state, () => state.CaptureOut(TouchStickSide.Right, 12));
        state.Suspend();
        State("Suspend clears owners and axes", state, null, null, 0, 0, 0, 0, true);
        Sample("Suspend unavailable", state, false, 0, 0, 0, 0);
        state.Suspend(); state.Clear();
        State("repeated Suspend then Clear remains suspended", state, null, null, 0, 0, 0, 0, true);
        Reject("suspended Down left", state, () => state.Begin(TouchStickSide.Left, 11, 1, 1));
        Reject("suspended Down right", state, () => state.Begin(TouchStickSide.Right, 12, 1, 1));
        Reject("suspended Move", state, () => state.Move(TouchStickSide.Right, 11, 1, 1));
        Reject("suspended End", state, () => state.End(TouchStickSide.Right, 11));
        state.Resume();
        State("Resume clears then unsuspends", state, null, null, 0, 0, 0, 0);
        Sample("Resume alone does not create available input", state, false, 0, 0, 0, 0);
        Reject("held old Move after Resume", state, () => state.Move(TouchStickSide.Right, 11, 1, 1));
        Reject("held old Cancel after Resume", state, () => state.Cancel(TouchStickSide.Right, 11));
        state.Resume();
        State("repeated idle Resume is neutral", state, null, null, 0, 0, 0, 0);
        Check("fresh Down required and accepted after Resume", state.Begin(TouchStickSide.Left, 11, .1f, -.2f));
        state.Resume();
        State("Resume clears even if already unsuspended", state, null, null, 0, 0, 0, 0);
        Reject("old Move after repeated Resume", state, () => state.Move(TouchStickSide.Left, 11, 1, 1));
        state.Suspend(); state.Resume(); state.Suspend();
        State("last Suspend wins in lifecycle sequence", state, null, null, 0, 0, 0, 0, true);
        state.Clear();
        Reject("Clear cannot unlock final Suspend", state, () => state.Begin(TouchStickSide.Left, 11, 1, 1));
        state.Resume();
        Check("new Down after lifecycle sequence", state.Begin(TouchStickSide.Left, 11, .1f, -.2f));
        Sample("fresh post lifecycle sample", state, true, -.2f, .1f, 0, 0);
    }

    private static void RouterCompatibility()
    {
        var state = Both();
        var router = new InputRouter();
        router.SetFilter(state.SourceId, InputFilterSettings.Linear);
        router.SetFilter("keys", InputFilterSettings.Linear);
        var keyboard = new InputSample("keys", FlightInputSourceKind.Keyboard, 1, 1, 1, 1);
        var frame = router.ReadFrame(new[] { state.Snapshot(), keyboard });
        Check("one dual source selected over keyboard", frame.SourceId == state.SourceId && frame.SourceKind == FlightInputSourceKind.Touch);
        Check("router does not arbitrate between sticks or add keyboard", Near(frame.Throttle, -.5f) && Near(frame.Yaw, .25f) &&
            Near(frame.Pitch, 1) && Near(frame.Roll, -.75f));
        router.SelectFixed(state.SourceId);
        Check("right Move for filter", state.Move(TouchStickSide.Right, 12, .6f, -.6f));
        Check("left Move for filter", state.Move(TouchStickSide.Left, 11, -.6f, .6f));
        router.SetFilter(state.SourceId, new InputFilterSettings(.2f, 1, 1));
        Sample("unfiltered snapshot before router", state, true, .6f, -.6f, -.6f, .6f);
        frame = router.ReadFrame(new[] { state.Snapshot() });
        Check("deadzone and expo applied once by router", Near(frame.Throttle, .125f) && Near(frame.Yaw, -.125f) &&
            Near(frame.Pitch, -.125f) && Near(frame.Roll, .125f));
        Sample("router read never mutates touch state", state, true, .6f, -.6f, -.6f, .6f);
        Check("cancel one stick for router", state.Cancel(TouchStickSide.Left, 11));
        frame = router.ReadFrame(new[] { state.Snapshot() });
        Check("one surviving stick retains single-source frame", frame.SourceId == state.SourceId &&
            frame.Throttle == 0 && frame.Yaw == 0 && Near(frame.Pitch, -.125f) && Near(frame.Roll, .125f));
        state.Clear();
        Check("Fixed idle touch neutral without keyboard fallback", !router.ReadFrame(new[] { state.Snapshot(), keyboard }).HasSource);
        Check("new touch owner for suspension", state.Begin(TouchStickSide.Left, 0, 1, 1));
        state.Suspend();
        Check("Fixed suspended touch unavailable", !router.ReadFrame(new[] { state.Snapshot(), keyboard }).HasSource);
        state.Resume();
        Check("Fixed resumed touch remains unavailable until new Down", !router.ReadFrame(new[] { state.Snapshot(), keyboard }).HasSource);
        router.SelectAutomatic();
        Check("Automatic idle touch does not suppress keyboard", router.ReadFrame(new[] { state.Snapshot(), keyboard }).SourceId == "keys");
    }

    private static void StressInvariants()
    {
        // Developer invariants, not the independently authored reference-state QA.
        var state = new TouchInputState("touch:stress");
        var random = new Random(1251010);
        var coordinates = new[] { -.7f, 0, .4f, 2f, -2f, float.MaxValue, -float.MaxValue,
            float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        for (int i = 0; i < 2400; i++)
        {
            TouchStickSide side = (TouchStickSide)random.Next(-1, 3);
            int id = random.Next(0, 8);
            if (i % 17 == 0) id = -1;
            else if (i % 19 == 0) id = int.MaxValue;
            float x = coordinates[random.Next(coordinates.Length)], y = coordinates[random.Next(coordinates.Length)];
            var before = new RecordedState(state);
            bool accepted = true;
            int operation = random.Next(0, 8);
            switch (operation)
            {
                case 0: accepted = state.Begin(side, id, x, y); break;
                case 1: accepted = state.Move(side, id, x, y); break;
                case 2: accepted = state.End(side, id); break;
                case 3: accepted = state.Cancel(side, id); break;
                case 4: accepted = state.CaptureOut(side, id); break;
                case 5: state.Clear(); break;
                case 6: state.Suspend(); break;
                case 7: state.Resume(); break;
            }
            Check("stress rejected event unchanged " + i, accepted || before.Matches(state));
            Check("stress bounded axes " + i, Bounded(state.LeftX) && Bounded(state.LeftY) && Bounded(state.RightX) && Bounded(state.RightY));
            Check("stress owner uniqueness " + i, !state.LeftPointerId.HasValue || !state.RightPointerId.HasValue || state.LeftPointerId != state.RightPointerId);
            Check("stress owner-free axes zero " + i,
                (state.LeftPointerId.HasValue || (state.LeftX == 0 && state.LeftY == 0)) &&
                (state.RightPointerId.HasValue || (state.RightX == 0 && state.RightY == 0)));
            Check("stress paused state cleared " + i, !state.IsSuspended ||
                (!state.LeftPointerId.HasValue && !state.RightPointerId.HasValue && state.LeftX == 0 && state.LeftY == 0 && state.RightX == 0 && state.RightY == 0));
            var sample = state.Snapshot();
            Check("stress sample availability and identity " + i, sample.SourceId == "touch:stress" && sample.Kind == FlightInputSourceKind.Touch &&
                sample.Available == (!state.IsSuspended && (state.LeftPointerId.HasValue || state.RightPointerId.HasValue)));
            Check("stress snapshot order and neutral unavailable " + i,
                sample.Available ? sample.Throttle == state.LeftY && sample.Yaw == state.LeftX && sample.Pitch == state.RightY && sample.Roll == state.RightX :
                sample.Throttle == 0 && sample.Yaw == 0 && sample.Pitch == 0 && sample.Roll == 0);
        }
    }
    private static bool Bounded(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= -1 && value <= 1;

    private static int Main()
    {
        InitialAndIds(); RequiredSequence(); OwnershipAndTermination();
        InvalidEventsAndCoordinates(); Lifecycle(); RouterCompatibility(); StressInvariants();
        Console.WriteLine("TouchInputState developer pure C# checks: " + _checks + "; failures: " + _failures);
        Console.WriteLine("Scope: developer algorithms only; not independent QA, Unity player/UI capture, physical controller, or real iPad/iPhone validation.");
        return _failures == 0 ? 0 : 2;
    }
}
