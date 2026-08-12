using System.Runtime.InteropServices;

namespace NutStaller.Ui
{
    // XInput-based controller navigation for the launcher UI.
    // DPad / left stick move focus spatially, A activates, B jumps back to the
    // nav column, LB/RB cycle pages, Start launches the game.
    internal sealed class GamepadNavigator : IDisposable
    {
        private const ushort DPAD_UP = 0x0001, DPAD_DOWN = 0x0002, DPAD_LEFT = 0x0004, DPAD_RIGHT = 0x0008;
        private const ushort START = 0x0010, LB = 0x0100, RB = 0x0200, BTN_A = 0x1000, BTN_B = 0x2000;
        private const short StickThreshold = 16000;

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger, bRightTrigger;
            public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState14(int index, out XINPUT_STATE state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState910(int index, out XINPUT_STATE state);

        private static bool _use910;

        private static int GetState(int index, out XINPUT_STATE state)
        {
            try
            {
                return _use910 ? XInputGetState910(index, out state) : XInputGetState14(index, out state);
            }
            catch (DllNotFoundException)
            {
                if (_use910) { state = default; return -1; }
                _use910 = true;
                return GetState(index, out state);
            }
        }

        private readonly Form _form;
        private readonly Func<IEnumerable<Control>> _candidates;
        private readonly Action<Control> _activate;
        private readonly Action<int> _cyclePage;
        private readonly Action _start;
        private readonly Action _back;
        private readonly System.Windows.Forms.Timer _timer;

        private int _padIndex = -1;
        private DateTime _nextScan = DateTime.MinValue;
        private ushort _prevButtons;
        private int _prevDir = -1;
        private DateTime _nextRepeat;

        public GamepadNavigator(Form form, Func<IEnumerable<Control>> candidates,
            Action<Control> activate, Action<int> cyclePage, Action start, Action back)
        {
            _form = form;
            _candidates = candidates;
            _activate = activate;
            _cyclePage = cyclePage;
            _start = start;
            _back = back;
            _timer = new System.Windows.Forms.Timer { Interval = 40 };
            _timer.Tick += (s, e) => Poll();
            _timer.Start();
        }

        public void Dispose() => _timer.Dispose();

        private void Poll()
        {
            if (!_form.ContainsFocus) { _prevButtons = 0; _prevDir = -1; return; }

            if (_padIndex < 0)
            {
                if (DateTime.UtcNow < _nextScan) return;
                _nextScan = DateTime.UtcNow.AddSeconds(2);
                for (int i = 0; i < 4; i++)
                    if (GetState(i, out _) == 0) { _padIndex = i; break; }
                if (_padIndex < 0) return;
            }

            if (GetState(_padIndex, out var state) != 0)
            {
                _padIndex = -1;
                _prevButtons = 0;
                _prevDir = -1;
                return;
            }

            ushort buttons = state.Gamepad.wButtons;
            ushort pressed = (ushort)(buttons & ~_prevButtons);

            // merge dpad and left stick into one direction: 0=up 1=down 2=left 3=right
            int dir = -1;
            if ((buttons & DPAD_UP) != 0 || state.Gamepad.sThumbLY > StickThreshold) dir = 0;
            else if ((buttons & DPAD_DOWN) != 0 || state.Gamepad.sThumbLY < -StickThreshold) dir = 1;
            else if ((buttons & DPAD_LEFT) != 0 || state.Gamepad.sThumbLX < -StickThreshold) dir = 2;
            else if ((buttons & DPAD_RIGHT) != 0 || state.Gamepad.sThumbLX > StickThreshold) dir = 3;

            if (dir != -1 && (dir != _prevDir || DateTime.UtcNow >= _nextRepeat))
            {
                _nextRepeat = DateTime.UtcNow.AddMilliseconds(dir != _prevDir ? 380 : 140);
                Move(dir);
            }
            _prevDir = dir;

            if ((pressed & BTN_A) != 0)
            {
                var focused = FocusedCandidate();
                if (focused != null) _activate(focused);
                else FocusFirst();
            }
            if ((pressed & BTN_B) != 0) _back();
            if ((pressed & LB) != 0) _cyclePage(-1);
            if ((pressed & RB) != 0) _cyclePage(1);
            if ((pressed & START) != 0) _start();

            _prevButtons = buttons;
        }

        private Control? FocusedCandidate()
        {
            var cands = new HashSet<Control>(_candidates());
            Control? active = _form.ActiveControl;
            // drill down through nested containers to the real focused control
            while (active is ContainerControl cc && cc.ActiveControl != null)
                active = cc.ActiveControl;
            // then walk back up until we hit a navigation candidate
            while (active != null && active != _form)
            {
                if (cands.Contains(active)) return active;
                active = active.Parent;
            }
            return null;
        }

        private void FocusFirst()
        {
            foreach (var c in _candidates()) { c.Focus(); return; }
        }

        private void Move(int dir)
        {
            var cur = FocusedCandidate();
            if (cur == null) { FocusFirst(); return; }

            Point from = Center(cur);
            Control? best = null;
            double bestScore = double.MaxValue;

            foreach (var c in _candidates())
            {
                if (c == cur) continue;
                Point to = Center(c);
                int dx = to.X - from.X, dy = to.Y - from.Y;
                int primary, secondary;
                switch (dir)
                {
                    case 0: primary = -dy; secondary = Math.Abs(dx); break;
                    case 1: primary = dy; secondary = Math.Abs(dx); break;
                    case 2: primary = -dx; secondary = Math.Abs(dy); break;
                    default: primary = dx; secondary = Math.Abs(dy); break;
                }
                if (primary <= 0) continue;
                double score = primary + secondary * 2.5;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            best?.Focus();
        }

        private static Point Center(Control c)
        {
            var screen = c.Parent!.RectangleToScreen(c.Bounds);
            return new Point(screen.X + screen.Width / 2, screen.Y + screen.Height / 2);
        }
    }
}
