using System;

namespace ShroomMouse
{
    // Independent of UI and native APIs so timing and cancellation can be verified.
    public sealed class Movement
    {
        readonly Func<int, bool, bool> send;
        public int Key { get; private set; }
        public bool PointerHeld { get; private set; }
        public bool Failed { get; private set; }
        long started;
        public Movement(Func<int, bool, bool> sender) { send = sender; }
        public bool Press(int key, long now, bool allowed)
        {
            if (!allowed || (key != 0x57 && key != 0x53)) return false;
            Cancel();
            if (Key != 0) return false;
            Failed = false;
            if (!send(key, true)) { Failed = true; return false; }
            Key = key; started = now; PointerHeld = true;
            return true;
        }
        public void ReleasePointer(long now)
        {
            PointerHeld = false;
            if (now - started >= 100) Cancel();
        }
        public void Tick(long now, bool allowed, bool inside)
        {
            if (Key == 0) return;
            if (!allowed || !inside || now - started >= 30000 || (!PointerHeld && now - started >= 100)) Cancel();
        }
        public void Cancel()
        {
            PointerHeld = false;
            if (Key != 0)
            {
                // Preserve ownership when key-up fails so the timer can retry it.
                if (send(Key, false)) Key = 0;
                else Failed = true;
            }
        }
    }
}
