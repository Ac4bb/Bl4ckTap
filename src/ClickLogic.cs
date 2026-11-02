using System;

namespace Bl4ckTapApp
{
    internal sealed class ClickOptions
    {
        public readonly bool Left, Right, Hold;
        public readonly int Cps;

        public ClickOptions(bool left, bool right, bool hold, int cps)
        {
            Left = left;
            Right = right;
            Hold = hold;
            Cps = Math.Max(1, Math.Min(30, cps));
        }

        public bool Wants(bool leftButton, bool held, bool enabled, bool allowed)
        {
            return enabled && allowed && (leftButton ? Left : Right) && (!Hold || held);
        }
    }

    // User/driver events change Held; our own generated pulses must never sustain a hold.
    internal sealed class PhysicalButton
    {
        public bool Held { get; private set; }
        private bool swallowed;
        public int ReleaseVersion { get; private set; }
        public void Reset() { Held = false; swallowed = false; ReleaseVersion++; }

        public bool Process(bool down, bool injected, bool intercept)
        {
            if (injected) return false;
            if (!down && Held) ReleaseVersion++;
            Held = down;
            if (down)
            {
                swallowed = swallowed || intercept;
                return swallowed;
            }
            bool result = swallowed;
            swallowed = false;
            return result;
        }
    }

    // A separate schedule for each button lets one stop while the other continues.
    internal sealed class PulseButton
    {
        public bool IsDown { get; private set; }
        private double nextDown, releaseAt;

        public bool Tick(double now, bool wanted, double interval, Func<bool, bool> send)
        {
            if (IsDown && (!wanted || now >= releaseAt))
            {
                if (!send(false)) return false;
                IsDown = false;
            }
            if (!wanted)
            {
                nextDown = now;
                return true;
            }
            if (!IsDown && now >= nextDown)
            {
                if (!send(true)) return false;
                IsDown = true;
                releaseAt = now + 10;
                // Missed intervals are skipped: never burst after a stalled thread.
                nextDown = now + interval;
            }
            return true;
        }

        public bool Release(Func<bool, bool> send)
        {
            nextDown = 0;
            if (!IsDown) return true;
            if (!send(false)) return false;
            IsDown = false;
            return true;
        }
    }
}
