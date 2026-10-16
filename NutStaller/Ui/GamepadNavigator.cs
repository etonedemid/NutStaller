using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NutStaller.Ui
{
    // Controller navigation for the launcher UI: XInput on Windows, SDL2
    // everywhere else (no SDL2 installed = no gamepad, nothing breaks).
    // DPad / left stick move focus spatially, A activates, B jumps back to the
    // nav column, LB/RB cycle pages, Start launches the game.
    internal sealed class GamepadNavigator : IDisposable
    {
        // button bits use the XInput layout; the SDL backend maps onto it
        private const ushort DPAD_UP = 0x0001, DPAD_DOWN = 0x0002, DPAD_LEFT = 0x0004, DPAD_RIGHT = 0x0008;
        private const ushort START = 0x0010, LB = 0x0100, RB = 0x0200, BTN_A = 0x1000, BTN_B = 0x2000;
        private const short StickThreshold = 16000;

        // left stick Y is positive = up, like XInput
        private record struct PadState(ushort Buttons, short LX, short LY);

        private interface IPadBackend
        {
            // false when no pad is connected (or the backend is unavailable)
            bool TryRead(out PadState state);
        }

        private readonly Window _window;
        private readonly Func<IEnumerable<Control>> _candidates;
        private readonly Action<Control> _activate;
        private readonly Action<int> _cyclePage;
        private readonly Action _start;
        private readonly Action _back;
        private readonly DispatcherTimer _timer;
        private readonly IPadBackend _pad;

        private ushort _prevButtons;
        private int _prevDir = -1;
        private DateTime _nextRepeat;

        public GamepadNavigator(Window window, Func<IEnumerable<Control>> candidates,
            Action<Control> activate, Action<int> cyclePage, Action start, Action back)
        {
            _window = window;
            _candidates = candidates;
            _activate = activate;
            _cyclePage = cyclePage;
            _start = start;
            _back = back;
            _pad = OperatingSystem.IsWindows() ? new XInputBackend() : new SdlBackend();
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Input, (s, e) => Poll());
            _timer.Start();
        }

        public void Dispose() => _timer.Stop();

        private void Poll()
        {
            if (!_window.IsActive || !_pad.TryRead(out var state)) { _prevButtons = 0; _prevDir = -1; return; }

            ushort buttons = state.Buttons;
            ushort pressed = (ushort)(buttons & ~_prevButtons);

            // merge dpad and left stick into one direction: 0=up 1=down 2=left 3=right
            int dir = -1;
            if ((buttons & DPAD_UP) != 0 || state.LY > StickThreshold) dir = 0;
            else if ((buttons & DPAD_DOWN) != 0 || state.LY < -StickThreshold) dir = 1;
            else if ((buttons & DPAD_LEFT) != 0 || state.LX < -StickThreshold) dir = 2;
            else if ((buttons & DPAD_RIGHT) != 0 || state.LX > StickThreshold) dir = 3;

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
            // walk up from the real focused element (maybe a TextBox inside a
            // value box) until we hit a navigation candidate
            for (var v = _window.FocusManager?.GetFocusedElement() as Visual; v != null; v = v.GetVisualParent())
                if (v is Control c && cands.Contains(c)) return c;
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
                double dx = to.X - from.X, dy = to.Y - from.Y;
                double primary, secondary;
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

        private Point Center(Control c) =>
            c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), _window) ?? default;

        // ------------------------------------------------------------- XInput

        private sealed class XInputBackend : IPadBackend
        {
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

            private bool _use910;
            private int _padIndex = -1;
            private DateTime _nextScan = DateTime.MinValue;

            private int GetState(int index, out XINPUT_STATE state)
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

            public bool TryRead(out PadState state)
            {
                state = default;
                if (_padIndex < 0)
                {
                    if (DateTime.UtcNow < _nextScan) return false;
                    _nextScan = DateTime.UtcNow.AddSeconds(2);
                    for (int i = 0; i < 4; i++)
                        if (GetState(i, out _) == 0) { _padIndex = i; break; }
                    if (_padIndex < 0) return false;
                }
                if (GetState(_padIndex, out var s) != 0) { _padIndex = -1; return false; }
                state = new PadState(s.Gamepad.wButtons, s.Gamepad.sThumbLX, s.Gamepad.sThumbLY);
                return true;
            }
        }

        // ------------------------------------------------------------- SDL2

        private sealed class SdlBackend : IPadBackend
        {
            private const string Lib = "libSDL2-2.0.so.0";
            private const uint SDL_INIT_GAMECONTROLLER = 0x2000;

            [DllImport(Lib)] private static extern int SDL_SetHint(string name, string value);
            [DllImport(Lib)] private static extern int SDL_InitSubSystem(uint flags);
            [DllImport(Lib)] private static extern int SDL_NumJoysticks();
            [DllImport(Lib)] private static extern int SDL_IsGameController(int index);
            [DllImport(Lib)] private static extern IntPtr SDL_GameControllerOpen(int index);
            [DllImport(Lib)] private static extern void SDL_GameControllerClose(IntPtr pad);
            [DllImport(Lib)] private static extern int SDL_GameControllerGetAttached(IntPtr pad);
            [DllImport(Lib)] private static extern void SDL_GameControllerUpdate();
            [DllImport(Lib)] private static extern byte SDL_GameControllerGetButton(IntPtr pad, int button);
            [DllImport(Lib)] private static extern short SDL_GameControllerGetAxis(IntPtr pad, int axis);

            // SDL_GameControllerButton index -> XInput bit
            private static readonly (int Sdl, ushort Bit)[] Map =
            {
                (0, BTN_A), (1, BTN_B), (6, START), (9, LB), (10, RB),
                (11, DPAD_UP), (12, DPAD_DOWN), (13, DPAD_LEFT), (14, DPAD_RIGHT),
            };

            private bool _ok;
            private IntPtr _pad;
            private DateTime _nextScan = DateTime.MinValue;

            public SdlBackend()
            {
                try
                {
                    // we only poll while our window is focused, but SDL itself must
                    // not drop input just because it owns no window
                    SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
                    _ok = SDL_InitSubSystem(SDL_INIT_GAMECONTROLLER) == 0;
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }

            public bool TryRead(out PadState state)
            {
                state = default;
                if (!_ok) return false;
                SDL_GameControllerUpdate(); // also picks up hotplugged pads

                if (_pad != IntPtr.Zero && SDL_GameControllerGetAttached(_pad) == 0)
                {
                    SDL_GameControllerClose(_pad);
                    _pad = IntPtr.Zero;
                }
                if (_pad == IntPtr.Zero)
                {
                    if (DateTime.UtcNow < _nextScan) return false;
                    _nextScan = DateTime.UtcNow.AddSeconds(2);
                    for (int i = 0; i < SDL_NumJoysticks() && _pad == IntPtr.Zero; i++)
                        if (SDL_IsGameController(i) != 0) _pad = SDL_GameControllerOpen(i);
                    if (_pad == IntPtr.Zero) return false;
                }

                ushort buttons = 0;
                foreach (var (sdl, bit) in Map)
                    if (SDL_GameControllerGetButton(_pad, sdl) != 0) buttons |= bit;
                short lx = SDL_GameControllerGetAxis(_pad, 0);
                short ly = SDL_GameControllerGetAxis(_pad, 1);
                // SDL's Y grows downward; flip it (clamped, -(-32768) overflows)
                state = new PadState(buttons, lx, (short)Math.Clamp(-ly, short.MinValue, short.MaxValue));
                return true;
            }
        }
    }
}
