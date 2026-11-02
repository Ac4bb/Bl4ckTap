using System;
using System.Runtime.InteropServices;

namespace Bl4ckTapApp
{
    internal static class Native
    {
        internal const uint Bl4ckTapMarker = 0x42414331;

        internal static bool IsBl4ckTapInput(uint flags, UIntPtr extraInfo)
        {
            // Mouse software may inject legitimate user input. Exclude only our own pulses.
            return (flags & 3) != 0 && extraInfo.ToUInt64() == Bl4ckTapMarker;
        }

        internal delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
        internal static bool IsInjected(uint flags) { return (flags & 3) != 0; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseHookData
        {
            public Point Point;
            public uint MouseData, Flags, Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseInput
        {
            public int X, Y;
            public uint MouseData, Flags, Time;
            public UIntPtr ExtraInfo;
        }

        // MOUSEINPUT is the largest member of the native INPUT union.
        [StructLayout(LayoutKind.Sequential)]
        internal struct Input { public uint Type; public MouseInput Mouse; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Message
        {
            public IntPtr Window;
            public uint Id;
            public UIntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public Point Point;
            public uint Private;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int type, MouseProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")]
        internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")]
        internal static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint MsgWaitForMultipleObjectsEx(uint count, IntPtr[] handles, uint timeout, uint wakeMask, uint flags);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetMessageExtraInfo();
        [DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint period);
    }
}
