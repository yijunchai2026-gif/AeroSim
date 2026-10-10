using System;

namespace AeroSim.Input
{
    public enum TouchStickSide { Left, Right }

    // Pure ownership state for a dual-stick view. Adapters own geometry and
    // pointer capture; both sticks contribute to one unfiltered touch source.
    public sealed class TouchInputState
    {
        public string SourceId { get; }
        public int? LeftPointerId { get; private set; }
        public int? RightPointerId { get; private set; }
        public float LeftX { get; private set; }
        public float LeftY { get; private set; }
        public float RightX { get; private set; }
        public float RightY { get; private set; }
        public bool IsSuspended { get; private set; }

        public TouchInputState(string sourceId = "touch:dual")
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("A source ID is required.", nameof(sourceId));
            SourceId = sourceId;
        }

        public bool Begin(TouchStickSide side, int pointerId, float x, float y)
        {
            if (IsSuspended || !ValidSide(side) || pointerId < 0 ||
                !FlightControlFrame.Finite(x) || !FlightControlFrame.Finite(y)) return false;
            // Repeated Down is not a Move, and one pointer cannot own two sticks.
            if (side == TouchStickSide.Left)
            {
                if (LeftPointerId.HasValue || RightPointerId == pointerId) return false;
                LeftPointerId = pointerId;
            }
            else
            {
                if (RightPointerId.HasValue || LeftPointerId == pointerId) return false;
                RightPointerId = pointerId;
            }
            SetAxes(side, x, y);
            return true;
        }

        public bool Move(TouchStickSide side, int pointerId, float x, float y)
        {
            if (IsSuspended || !Owns(side, pointerId)) return false;
            // Invalid geometry fails closed as a pair, retaining capture ownership.
            if (!FlightControlFrame.Finite(x) || !FlightControlFrame.Finite(y))
                SetAxes(side, 0, 0);
            else SetAxes(side, x, y);
            return true;
        }

        public bool End(TouchStickSide side, int pointerId)
        {
            if (!Owns(side, pointerId)) return false;
            if (side == TouchStickSide.Left) LeftPointerId = null;
            else RightPointerId = null;
            SetAxes(side, 0, 0);
            return true;
        }

        public bool Cancel(TouchStickSide side, int pointerId) => End(side, pointerId);
        public bool CaptureOut(TouchStickSide side, int pointerId) => End(side, pointerId);

        public void Clear()
        {
            LeftPointerId = null;
            RightPointerId = null;
            LeftX = 0; LeftY = 0; RightX = 0; RightY = 0;
        }

        public void Suspend() { Clear(); IsSuspended = true; }
        public void Resume() { Clear(); IsSuspended = false; }

        public InputSample Snapshot()
        {
            bool available = !IsSuspended && (LeftPointerId.HasValue || RightPointerId.HasValue);
            return new InputSample(SourceId, FlightInputSourceKind.Touch,
                available ? LeftY : 0, available ? LeftX : 0,
                available ? RightY : 0, available ? RightX : 0,
                available: available);
        }

        private bool Owns(TouchStickSide side, int pointerId)
        {
            if (pointerId < 0 || !ValidSide(side)) return false;
            return side == TouchStickSide.Left ? LeftPointerId == pointerId : RightPointerId == pointerId;
        }

        private static bool ValidSide(TouchStickSide side) =>
            side == TouchStickSide.Left || side == TouchStickSide.Right;

        private void SetAxes(TouchStickSide side, float x, float y)
        {
            x = FlightControlFrame.Clamp(x, -1, 1);
            y = FlightControlFrame.Clamp(y, -1, 1);
            if (side == TouchStickSide.Left) { LeftX = x; LeftY = y; }
            else { RightX = x; RightY = y; }
        }
    }
}
