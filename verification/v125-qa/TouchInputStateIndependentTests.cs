using System;
using System.Collections.Generic;
using System.Globalization;
using AeroSim.Input;

// Independent QA oracle derived from V125_TOUCH_CONTRACT, not the implementation
// or developer tests. No Unity, pointer capture, geometry, or device claims.
internal static class TouchInputStateIndependentTests
{
    private enum Action { Down, Move, Up, Cancel, CaptureOut, Clear, Suspend, Resume }

    private readonly struct Event
    {
        public readonly Action Action;
        public readonly int Side;
        public readonly int Id;
        public readonly float X;
        public readonly float Y;
        public Event(Action action, int side = 0, int id = 0, float x = 0, float y = 0)
        { Action = action; Side = side; Id = id; X = x; Y = y; }
        public override string ToString() => Action + "(" + Side + "," + Id + "," + X + "," + Y + ")";
    }

    // Int-indexed arrays and explicit transitions keep this oracle separate from
    // the production branch structure and do not call FlightControlFrame helpers.
    private sealed class ReferenceState
    {
        public readonly int?[] Owner = new int?[2];
        public readonly float[,] Axis = new float[2, 2];
        public bool Suspended;

        public bool? Apply(Event e)
        {
            if (e.Action == Action.Clear || e.Action == Action.Suspend || e.Action == Action.Resume)
            {
                for (int side = 0; side != 2; side++) { Owner[side] = null; Zero(side); }
                if (e.Action != Action.Clear) Suspended = e.Action == Action.Suspend;
                return null;
            }
            if (e.Side < 0 || e.Side >= Owner.Length || e.Id < 0) return false;
            bool owns = Owner[e.Side].HasValue && Owner[e.Side].Value == e.Id;
            if (e.Action == Action.Down)
            {
                if (Suspended || Owner[e.Side].HasValue || !IsFinite(e.X) || !IsFinite(e.Y)) return false;
                for (int side = 0; side != 2; side++) if (Owner[side] == e.Id) return false;
                Owner[e.Side] = e.Id; Set(e.Side, e.X, e.Y); return true;
            }
            if (!owns) return false;
            if (e.Action == Action.Move)
            {
                if (Suspended) return false;
                if (IsFinite(e.X) && IsFinite(e.Y)) Set(e.Side, e.X, e.Y);
                else Zero(e.Side);
                return true;
            }
            Owner[e.Side] = null; Zero(e.Side); return true;
        }
        private void Set(int side, float x, float y) { Axis[side, 0] = Bound(x); Axis[side, 1] = Bound(y); }
        private void Zero(int side) { Axis[side, 0] = 0; Axis[side, 1] = 0; }
    }

    private sealed class Harness
    {
        public readonly TouchInputState Actual;
        public readonly ReferenceState Expected = new ReferenceState();
        private readonly string _name;
        public Harness(string name, string sourceId = "touch:independent")
        { _name = name; Actual = new TouchInputState(sourceId); Compare("initial"); }

        public void Step(Event e, string group, bool requireDual = false)
        {
            _events++;
            if (group == "directed") _directedEvents++;
            else if (group == "held") _heldEvents++;
            else if (group == "random") _randomEvents++;
            bool? want = Expected.Apply(e);
            bool? got;
            TouchStickSide side = (TouchStickSide)e.Side;
            switch (e.Action)
            {
                case Action.Down: got = Actual.Begin(side, e.Id, e.X, e.Y); break;
                case Action.Move: got = Actual.Move(side, e.Id, e.X, e.Y); break;
                case Action.Up: got = Actual.End(side, e.Id); break;
                case Action.Cancel: got = Actual.Cancel(side, e.Id); break;
                case Action.CaptureOut: got = Actual.CaptureOut(side, e.Id); break;
                case Action.Clear: Actual.Clear(); got = null; break;
                case Action.Suspend: Actual.Suspend(); got = null; break;
                default: Actual.Resume(); got = null; break;
            }
            string at = _name + "#" + _events + " " + e;
            Check(got == want, at + " return");
            Compare(at);
            if (Expected.Owner[0].HasValue && Expected.Owner[1].HasValue) _dualOwnedEvents++;
            if (Actual.LeftPointerId.HasValue && Actual.RightPointerId.HasValue) _actualDualOwnedEvents++;
            if (requireDual)
            {
                _requiredDualEvents++;
                Check(Expected.Owner[0].HasValue && Expected.Owner[1].HasValue, at + " oracle dual occupancy");
                Check(Actual.LeftPointerId.HasValue && Actual.RightPointerId.HasValue, at + " actual dual occupancy");
            }
        }

        private void Compare(string at)
        {
            Check(Actual.LeftPointerId == Expected.Owner[0], at + " left owner");
            Check(Actual.RightPointerId == Expected.Owner[1], at + " right owner");
            Equal(Actual.LeftX, Expected.Axis[0, 0], at + " left X");
            Equal(Actual.LeftY, Expected.Axis[0, 1], at + " left Y");
            Equal(Actual.RightX, Expected.Axis[1, 0], at + " right X");
            Equal(Actual.RightY, Expected.Axis[1, 1], at + " right Y");
            Check(Actual.IsSuspended == Expected.Suspended, at + " suspended");
            Check(Actual.SourceId == "touch:independent", at + " source property");
            InputSample sample = Actual.Snapshot();
            bool available = !Expected.Suspended && (Expected.Owner[0].HasValue || Expected.Owner[1].HasValue);
            Check(sample.SourceId == "touch:independent", at + " sample source");
            Check(sample.Kind == FlightInputSourceKind.Touch, at + " sample kind");
            Check(sample.DeviceIdentity == null, at + " no invented physical identity");
            Check(sample.Available == available, at + " sample available");
            Equal(sample.Throttle, available ? Expected.Axis[0, 1] : 0, at + " raw throttle=leftY");
            Equal(sample.Yaw, available ? Expected.Axis[0, 0] : 0, at + " raw yaw=leftX");
            Equal(sample.Pitch, available ? Expected.Axis[1, 1] : 0, at + " raw pitch=rightY");
            Equal(sample.Roll, available ? Expected.Axis[1, 0] : 0, at + " raw roll=rightX");
            Check(!Actual.LeftPointerId.HasValue || !Actual.RightPointerId.HasValue || Actual.LeftPointerId != Actual.RightPointerId, at + " unique owner");
            Check(IsFinite(Actual.LeftX) && IsFinite(Actual.LeftY) && IsFinite(Actual.RightX) && IsFinite(Actual.RightY), at + " finite axes");
        }
    }

    private static int _checks, _failures, _events, _directedEvents, _heldEvents, _randomEvents, _dualOwnedEvents, _actualDualOwnedEvents, _requiredDualEvents, _routerCases;
    private static readonly List<string> FailureSamples = new List<string>();
    private static readonly float[] Values = { 0, .01f, -.04f, .37f, -.61f, .88f, 1, -1, 1.9f, -9.4f, float.MaxValue, float.MinValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity };

    private static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Constructors();
        Directed();
        HeldDualOwnership();
        RandomTrace();
        RouterIntegration();
        foreach (string failure in FailureSamples) Console.Error.WriteLine("FAIL: " + failure);
        Console.WriteLine("INDEPENDENT_RESULT {\"checks\":" + _checks + ",\"failures\":" + _failures +
            ",\"events\":" + _events + ",\"directedEvents\":" + _directedEvents + ",\"heldStressEvents\":" + _heldEvents +
            ",\"requiredDualOwnerEvents\":" + _requiredDualEvents + ",\"randomEvents\":" + _randomEvents +
            ",\"oracleDualOwnedEvents\":" + _dualOwnedEvents + ",\"actualDualOwnedEvents\":" + _actualDualOwnedEvents + ",\"routerCases\":" + _routerCases +
            ",\"seed\":1251010,\"referenceStateMachine\":true}");
        return _failures == 0 ? 0 : 1;
    }

    private static void Constructors()
    {
        foreach (string id in new string[] { null, "", " ", "\t\r\n" })
        {
            bool threw = false;
            try { new TouchInputState(id); } catch (ArgumentException) { threw = true; }
            Check(threw, "constructor rejects absent/whitespace ID");
        }
        var initial = new TouchInputState();
        Check(initial.SourceId == "touch:dual" && initial.Snapshot().SourceId == "touch:dual", "default source identity");
        var spaced = new TouchInputState(" touch:kept ");
        Check(spaced.SourceId == " touch:kept " && spaced.Snapshot().SourceId == " touch:kept ", "source ID preserved exactly");
    }

    private static void Directed()
    {
        var h = new Harness("contract-sequence");
        Step(h, new Event(Action.Down, 0, 11, .37f, -.61f));
        Step(h, new Event(Action.Down, 1, 12, -.88f, .21f));
        Step(h, new Event(Action.Move, 0, 11, .44f, -.52f));
        Step(h, new Event(Action.Move, 1, 12, -.31f, .63f));
        foreach (int side in new[] { 0, 1 })
        {
            Step(h, new Event(Action.Down, side, 13, 1, 1)); // third pointer
            Step(h, new Event(Action.Down, side, side == 0 ? 11 : 12, -1, -1)); // duplicate Down
            Step(h, new Event(Action.Move, side, 13, float.NaN, float.PositiveInfinity));
            Step(h, new Event(Action.Move, side, side == 0 ? 12 : 11, -1, -1)); // cross-stick
            foreach (Action action in new[] { Action.Up, Action.Cancel, Action.CaptureOut })
            {
                Step(h, new Event(action, side, 13));
                Step(h, new Event(action, side, side == 0 ? 12 : 11));
            }
        }
        Step(h, new Event(Action.Cancel, 0, 11)); // must leave right owner and axes untouched
        Step(h, new Event(Action.CaptureOut, 0, 13));
        Step(h, new Event(Action.Down, 0, 12, 1, 1)); // right owner cannot acquire idle left
        Step(h, new Event(Action.Move, 0, 11, .9f, .9f));
        Step(h, new Event(Action.Up, 0, 11));
        Step(h, new Event(Action.Up, 1, 12));
        Step(h, new Event(Action.Up, 1, 12));

        // Each terminal operation in both directions, with the other owner active.
        foreach (Action terminal in new[] { Action.Up, Action.Cancel, Action.CaptureOut })
            foreach (int side in new[] { 0, 1 })
            {
                Step(h, new Event(Action.Clear));
                Step(h, new Event(Action.Down, 0, 41, .71f, -.81f));
                Step(h, new Event(Action.Down, 1, 42, -.91f, .61f));
                Step(h, new Event(terminal, side, side == 0 ? 41 : 42));
                Step(h, new Event(terminal, side, side == 0 ? 41 : 42));
                Step(h, new Event(Action.Move, 1 - side, side == 0 ? 42 : 41, .35f, -.25f));
            }

        Step(h, new Event(Action.Clear));
        Step(h, new Event(Action.Down, 0, 0, float.MaxValue, float.MinValue));
        Step(h, new Event(Action.Down, 1, int.MaxValue, 1, 1)); // square corners not circular normalization
        foreach (int side in new[] { -1, 2, int.MinValue, int.MaxValue })
            foreach (Action action in new[] { Action.Down, Action.Move, Action.Up, Action.Cancel, Action.CaptureOut })
                Step(h, new Event(action, side, 0, float.NaN, float.PositiveInfinity));
        foreach (int id in new[] { -1, int.MinValue })
            foreach (int side in new[] { 0, 1 })
                foreach (Action action in new[] { Action.Down, Action.Move, Action.Up, Action.Cancel, Action.CaptureOut })
                    Step(h, new Event(action, side, id, float.MaxValue, float.MinValue));
        foreach (int side in new[] { 0, 1 })
            foreach (float malformed in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                int owner = side == 0 ? 0 : int.MaxValue;
                Step(h, new Event(Action.Move, side, 17, malformed, .7f)); // nonowner no-op
                Step(h, new Event(Action.Move, side, owner, malformed, .7f)); // zero whole pair, retain owner
                Step(h, new Event(Action.Move, side, owner, .7f, -.8f));
                Step(h, new Event(Action.Move, side, owner, .7f, malformed));
                Step(h, new Event(Action.Move, side, owner, float.MaxValue, float.MinValue));
            }
        Step(h, new Event(Action.Clear));
        foreach (int side in new[] { 0, 1 })
            foreach (float malformed in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Step(h, new Event(Action.Down, side, 17, malformed, .7f));
                Step(h, new Event(Action.Down, side, 17, .7f, malformed));
            }

        // Repeated lifecycle operations must clear, never revive old bindings.
        Step(h, new Event(Action.Down, 0, 11, .4f, .5f));
        Step(h, new Event(Action.Down, 1, 12, .6f, .7f));
        Step(h, new Event(Action.Suspend));
        Step(h, new Event(Action.Suspend));
        Step(h, new Event(Action.Clear)); // preserve suspended
        foreach (Action action in new[] { Action.Down, Action.Move, Action.Up, Action.Cancel, Action.CaptureOut })
            Step(h, new Event(action, 0, 11, .9f, -.9f));
        Step(h, new Event(Action.Resume));
        Step(h, new Event(Action.Resume));
        Step(h, new Event(Action.Move, 0, 11, .9f, -.9f));
        Step(h, new Event(Action.Up, 1, 12));
        Step(h, new Event(Action.Down, 0, 31, .3f, .4f));
        Step(h, new Event(Action.Down, 1, 32, .5f, .6f));
        Step(h, new Event(Action.Clear));
        Step(h, new Event(Action.Move, 0, 31, .9f, .9f));
        Step(h, new Event(Action.CaptureOut, 1, 32));
        Step(h, new Event(Action.Down, 0, 31, .15f, .25f));
        Step(h, new Event(Action.Move, 0, 11, -.9f, -.9f)); // different stale ID rejected
        // Same-ID reuse cannot distinguish a delayed old event: adapter must reject
        // its generation before calling this API. This boundary is explicit, not a pass claim.
        Step(h, new Event(Action.Move, 0, 31, .45f, .55f));
        Step(h, new Event(Action.Resume)); // Resume while active also clears
        Step(h, new Event(Action.Move, 0, 31, .9f, .9f));
        Step(h, new Event(Action.Clear));
    }

    private static void HeldDualOwnership()
    {
        var h = new Harness("high-occupancy");
        for (int cycle = 0; cycle != 128; cycle++)
        {
            int left = 1000 + cycle * 3, right = left + 1, third = left + 2;
            h.Step(new Event(Action.Down, 0, left, .31f, -.47f), "held");
            h.Step(new Event(Action.Down, 1, right, -.57f, .67f), "held", true);
            for (int i = 0; i != 96; i++)
            {
                int side = i % 2, owner = side == 0 ? left : right, other = side == 0 ? right : left;
                Event e;
                switch ((i / 2) % 8)
                {
                    case 0: e = new Event(Action.Move, side, owner, (i % 5 - 2) * .37f, .79f); break;
                    case 1: e = new Event(Action.Down, side, third, -1, -1); break;
                    case 2: e = new Event(Action.Down, side, owner, 1, 1); break;
                    case 3: e = new Event(Action.Move, side, other, .9f, -.9f); break;
                    case 4: e = new Event(Action.CaptureOut, side, third); break;
                    case 5: e = new Event(Action.Cancel, side, other); break;
                    case 6: e = new Event(Action.Move, side, owner, float.NaN, .7f); break;
                    default: e = new Event(Action.Move, side, owner, float.MaxValue, float.MinValue); break;
                }
                h.Step(e, "held", true);
            }
            h.Step(new Event(Action.Cancel, 0, left), "held");
            h.Step(new Event(Action.Move, 1, right, -.61f, .73f), "held");
            h.Step(new Event(Action.CaptureOut, 0, third), "held");
            h.Step(new Event(Action.Down, 0, right, .9f, .9f), "held");
            h.Step(new Event(Action.Down, 0, third, -.41f, .51f), "held", true);
            h.Step(new Event(Action.Clear), "held");
            h.Step(new Event(Action.Move, 0, left, .9f, -.9f), "held");
            h.Step(new Event(Action.Up, 1, right), "held");
        }
        Check(_heldEvents == 13568, "all held stress events executed");
        Check(_dualOwnedEvents >= 12544, "high-occupancy sequence genuinely holds two owners");
    }

    private static void RandomTrace()
    {
        var h = new Harness("deterministic-reference-trace");
        var random = new Random(1251010);
        for (int i = 0; i != 12000; i++)
        {
            int choice = random.Next(100), side = random.Next(2);
            Action action = choice < 30 ? Action.Down : choice < 67 ? Action.Move : choice < 76 ? Action.Up :
                choice < 83 ? Action.Cancel : choice < 90 ? Action.CaptureOut : choice < 94 ? Action.Clear :
                choice < 97 ? Action.Suspend : Action.Resume;
            if (random.Next(20) == 0) side = random.Next(2) == 0 ? -1 : int.MaxValue;
            int id = random.Next(6);
            if (side >= 0 && side < 2 && random.Next(100) < 62 && h.Expected.Owner[side].HasValue)
                id = h.Expected.Owner[side].Value;
            if (random.Next(40) == 0) id = -1;
            if (random.Next(40) == 0) id = int.MaxValue;
            h.Step(new Event(action, side, id, Values[random.Next(Values.Length)], Values[random.Next(Values.Length)]), "random");
        }
        Check(_randomEvents == 12000, "entire deterministic trace executed");
    }

    private static void RouterIntegration()
    {
        var touch = new TouchInputState("touch:router-qa");
        var router = new InputRouter();
        router.SetFilter(touch.SourceId, InputFilterSettings.Linear);
        router.SelectFixed(touch.SourceId);
        var keyboard = new InputSample("keyboard:qa", FlightInputSourceKind.Keyboard, 1, 1, 1, 1);
        Frame(router.ReadFrame(new[] { touch.Snapshot(), keyboard }), 0, 0, 0, 0, false, "idle fixed cannot fall back");
        Check(touch.Begin(TouchStickSide.Left, 81, .31f, -.43f), "router setup left");
        Check(touch.Begin(TouchStickSide.Right, 82, -.67f, .79f), "router setup right");
        InputSample raw = touch.Snapshot();
        Frame(router.ReadFrame(new[] { raw, keyboard }), -.43f, .31f, .79f, -.67f, true, "linear raw mapping");
        Frame(router.ReadFrame(new[] { keyboard, raw }), -.43f, .31f, .79f, -.67f, true, "fixed enumeration order");
        Equal(raw.Yaw, .31f, "snapshot is not deadzone/expo filtered");
        Equal(raw.Pitch, .79f, "raw right axis retained");
        router.SetFilter(touch.SourceId, new InputFilterSettings(.2f, .6f, 1.2f, true, false, true, false));
        float throttle = Filter(-.43f, true), yaw = Filter(.31f, false), pitch = Filter(.79f, true), roll = Filter(-.67f, false);
        Frame(router.ReadFrame(new[] { raw, keyboard }), throttle, yaw, pitch, roll, true, "filter exactly once");
        Check(Math.Abs(yaw - Filter(yaw, false)) > .01f, "double-filter fixture observably different");
        Equal(touch.Snapshot().Yaw, .31f, "router leaves touch raw state unchanged");
        Check(touch.Cancel(TouchStickSide.Left, 81), "router cancel left");
        Frame(router.ReadFrame(new[] { touch.Snapshot() }), 0, 0, pitch, roll, true, "remaining right same single source");
        touch.Suspend();
        Frame(router.ReadFrame(new[] { touch.Snapshot(), keyboard }), 0, 0, 0, 0, false, "suspended unavailable fixed source");
        touch.Resume();
        Frame(router.ReadFrame(new[] { touch.Snapshot(), keyboard }), 0, 0, 0, 0, false, "resumed no new Down unavailable");
        Check(!touch.Move(TouchStickSide.Right, 82, 1, 1), "resume old pointer cannot revive sample");
        Check(touch.Begin(TouchStickSide.Left, 91, .1f, -.1f), "new Down after resume");
        Frame(router.ReadFrame(new[] { touch.Snapshot(), keyboard }), 0, 0, 0, 0, true, "available owner at deadzone has touch source");
        Frame(router.ReadFrame(new[] { keyboard }), 0, 0, 0, 0, false, "fixed missing source no keyboard fallback");
        Frame(router.ReadFrame(new[] { touch.Snapshot(), touch.Snapshot(), keyboard }), 0, 0, 0, 0, false, "duplicate touch IDs rejected");
        router.SelectAutomatic();
        Frame(router.ReadFrame(new[] { touch.Snapshot(), keyboard }), 1, 1, 1, 1, true, "automatic may choose active keyboard", "keyboard:qa", FlightInputSourceKind.Keyboard);
        router.SelectFixed(touch.SourceId); router.Suspend();
        Frame(router.ReadFrame(new[] { touch.Snapshot(), keyboard }), 0, 0, 0, 0, false, "router suspension neutral");
        router.Resume(); router.SetContext(FlightInputContext.Menu);
        Frame(router.ReadFrame(new[] { touch.Snapshot(), keyboard }), 0, 0, 0, 0, false, "router menu neutral");
    }

    private static float Filter(float raw, bool reverse)
    {
        // Independent double-precision specification computation. The tolerance
        // permits the production float arithmetic, not extra passes or remapping.
        double magnitude = Math.Abs((double)raw);
        if (magnitude <= .2) return 0;
        double value = Math.Sign(raw) * (magnitude - .2) / .8;
        if (reverse) value = -value;
        value = (value * .4 + value * value * value * .6) * 1.2;
        return (float)Math.Max(-1, Math.Min(1, value));
    }

    private static void Frame(FlightControlFrame frame, float throttle, float yaw, float pitch, float roll,
        bool source, string label, string id = "touch:router-qa", FlightInputSourceKind kind = FlightInputSourceKind.Touch)
    {
        _routerCases++;
        Near(frame.Throttle, throttle, label + " throttle"); Near(frame.Yaw, yaw, label + " yaw");
        Near(frame.Pitch, pitch, label + " pitch"); Near(frame.Roll, roll, label + " roll");
        Check(frame.HasSource == source, label + " has source");
        Check(frame.SourceId == (source ? id : ""), label + " source ID");
        Check(frame.SourceKind == (source ? kind : FlightInputSourceKind.None), label + " source kind");
    }

    private static void Step(Harness h, Event e) => h.Step(e, "directed");
    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static float Bound(float v) => (float)Math.Max(-1d, Math.Min(1d, v));
    private static void Equal(float actual, float expected, string label) => Check(actual == expected, label + " expected=" + expected + " got=" + actual);
    private static void Near(float actual, float expected, string label) => Check(IsFinite(actual) && Math.Abs(actual - expected) <= .000001f, label + " expected=" + expected + " got=" + actual);
    private static void Check(bool success, string label)
    {
        _checks++;
        if (success) return;
        _failures++;
        if (FailureSamples.Count < 12) FailureSamples.Add(label);
    }
}
