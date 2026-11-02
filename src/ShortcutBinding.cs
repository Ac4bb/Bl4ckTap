using System;
using System.Windows.Forms;

namespace Bl4ckTapApp
{
    internal sealed class ShortcutBinding
    {
        public readonly int MouseButton;
        public readonly uint Key;
        public readonly string Name;
        public bool IsMouse { get { return MouseButton != 0; } }
        public string Id { get { return IsMouse ? "mouse:" + MouseButton : "key:" + Key; } }

        private ShortcutBinding(int mouseButton, uint key, string name)
        {
            MouseButton = mouseButton; Key = key; Name = name;
        }

        public static ShortcutBinding Mouse(int button)
        {
            if (button < 3 || button > 5) throw new ArgumentOutOfRangeException("button");
            return new ShortcutBinding(button, 0, button == 3 ? "Clic molette" : "Souris " + button);
        }

        public static ShortcutBinding Keyboard(Keys key)
        {
            if (!SupportsKey(key)) throw new ArgumentOutOfRangeException("key");
            return new ShortcutBinding(0, (uint)key, new KeysConverter().ConvertToString(key));
        }

        public static bool SupportsKey(Keys key)
        {
            return (int)key >= 8 && (int)key <= 254 && Enum.IsDefined(typeof(Keys), key)
                && key != Keys.F8 && key != Keys.F12 && key != Keys.Escape
                && key != Keys.ShiftKey && key != Keys.ControlKey && key != Keys.Menu
                && key != Keys.LShiftKey && key != Keys.RShiftKey && key != Keys.LControlKey
                && key != Keys.RControlKey && key != Keys.LMenu && key != Keys.RMenu
                && key != Keys.LWin && key != Keys.RWin;
        }

        public static ShortcutBinding Parse(string id)
        {
            if (id == "mouse:3") return Mouse(3);
            if (id == "mouse:5") return Mouse(5);
            uint key;
            if (id != null && id.StartsWith("key:") && UInt32.TryParse(id.Substring(4), out key)
                && key <= 254 && SupportsKey((Keys)key)) return Keyboard((Keys)key);
            return Mouse(4);
        }

        public override string ToString()
        {
            if (MouseButton == 4) return "Souris 4 · bouton latéral arrière";
            if (MouseButton == 5) return "Souris 5 · bouton latéral avant";
            return IsMouse ? Name : "Clavier · " + Name;
        }
    }

    internal struct ShortcutMouseResult
    {
        public bool Consumed, Toggle;
    }

    internal sealed class ShortcutMouseInput
    {
        public void Reset() { middle.Reset(); back.Reset(); forward.Reset(); }
        private readonly PhysicalButton middle = new PhysicalButton(), back = new PhysicalButton(), forward = new PhysicalButton();

        public ShortcutMouseResult Process(int message, uint mouseData, uint flags, ShortcutBinding binding, bool ready, UIntPtr extraInfo = default(UIntPtr))
        {
            ShortcutMouseResult result = new ShortcutMouseResult();
            int button;
            bool down;
            if (message == 0x0207 || message == 0x0208) { button = 3; down = message == 0x0207; }
            else if (message == 0x020B || message == 0x020C)
            {
                uint xbutton = (mouseData >> 16) & 0xFFFF;
                if (xbutton != 1 && xbutton != 2) return result;
                button = xbutton == 1 ? 4 : 5;
                down = message == 0x020B;
            }
            else return result;

            bool injected = Native.IsInjected(flags);
            PhysicalButton physical = button == 3 ? middle : button == 4 ? back : forward;
            bool wasHeld = physical.Held;
            bool matches = ready && binding.MouseButton == button;
            result.Consumed = physical.Process(down, injected, matches);
            result.Toggle = !injected && down && !wasHeld && matches;
            return result;
        }
    }
}
