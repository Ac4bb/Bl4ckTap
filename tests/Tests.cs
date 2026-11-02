using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Bl4ckTapApp
{
    internal static class Tests
    {
        private static int checks;
        private static void Check(bool success, string description)
        {
            checks++;
            if (!success) throw new Exception("FAIL: " + description);
        }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--probe")
                {
                    for (uint key = 0x75; key <= 0x7B; key++)
                    {
                        bool success = Native.RegisterHotKey(IntPtr.Zero, 201, 0x4000, key);
                        int error = Marshal.GetLastWin32Error();
                        Console.WriteLine("F" + (key - 0x6F) + ": " + (success ? "available" : "error " + error));
                        if (success) Native.UnregisterHotKey(IntPtr.Zero, 201);
                    }
                }
                else if (args.Length > 0 && args[0] == "--smoke") { Smoke(); TestThreadIsolation(); }
                else
                {
                    TestSelection();
                    TestPhysicalButtons();
                    TestIndependentHold();
                    TestTimingAndRelease();
                    TestFailures();
                    TestMouseShortcut();
                    TestShortcutSettings();
                    TestDriverInput();
                    StressPhysicalInput();
                    Check(Marshal.SizeOf(typeof(Native.Input)) == (IntPtr.Size == 8 ? 40 : 28), "Native INPUT matches Windows ABI");
                    Check(Marshal.SizeOf(typeof(Native.MouseHookData)) == (IntPtr.Size == 8 ? 32 : 24), "Native hook structure matches Windows ABI");
                }
                Console.WriteLine("PASS: " + checks + " checks. No real clicks sent.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static void TestSelection()
        {
            for (int selection = 0; selection < 4; selection++)
            for (int held = 0; held < 4; held++)
            for (int holdMode = 0; holdMode < 2; holdMode++)
            {
                ClickOptions opts = new ClickOptions((selection & 1) != 0, (selection & 2) != 0, holdMode == 1, 12);
                int actual = (opts.Wants(true, (held & 1) != 0, true, true) ? 1 : 0)
                    | (opts.Wants(false, (held & 2) != 0, true, true) ? 2 : 0);
                int expected = holdMode == 1 ? selection & held : selection;
                Check(actual == expected, "Button/mode combination " + selection + "/" + held + "/" + holdMode);
                Check(!opts.Wants(true, true, false, true), "Disabled means no left clicks");
                Check(!opts.Wants(false, true, true, false), "App focus means no right clicks");
            }
            Check(new ClickOptions(true, true, true, 0).Cps == 1, "Minimum speed");
            Check(new ClickOptions(true, true, true, 100).Cps == 30, "Maximum speed");
        }

        private static void TestPhysicalButtons()
        {
            PhysicalButton button = new PhysicalButton();
            Check(button.Process(true, false, true), "Selected hardware down is replaced");
            Check(button.Held, "Hardware down starts hold");
            Check(!button.Process(false, true, false), "Generated up propagates");
            Check(button.Held, "Generated up cannot cancel physical hold");
            Check(button.Process(false, false, false), "Matched hardware up stays consumed after stop");
            Check(!button.Held, "Hardware up stops hold");
            button.Process(true, true, true);
            Check(!button.Held, "Generated down cannot create a self-sustaining hold");
            Check(!button.Process(true, false, false), "Unselected hardware click passes through");
            Check(!button.Process(false, false, true), "An unconsumed down has an unconsumed up");
        }

        private static void TestIndependentHold()
        {
            PhysicalButton hardwareL = new PhysicalButton(), hardwareR = new PhysicalButton();
            PulseButton l = new PulseButton(), r = new PulseButton();
            ClickOptions opts = new ClickOptions(true, true, true, 10);
            List<bool> le = new List<bool>(), re = new List<bool>();
            Func<bool, bool> sl = delegate(bool down) { le.Add(down); hardwareL.Process(down, true, false); return true; };
            Func<bool, bool> sr = delegate(bool down) { re.Add(down); hardwareR.Process(down, true, false); return true; };
            hardwareL.Process(true, false, true);
            for (int t = 0; t < 600; t++)
            {
                if (t == 200) hardwareR.Process(true, false, true);
                if (t == 400) hardwareL.Process(false, false, true);
                if (t == 500) hardwareR.Process(false, false, true);
                l.Tick(t, opts.Wants(true, hardwareL.Held, true, true), 100, sl);
                r.Tick(t, opts.Wants(false, hardwareR.Held, true, true), 100, sr);
                if (t == 199) Check(re.Count == 0, "Holding left alone never starts right");
                if (t == 399) Check(le.Count == 8 && re.Count == 4, "Both held click independently");
                if (t == 499) Check(le.Count == 8 && re.Count == 6, "Releasing left keeps right running");
            }
            Check(le.Count == 8 && re.Count == 6, "No more clicks after both hardware releases");
            Check(!l.IsDown && !r.IsDown, "Neither mouse button remains down");
        }

        private static void TestTimingAndRelease()
        {
            foreach (int cps in new[] { 1, 12, 30 })
            {
                PulseButton pulse = new PulseButton();
                int downs = 0, ups = 0;
                Func<bool, bool> send = delegate(bool down) { if (down) downs++; else ups++; return true; };
                for (int t = 0; t < 1000; t++) pulse.Tick(t, true, 1000.0 / cps, send);
                pulse.Release(send);
                Check(downs == cps && downs == ups, "One second at " + cps + " CPS with balanced releases");
            }
            PulseButton p = new PulseButton();
            List<bool> events = new List<bool>();
            Func<bool, bool> capture = delegate(bool down) { events.Add(down); return true; };
            p.Tick(0, true, 100, capture);
            p.Tick(4, false, 100, capture);
            Check(events.Count == 2 && events[0] && !events[1], "Stop immediately releases an in-flight pulse");
            p.Tick(5, true, 100, capture);
            Check(events.Count == 3 && events[2], "New physical hold starts immediately");
            p.Tick(10000, true, 100, capture);
            Check(events.Count == 5, "No catch-up burst after a long stall");
            p.Release(capture);
            p.Release(capture);
            Check(events.Count == 6, "Shutdown release is idempotent");
        }

        private static void TestFailures()
        {
            PulseButton p = new PulseButton();
            Check(!p.Tick(0, true, 100, delegate { return false; }), "Rejected down reported");
            Check(!p.IsDown, "Rejected down does not become a held button");
            p.Tick(1, true, 100, delegate { return true; });
            Check(!p.Release(delegate { return false; }) && p.IsDown, "Failed release remains pending");
            Check(p.Release(delegate { return true; }) && !p.IsDown, "Release can be retried");
        }

        private static void TestMouseShortcut()
        {
            ShortcutMouseInput input = new ShortcutMouseInput();
            ShortcutBinding mouse4 = ShortcutBinding.Mouse(4);
            ShortcutMouseResult result = input.Process(0x020B, 1U << 16, 0, mouse4, true);
            Check(result.Toggle && result.Consumed, "Mouse 4 activates and consumes its physical press");
            result = input.Process(0x020B, 1U << 16, 0, mouse4, true);
            Check(!result.Toggle && result.Consumed, "A held shortcut cannot repeatedly toggle");
            result = input.Process(0x020C, 1U << 16, 0, mouse4, true);
            Check(!result.Toggle && result.Consumed, "Mouse 4 release is consumed without a second toggle");
            Check(input.Process(0x020B, 1U << 16, 0, mouse4, true).Toggle, "The next press toggles again");
            result = input.Process(0x020C, 1U << 16, 0, ShortcutBinding.Mouse(5), true);
            Check(result.Consumed && !result.Toggle, "A binding change still consumes the matching release");
            result = input.Process(0x020B, 2U << 16, 0, mouse4, true);
            Check(!result.Toggle && !result.Consumed, "Unassigned side button passes through");
            input.Process(0x020C, 2U << 16, 0, mouse4, true);
            result = input.Process(0x020B, 1U << 16, 1, mouse4, true, new UIntPtr(Native.Bl4ckTapMarker));
            Check(!result.Toggle && !result.Consumed, "Our own injected side clicks cannot toggle");
            result = input.Process(0x020B, 1U << 16, 0, mouse4, true);
            Check(result.Toggle, "Injected press does not corrupt physical shortcut state");
            input.Process(0x020C, 1U << 16, 0, mouse4, true);
            Check(!input.Process(0x020B, 1, 0, mouse4, true).Toggle, "X button must come from high word");
            Check(!input.Process(0x020B, 1U << 16, 0, mouse4, false).Toggle, "Unavailable engine ignores shortcut");
            input.Process(0x020C, 1U << 16, 0, mouse4, false);
            Check(!input.Process(0x0201, 0, 0, mouse4, true).Consumed, "Main mouse buttons are not shortcut input");
            Check(input.Process(0x0207, 0, 0, ShortcutBinding.Mouse(3), true).Toggle, "Middle button can be assigned");
            Check(input.Process(0x020B, 2U << 16, 0, ShortcutBinding.Mouse(5), true).Toggle, "Mouse 5 can be assigned");
        }

        private static void TestShortcutSettings()
        {
            foreach (string id in new[] { "mouse:3", "mouse:4", "mouse:5", "key:117", "key:65" })
                Check(ShortcutBinding.Parse(id).Id == id, "Shortcut preference round-trip " + id);
            foreach (string id in new[] { "", "broken", "key:119", "key:123", "key:9999", "mouse:1", null })
                Check(ShortcutBinding.Parse(id).Id == "mouse:4", "Missing or invalid binding defaults to Mouse 4");
            Check(!ShortcutBinding.SupportsKey(Keys.F8), "F8 remains reserved for stop");
            Check(!ShortcutBinding.SupportsKey(Keys.LButton), "Cannot bind repeated mouse button");
            Check(ShortcutBinding.SupportsKey(Keys.F6) && ShortcutBinding.SupportsKey(Keys.A), "Keyboard choices are supported");
        }

        private static void TestDriverInput()
        {
            Check(Native.IsInjected(1), "All SendInput events are excluded from physical state");
            Check(Native.IsInjected(3), "Lower-integrity injections are excluded");
            Check(Native.IsBl4ckTapInput(1, new UIntPtr(Native.Bl4ckTapMarker)), "Our pulse marker is excluded");
            Check(!Native.IsBl4ckTapInput(0, new UIntPtr(Native.Bl4ckTapMarker)), "Hardware input is never excluded by marker alone");
            foreach (int button in new[] { 3, 4, 5 })
            {
                ShortcutMouseInput input = new ShortcutMouseInput();
                ShortcutBinding binding = ShortcutBinding.Mouse(button);
                int down = button == 3 ? 0x0207 : 0x020B;
                int up = button == 3 ? 0x0208 : 0x020C;
                uint data = button == 3 ? 0 : (uint)(button - 3) << 16;
                Check(!input.Process(down, data, 1, binding, true).Toggle, "Injected shortcut is ignored " + button);
                Check(!input.Process(up, data, 1, binding, true).Toggle, "Driver-backed release doesn't toggle " + button);
                Check(input.Process(down, data, 0, binding, true).Toggle, "Physical press still toggles " + button);
            }
            PhysicalButton held = new PhysicalButton();
            held.Process(true, Native.IsInjected(0), true);
            Check(held.Held, "Physical down starts hold");
            held.Process(false, Native.IsInjected(1), false);
            Check(held.Held, "Our up pulse does not release the user's held button");
            held.Process(false, Native.IsInjected(0), false);
            Check(!held.Held, "Physical release ends hold immediately");
        }

        private static void StressPhysicalInput()
        {
            PhysicalButton pl = new PhysicalButton(), pr = new PhysicalButton();
            PulseButton l = new PulseButton(), r = new PulseButton();
            ShortcutMouseInput shortcuts = new ShortcutMouseInput();
            int ld = 0, lu = 0, rd = 0, ru = 0;
            Func<bool,bool> sl = delegate(bool down) { if (down) ld++; else lu++; pl.Process(down,Native.IsInjected(1),false); return true; };
            Func<bool,bool> sr = delegate(bool down) { if (down) rd++; else ru++; pr.Process(down,Native.IsInjected(3),false); return true; };
            for (int cycle = 0; cycle < 10000; cycle++)
            {
                double start = cycle * 300.0;
                pl.Process(true,false,false); pr.Process(true,false,false);
                for (int step = 0; step < 5; step++)
                {
                    double now = start + step * 50;
                    if (step == 2) pl.Process(false,false,false);
                    if (step == 4) pr.Process(false,false,false);
                    int before = ld;
                    l.Tick(now,pl.Held,50,sl); r.Tick(now,pr.Held,50,sr);
                    Check(step < 2 || ld == before,"Released left never repeats while right continues");
                    pl.Process(true,Native.IsInjected(1),false); pr.Process(true,Native.IsInjected(3),false);
                    Check(pl.Held == (step < 2) && pr.Held == (step < 4),"Injected pulses never alter physical state");
                }
                l.Release(sl); r.Release(sr); pl.Reset(); pr.Reset();
                Check(!pl.Held && !pr.Held && !l.IsDown && !r.IsDown,"Stop fully resets held state and output");
                foreach (int b in new[] { 3,4,5 })
                {
                    int down = b == 3 ? 0x207 : 0x20B, up = b == 3 ? 0x208 : 0x20C;
                    uint data = b == 3 ? 0U : (uint)(b-3)<<16;
                    ShortcutBinding binding = ShortcutBinding.Mouse(b);
                    Check(shortcuts.Process(down,data,0,binding,true).Toggle,"Shortcut remains responsive after pulses");
                    Check(!shortcuts.Process(down,data,0,binding,true).Toggle,"Held shortcut does not repeat");
                    Check(!shortcuts.Process(up,data,1,binding,true).Toggle,"Generated shortcut release is ignored");
                    Check(!shortcuts.Process(up,data,0,binding,true).Toggle,"Physical shortcut release does not toggle");
                }
            }
            Check(ld == 20000 && rd == 40000 && ld == lu && rd == ru,"60000 generated clicks remain balanced after 10000 hold cycles");
            Console.WriteLine("Stress: 60000 simulated clicks, 10000 hold cycles, 10000 presses per side/middle shortcut.");
        }

        private static void TestThreadIsolation()
        {
            int downs = 0, ups = 0;
            using (ManualResetEvent entered = new ManualResetEvent(false))
            using (ManualResetEvent release = new ManualResetEvent(false))
            {
                Func<bool,bool,bool> sink = delegate(bool left,bool down)
                {
                    if (down) { Interlocked.Increment(ref downs); entered.Set(); release.WaitOne(2000); }
                    else Interlocked.Increment(ref ups);
                    return true;
                };
                using (ClickEngine engine = new ClickEngine(new ClickOptions(true,true,true,30),ShortcutBinding.Mouse(4),sink,delegate { return true; }))
                {
                    try
                    {
                        AwaitStatus(engine,s=>s.Ready,"Input and output threads ready");
                        engine.Toggle(); AwaitStatus(engine,s=>s.Enabled,"Engine armed for simulated holds");
                        FeedMouseRecord(engine,0x201,0);
                        Check(entered.WaitOne(1500),"Generated down reached recording sink");
                        Stopwatch watch = Stopwatch.StartNew();
                        FeedMouseRecord(engine,0x202,0);
                        AwaitStatus(engine,s=>!s.LeftHeld,"Physical release is processed while output is blocked");
                        Check(watch.ElapsedMilliseconds < 250,"Input thread is independent from blocked output");
                        release.Set(); Thread.Sleep(100);
                        Check(downs == 1 && ups == 1,"No repeat after physical release during blocked send");
                        FeedMouseRecord(engine,0x201,0); FeedMouseRecord(engine,0x204,0);
                        AwaitStatus(engine,s=>s.LeftCount>=3 && s.RightCount>=2,"Both physical holds produce independent pulses");
                        engine.Stop(); AwaitStatus(engine,s=>!s.Enabled&&!s.LeftHeld&&!s.RightHeld,"Stop clears all physical click states");
                        Thread.Sleep(50); int stopped = downs; Thread.Sleep(100);
                        Check(downs == stopped && downs == ups,"Stop releases output and prevents any further clicks");
                        engine.Toggle(); AwaitStatus(engine,s=>s.Enabled,"Reactivation succeeds"); Thread.Sleep(70);
                        Check(downs == stopped,"Reactivation cannot revive stale holds");
                    }
                    finally { release.Set(); }
                }
                Check(downs == ups,"Disposal balances every generated down with an up");
            }
        }

        private static void AwaitStatus(ClickEngine engine, Func<EngineStatus, bool> condition, string description)
        {
            for (int i = 0; i < 100 && !condition(engine.Status); i++) Thread.Sleep(20);
            Check(condition(engine.Status), description + ": " + engine.Status.Error);
        }

        // Feed a synthetic hook record on the engine thread; never inject into Windows.
        private static void FeedMouseRecord(ClickEngine engine, int message, uint mouseData)
        {
            Action feed = delegate
            {
                Native.MouseHookData record = new Native.MouseHookData { MouseData = mouseData };
                IntPtr data = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.MouseHookData)));
                try
                {
                    Marshal.StructureToPtr(record, data, false);
                    typeof(ClickEngine).GetMethod("MouseHook", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(engine, new object[] { 0, new IntPtr(message), data });
                }
                finally { Marshal.FreeHGlobal(data); }
            };
            typeof(ClickEngine).GetMethod("Post", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(engine, new object[] { feed });
        }

        private static void Smoke()
        {
            using (ClickEngine engine = new ClickEngine(new ClickOptions(true, true, true, 12)))
            {
                for (int i = 0; i < 100 && !engine.Status.Ready && engine.Status.Error == null; i++) Thread.Sleep(20);
                Check(engine.Status.Ready, "Hook and hotkeys ready: " + engine.Status.Error);
                Check(!engine.Status.Enabled, "Starts disabled");
                engine.Configure(new ClickOptions(false, false, false, 30));
                engine.Toggle();
                Thread.Sleep(100);
                Check(!engine.Status.Enabled, "Cannot activate with neither button selected");
                Check(engine.Status.Shortcut.Id == "mouse:4", "Mouse 4 is the default");
                engine.ChangeShortcut(ShortcutBinding.Keyboard(Keys.F6));
                AwaitStatus(engine, s => s.ShortcutRevision == 1, "Keyboard rebinding completes");
                Check(engine.Status.Shortcut.Key == 0x75, "F6 binding applied");
                engine.BeginShortcutCapture();
                AwaitStatus(engine, s => s.CapturingShortcut, "Shortcut capture starts");
                bool captureFreedF6 = Native.RegisterHotKey(IntPtr.Zero, 103, 0x4000, 0x75);
                if (captureFreedF6) Native.UnregisterHotKey(IntPtr.Zero, 103);
                Check(captureFreedF6, "Old keyboard shortcut is released during capture");
                engine.CancelShortcutCapture();
                AwaitStatus(engine, s => s.ShortcutRevision == 2 && !s.CapturingShortcut, "Capture cancellation completes");
                Check(engine.Status.Shortcut.Key == 0x75, "Cancel preserves the previous binding");
                engine.BeginShortcutCapture();
                AwaitStatus(engine, s => s.CapturingShortcut, "Mouse capture starts");
                FeedMouseRecord(engine, 0x020B, 1U << 16);
                AwaitStatus(engine, s => s.ShortcutRevision == 3, "Mouse 4 is captured through native hook dispatch");
                Check(engine.Status.Shortcut.Id == "mouse:4" && !engine.Status.Enabled, "Capturing does not activate clicks");
                FeedMouseRecord(engine, 0x020C, 1U << 16);
                bool f6Available = Native.RegisterHotKey(IntPtr.Zero, 104, 0x4000, 0x75);
                Check(f6Available, "Mouse binding frees previous keyboard shortcut");
                try
                {
                    engine.ChangeShortcut(ShortcutBinding.Keyboard(Keys.F6));
                    AwaitStatus(engine, s => s.ShortcutRevision == 4, "Conflicting keyboard binding handled");
                    Check(engine.Status.Shortcut.Id == "mouse:4" && engine.Status.Error != null && engine.Status.Ready,
                        "Unavailable key preserves a working Mouse 4 binding");
                }
                finally { if (f6Available) Native.UnregisterHotKey(IntPtr.Zero, 104); }
                Check(engine.Status.LeftCount == 0 && engine.Status.RightCount == 0, "Smoke test sent no clicks");
            }
            bool f6 = Native.RegisterHotKey(IntPtr.Zero, 101, 0x4000, 0x75);
            bool f8 = Native.RegisterHotKey(IntPtr.Zero, 102, 0x4000, 0x77);
            if (f6) Native.UnregisterHotKey(IntPtr.Zero, 101);
            if (f8) Native.UnregisterHotKey(IntPtr.Zero, 102);
            Check(f6 && f8, "Shutdown frees both global hotkeys");
        }
    }
}
