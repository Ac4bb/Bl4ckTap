using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Bl4ckTapApp
{
    internal sealed class EngineStatus
    {
        public readonly bool Ready, Enabled, Allowed, LeftHeld, RightHeld, CapturingShortcut;
        public readonly string Error;
        public readonly long LeftCount, RightCount;
        public readonly ShortcutBinding Shortcut;
        public readonly int ShortcutRevision;
        public EngineStatus(bool ready, bool enabled, bool allowed, bool left, bool right, string error,
            long lc, long rc, ShortcutBinding shortcut, int revision, bool capturing)
        {
            Ready = ready; Enabled = enabled; Allowed = allowed; LeftHeld = left; RightHeld = right;
            Error = error; LeftCount = lc; RightCount = rc; Shortcut = shortcut;
            ShortcutRevision = revision; CapturingShortcut = capturing;
        }
    }

    internal sealed class InputFrame
    {
        public readonly ClickOptions Options;
        public readonly bool Enabled, LeftHeld, RightHeld;
        public readonly int Generation, LeftRelease, RightRelease;
        public readonly long Timestamp;
        public InputFrame(ClickOptions options, bool enabled, PhysicalButton left, PhysicalButton right, int generation)
        {
            Options = options; Enabled = enabled; LeftHeld = left.Held; RightHeld = right.Held;
            Generation = generation; LeftRelease = left.ReleaseVersion; RightRelease = right.ReleaseVersion;
            Timestamp = Stopwatch.GetTimestamp();
        }
    }

    internal sealed class ClickEngine : IDisposable
    {
        // Input thread owns hook/physical state. Output thread alone calls SendInput.
        // No lock is held across SendInput or inside the hook.
        private readonly Thread inputThread, outputThread;
        private readonly AutoResetEvent signal = new AutoResetEvent(false), outputSignal = new AutoResetEvent(false);
        private readonly ConcurrentQueue<Action> commands = new ConcurrentQueue<Action>();
        private readonly PhysicalButton physicalLeft = new PhysicalButton(), physicalRight = new PhysicalButton();
        private readonly ShortcutMouseInput shortcutMouse = new ShortcutMouseInput();
        private readonly Native.MouseProc callback;
        private readonly uint ownProcess = (uint)Process.GetCurrentProcess().Id;
        private readonly Func<bool, bool, bool> testOutput;
        private readonly Func<bool> testTarget;
        private ClickOptions options;
        private ShortcutBinding shortcut, pendingMouseShortcut;
        private IntPtr hook;
        private bool ready, enabled, capturingShortcut, toggleRegistered, stopRegistered;
        private volatile bool running = true, outputRunning = true, allowed;
        private int disposed, generation, shortcutRevision, recoveryPending;
        private string error;
        private long leftCount, rightCount, ownHookEvents;
        private volatile EngineStatus status;
        private volatile InputFrame frame;

        public EngineStatus Status { get { return status; } }
        public ClickEngine(ClickOptions initial) : this(initial, ShortcutBinding.Mouse(4)) { }
        public ClickEngine(ClickOptions initial, ShortcutBinding binding) : this(initial, binding, null, null) { }
        internal ClickEngine(ClickOptions initial, ShortcutBinding binding, Func<bool, bool, bool> output, Func<bool> target)
        {
            options = initial; shortcut = binding; callback = MouseHook; testOutput = output; testTarget = target;
            Publish();
            inputThread = new Thread(InputLoop) { IsBackground = true, Name = "Bl4ckTap physical input" };
            outputThread = new Thread(OutputLoop) { IsBackground = true, Name = "Bl4ckTap pulse output" };
            outputThread.Start(); inputThread.Start();
        }

        public void Configure(ClickOptions value)
        {
            Post(delegate
            {
                generation++;
                if (options.Hold != value.Hold) Disable();
                options = value;
                if (!value.Left || !enabled) physicalLeft.Reset();
                if (!value.Right || !enabled) physicalRight.Reset();
                if (!value.Left && !value.Right) Disable();
            });
        }
        public void Toggle() { Post(delegate { SetEnabled(!enabled); }); }
        public void Stop() { Post(delegate { Disable(); CancelCapture(); }); }
        public void ChangeShortcut(ShortcutBinding value)
        {
            Post(delegate { Disable(); capturingShortcut = false; pendingMouseShortcut = null; ApplyShortcut(value); shortcutRevision++; });
        }
        public void BeginShortcutCapture()
        {
            Post(delegate
            {
                Disable(); InstallHook(); shortcutMouse.Reset(); capturingShortcut = true; pendingMouseShortcut = null;
                if (toggleRegistered) Native.UnregisterHotKey(IntPtr.Zero, 1);
                toggleRegistered = false;
            });
        }
        public void CancelShortcutCapture() { Post(CancelCapture); }

        private void Post(Action action)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            commands.Enqueue(action);
            try { signal.Set(); } catch (ObjectDisposedException) { }
        }
        private void Disable()
        {
            enabled = false; generation++; physicalLeft.Reset(); physicalRight.Reset();
            // Side-button edge latches survive until UP to prevent an immediate re-toggle.
        }
        private void SetEnabled(bool value)
        {
            if (!value) { Disable(); return; }
            if (ready && !capturingShortcut && (options.Left || options.Right)) { enabled = true; error = null; }
        }
        private void CancelCapture()
        {
            if (!capturingShortcut) return;
            capturingShortcut = false; pendingMouseShortcut = null; ApplyShortcut(shortcut); shortcutRevision++;
        }
        private void ApplyShortcut(ShortcutBinding value)
        {
            ShortcutBinding previous = shortcut;
            if (toggleRegistered) Native.UnregisterHotKey(IntPtr.Zero, 1);
            toggleRegistered = false;
            if (value.IsMouse || Native.RegisterHotKey(IntPtr.Zero, 1, 0x4000, value.Key))
            {
                shortcut = value; toggleRegistered = !value.IsMouse; error = null; return;
            }
            int failure = Marshal.GetLastWin32Error(); shortcut = previous;
            if (!previous.IsMouse)
            {
                toggleRegistered = Native.RegisterHotKey(IntPtr.Zero, 1, 0x4000, previous.Key);
                if (!toggleRegistered) shortcut = ShortcutBinding.Mouse(4);
            }
            error = value.Name + " indisponible (" + failure + "). Raccourci conservé : " + shortcut.Name + ".";
        }
        private void InstallHook()
        {
            IntPtr replacement = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
            if (replacement == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Détection de souris indisponible.");
            IntPtr previous = hook; hook = replacement;
            if (previous != IntPtr.Zero) Native.UnhookWindowsHookEx(previous);
        }

        private void InputLoop()
        {
            try
            {
                Native.Message message;
                Native.PeekMessage(out message, IntPtr.Zero, 0, 0, 0); InstallHook();
                stopRegistered = Native.RegisterHotKey(IntPtr.Zero, 2, 0x4000, 0x77);
                if (!stopRegistered) throw new Win32Exception(Marshal.GetLastWin32Error(), "F8 indisponible. Ferme l'autre appli utilisant cette touche.");
                ApplyShortcut(shortcut); ready = true;
                long renewal = Stopwatch.GetTimestamp();
                IntPtr[] handles = { signal.SafeWaitHandle.DangerousGetHandle() };
                while (running)
                {
                    for (int i = 0; i < 256 && Native.PeekMessage(out message, IntPtr.Zero, 0, 0, 1); i++)
                    {
                        if (message.Id != 0x0312) continue;
                        if (message.WParam.ToUInt64() == 2) { Disable(); CancelCapture(); }
                        else if (message.WParam.ToUInt64() == 1 && toggleRegistered) SetEnabled(!enabled);
                    }
                    Action action;
                    for (int i = 0; i < 128 && commands.TryDequeue(out action); i++) action();
                    if (capturingShortcut && pendingMouseShortcut != null)
                    {
                        ShortcutBinding captured = pendingMouseShortcut;
                        pendingMouseShortcut = null; capturingShortcut = false; ApplyShortcut(captured); shortcutRevision++;
                    }
                    if (!enabled && !capturingShortcut && MillisecondsSince(renewal) >= 10000)
                    {
                        InstallHook(); physicalLeft.Reset(); physicalRight.Reset(); shortcutMouse.Reset(); renewal = Stopwatch.GetTimestamp();
                    }
                    Publish();
                    if (!running) break;
                    if (Native.MsgWaitForMultipleObjectsEx(1, handles, 25, 0x04FF, 0x0004) == UInt32.MaxValue)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex) { error = ex.Message; }
            finally
            {
                Disable(); ready = false; Publish(); outputRunning = false; outputSignal.Set();
                // Keep servicing the hook until final generated UPs have completed.
                while (outputThread.IsAlive)
                {
                    Native.Message message; Native.PeekMessage(out message, IntPtr.Zero, 0, 0, 1); outputThread.Join(5);
                }
                if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
                if (toggleRegistered) Native.UnregisterHotKey(IntPtr.Zero, 1);
                if (stopRegistered) Native.UnregisterHotKey(IntPtr.Zero, 2);
                Publish();
            }
        }
        private void Publish()
        {
            frame = new InputFrame(options, enabled && ready, physicalLeft, physicalRight, generation);
            status = new EngineStatus(ready, enabled, allowed, physicalLeft.Held, physicalRight.Held, error,
                Interlocked.Read(ref leftCount), Interlocked.Read(ref rightCount), shortcut, shortcutRevision, capturingShortcut);
            outputSignal.Set();
        }
        private static double MillisecondsSince(long stamp) { return (Stopwatch.GetTimestamp() - stamp) * 1000.0 / Stopwatch.Frequency; }
        private void RequestRecovery(string reason)
        {
            if (Interlocked.Exchange(ref recoveryPending, 1) != 0) return;
            Post(delegate { Disable(); shortcutMouse.Reset(); InstallHook(); error = reason; Interlocked.Exchange(ref recoveryPending, 0); });
        }

        private void OutputLoop()
        {
            PulseButton left = new PulseButton(), right = new PulseButton();
            bool precision = false;
            int seenGeneration = -1, leftRelease = -1, rightRelease = -1, blockedGeneration = -1;
            long expectedOwnEvents = Interlocked.Read(ref ownHookEvents), unacknowledgedSince = 0;
            Stopwatch time = Stopwatch.StartNew();
            Func<bool, bool, bool> send = delegate(bool leftButton, bool down)
            {
                bool success;
                if (testOutput != null) success = testOutput(leftButton, down);
                else
                {
                    Native.Input input = new Native.Input();
                    input.Mouse.Flags = leftButton ? (down ? 2U : 4U) : (down ? 8U : 16U);
                    input.Mouse.ExtraInfo = new UIntPtr(Native.Bl4ckTapMarker);
                    success = Native.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Native.Input))) == 1;
                    if (success) { expectedOwnEvents++; if (unacknowledgedSince == 0) unacknowledgedSince = Stopwatch.GetTimestamp(); }
                }
                if (success && down) { if (leftButton) Interlocked.Increment(ref leftCount); else Interlocked.Increment(ref rightCount); }
                return success;
            };
            Func<bool, bool> sendLeft = delegate(bool down) { return send(true, down); };
            Func<bool, bool> sendRight = delegate(bool down) { return send(false, down); };
            try
            {
                while (outputRunning)
                {
                    InputFrame current = frame;
                    bool released = true;
                    if (seenGeneration != current.Generation)
                    {
                        released = left.Release(sendLeft) & right.Release(sendRight); seenGeneration = current.Generation;
                        expectedOwnEvents = Interlocked.Read(ref ownHookEvents); unacknowledgedSince = 0;
                    }
                    if (leftRelease != current.LeftRelease) { released &= left.Release(sendLeft); leftRelease = current.LeftRelease; }
                    if (rightRelease != current.RightRelease) { released &= right.Release(sendRight); rightRelease = current.RightRelease; }
                    bool stalled = current.Enabled && MillisecondsSince(current.Timestamp) > 250;
                    bool missingHook = testOutput == null && unacknowledgedSince != 0
                        && Interlocked.Read(ref ownHookEvents) < expectedOwnEvents && MillisecondsSince(unacknowledgedSince) > 250;
                    if (Interlocked.Read(ref ownHookEvents) >= expectedOwnEvents) unacknowledgedSince = 0;
                    if (stalled || missingHook)
                    {
                        blockedGeneration = current.Generation;
                        RequestRecovery("Détection réinitialisée. Relâche les boutons, puis réactive l'auto clicker.");
                    }
                    allowed = testTarget != null ? testTarget() : IsTargetAllowed();
                    bool canClick = current.Enabled && allowed && blockedGeneration != current.Generation && !stalled && !missingHook;
                    bool l = current.Options.Wants(true, current.LeftHeld, canClick, true);
                    bool r = current.Options.Wants(false, current.RightHeld, canClick, true);
                    bool active = l || r || left.IsDown || right.IsDown;
                    if (active && !precision) precision = Native.timeBeginPeriod(1) == 0;
                    if (!active && precision) { Native.timeEndPeriod(1); precision = false; }
                    double now = time.Elapsed.TotalMilliseconds;
                    bool goodLeft = left.Tick(now, l, 1000.0 / current.Options.Cps, sendLeft);
                    InputFrame fresh = frame;
                    r &= fresh.Enabled && fresh.Generation == current.Generation && (!fresh.Options.Hold || fresh.RightHeld);
                    bool goodRight = right.Tick(now, r && goodLeft, 1000.0 / current.Options.Cps, sendRight);
                    if (!released || !goodLeft || !goodRight)
                    {
                        blockedGeneration = current.Generation; left.Release(sendLeft); right.Release(sendRight);
                        Post(delegate { Disable(); error = "Windows a refusé un clic. Vérifie les droits des deux applications."; });
                    }
                    outputSignal.WaitOne(active ? 2 : 15);
                }
            }
            catch (Exception ex)
            {
                string failure = ex.Message; Post(delegate { Disable(); ready = false; error = "Moteur arrêté : " + failure; });
            }
            finally
            {
                for (int i = 0; i < 3 && (left.IsDown || right.IsDown); i++) { left.Release(sendLeft); right.Release(sendRight); }
                if (precision) Native.timeEndPeriod(1);
            }
        }
        private bool IsOwnWindow(IntPtr window)
        {
            uint process; Native.GetWindowThreadProcessId(window, out process); return process == ownProcess;
        }
        private bool IsTargetAllowed()
        {
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero || IsOwnWindow(foreground)) return false;
            Native.Point cursor;
            return Native.GetCursorPos(out cursor) && !IsOwnWindow(Native.WindowFromPoint(cursor));
        }

        private IntPtr MouseHook(int code, IntPtr message, IntPtr data)
        {
            if (code < 0) return Native.CallNextHookEx(hook, code, message, data);
            int id = message.ToInt32();
            bool primary = id == 0x0201 || id == 0x0202 || id == 0x0204 || id == 0x0205;
            bool extra = id == 0x0207 || id == 0x0208 || id == 0x020B || id == 0x020C;
            if (!primary && !extra) return Native.CallNextHookEx(hook, code, message, data);
            Native.MouseHookData info = (Native.MouseHookData)Marshal.PtrToStructure(data, typeof(Native.MouseHookData));
            if (Native.IsInjected(info.Flags))
            {
                if (Native.IsBl4ckTapInput(info.Flags, info.ExtraInfo)) Interlocked.Increment(ref ownHookEvents);
                return Native.CallNextHookEx(hook, code, message, data);
            }
            if (primary)
            {
                bool isLeft = id == 0x0201 || id == 0x0202;
                (isLeft ? physicalLeft : physicalRight).Process(id == 0x0201 || id == 0x0204, false, false);
            }
            else
            {
                ShortcutBinding match = shortcut;
                if (capturingShortcut)
                {
                    int button = id == 0x0207 || id == 0x0208 ? 3 : (int)(info.MouseData >> 16) + 3;
                    if (button >= 3 && button <= 5) match = ShortcutBinding.Mouse(button);
                }
                ShortcutMouseResult action = shortcutMouse.Process(id, info.MouseData, info.Flags, match, ready, info.ExtraInfo);
                if (action.Toggle)
                {
                    if (capturingShortcut) { if (pendingMouseShortcut == null) pendingMouseShortcut = match; }
                    else SetEnabled(!enabled);
                }
                if (action.Consumed) return new IntPtr(1);
            }
            return Native.CallNextHookEx(hook, code, message, data);
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            commands.Enqueue(delegate { running = false; }); signal.Set();
            inputThread.Join(); outputThread.Join(); signal.Dispose(); outputSignal.Dispose();
        }
    }
}
