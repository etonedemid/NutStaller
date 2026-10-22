using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering;

namespace NutStaller.Ui
{
    // Shared base: Andy text at a design-pixel size, custom painted, and hit
    // testable over its whole rectangle even where nothing is drawn.
    public abstract class BkControl : Control, ICustomHitTest
    {
        private string _text = "";
        private double _fontSizePx = 18;

        public string Text { get => _text; set { _text = value; InvalidateVisual(); } }
        public double FontSizePx { get => _fontSizePx; set { _fontSizePx = value; InvalidateVisual(); } }

        protected BkControl() => FocusAdorner = null; // BkFocusRing draws our own

        public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

        protected override void OnGotFocus(GotFocusEventArgs e) { InvalidateVisual(); base.OnGotFocus(e); }
        protected override void OnLostFocus(RoutedEventArgs e) { InvalidateVisual(); base.OnLostFocus(e); }
    }

    // Menu-entry style button: dark on the left, fading out to the right,
    // white Andy text. Darkens when hovered, near-solid when selected/pressed.
    public class BkButton : BkControl
    {
        private bool _hover, _down, _selected;

        public event EventHandler? Click;

        public bool Selected { get => _selected; set { _selected = value; InvalidateVisual(); } }

        public BkButton()
        {
            FontSizePx = 22;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        // lets gamepad navigation press the button
        public void PerformClick() => Click?.Invoke(this, EventArgs.Empty);

        protected override void OnPointerEntered(PointerEventArgs e) { _hover = true; InvalidateVisual(); base.OnPointerEntered(e); }
        protected override void OnPointerExited(PointerEventArgs e) { _hover = false; _down = false; InvalidateVisual(); base.OnPointerExited(e); }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            _down = true;
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            bool wasDown = _down;
            _down = false;
            InvalidateVisual();
            if (wasDown && HitTest(e.GetPosition(this))) PerformClick();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Space) { PerformClick(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        public override void Render(DrawingContext g)
        {
            double w = Bounds.Width, h = Bounds.Height;

            // gradient slab: blacker on the left, fading out to the right
            bool hot = _hover || IsFocused;
            byte leftAlpha = (byte)(_selected || _down ? 250 : hot ? 225 : 185);
            byte rightAlpha = (byte)(_selected || _down ? 90 : hot ? 45 : 15);
            var slab = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(leftAlpha, 4, 10, 34), 0),
                    new GradientStop(Color.FromArgb(rightAlpha, 4, 10, 34), 1),
                },
            };
            g.FillRectangle(slab, new Rect(0, 0, w, h));

            // faint sketch line along the bottom, like the mock
            g.DrawLine(Assets.LineDim, new Point(0, h - 0.5), new Point(w, h - 0.5));

            Assets.DrawText(g, Text.ToUpperInvariant(), FontSizePx, Assets.Ink, new Rect(10, 1, w - 14, h - 2));
            BkFocusRing.Draw(g, this);
        }
    }

    // Dashed white outline drawn on whichever control the gamepad focus is on.
    internal static class BkFocusRing
    {
        private static readonly IPen Pen = new Pen(Brushes.White, 2, DashStyle.Dash);

        public static void Draw(DrawingContext g, Control c)
        {
            if (!c.IsFocused) return;
            g.DrawRectangle(null, Pen, new Rect(1, 1, c.Bounds.Width - 2, c.Bounds.Height - 2));
        }
    }

    // Navy slab holding an editable value (numbers / short strings).
    // With Transparent = true it paints nothing but the text; clicking it
    // brings up the edit field, which hides again when focus leaves.
    public class BkValueBox : Decorator, ICustomHitTest
    {
        private readonly TextBox _tb;
        private bool _transparent;
        private bool _editing;

        public event EventHandler? ValueChanged;

        public string Value { get => _tb.Text ?? ""; set => _tb.Text = value; }

        public bool Transparent
        {
            get => _transparent;
            set { _transparent = value; UpdateTextBoxVisibility(); InvalidateVisual(); }
        }

        public double FontSizePx { get => _tb.FontSize; set => _tb.FontSize = value; }

        public BkValueBox()
        {
            Focusable = true;
            FocusAdorner = null;
            _tb = new TextBox
            {
                FontFamily = Assets.AndyFamily,
                FontSize = 18,
                Foreground = Assets.Ink,
                Background = Assets.Navy,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(4, 2),
                Padding = new Thickness(2, 0),
                MinHeight = 0,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            // keep Fluent from flipping the field to its light focus/hover look
            foreach (var key in new[] { "TextControlBackgroundFocused", "TextControlBackgroundPointerOver" })
                _tb.Resources[key] = Assets.Navy;
            foreach (var key in new[] { "TextControlForegroundFocused", "TextControlForegroundPointerOver" })
                _tb.Resources[key] = Assets.Ink;
            _tb.TextChanged += (s, e) => { ValueChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual(); };
            _tb.LostFocus += (s, e) =>
            {
                if (_transparent) { _editing = false; UpdateTextBoxVisibility(); InvalidateVisual(); }
            };
            Child = _tb;
            UpdateTextBoxVisibility();
        }

        public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

        private void UpdateTextBoxVisibility() => _tb.IsVisible = !_transparent || _editing;

        protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            BeginEdit();
            e.Handled = true;
        }

        // opens the edit field; also used by gamepad navigation (A button)
        public void BeginEdit()
        {
            if (_transparent && !_editing)
            {
                _editing = true;
                UpdateTextBoxVisibility();
                InvalidateVisual();
            }
            _tb.Focus();
            _tb.SelectAll();
        }

        protected override void OnGotFocus(GotFocusEventArgs e) { InvalidateVisual(); base.OnGotFocus(e); }
        protected override void OnLostFocus(RoutedEventArgs e) { InvalidateVisual(); base.OnLostFocus(e); }

        public override void Render(DrawingContext g)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (_transparent && !_editing)
            {
                // fully transparent: just the text over whatever is behind us
                Assets.DrawText(g, Value, FontSizePx, Assets.Ink, new Rect(6, 0, w - 12, h),
                    trimming: TextTrimming.PrefixCharacterEllipsis);
                BkFocusRing.Draw(g, this);
                return;
            }
            g.FillRectangle(Assets.Navy, new Rect(0, 0, w, h));
            g.DrawRectangle(null, Assets.LineDim, new Rect(0.5, 0.5, w - 1, h - 1));
            BkFocusRing.Draw(g, this);
        }
    }

    // Click, then press a key to bind it.
    public class BkKeyField : BkControl
    {
        private static readonly IBrush Armed = new SolidColorBrush(Color.FromRgb(30, 55, 130));
        private static readonly IPen ArmedLine = new Pen(Brushes.White);

        public event EventHandler? KeyPicked;
        private bool _listening;

        public string KeyName { get => Text; set => Text = value; }

        public BkKeyField()
        {
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        // The first click arms the field; a click while armed binds that mouse
        // button (reNut's LMB / RMB / MMB).
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            e.Handled = true;
            if (!_listening) { BeginListen(); return; }
            string name = e.GetCurrentPoint(this).Properties.PointerUpdateKind switch
            {
                PointerUpdateKind.LeftButtonPressed => "LMB",
                PointerUpdateKind.RightButtonPressed => "RMB",
                PointerUpdateKind.MiddleButtonPressed => "MMB",
                _ => "",
            };
            if (name.Length > 0) Pick(name);
        }

        private void Pick(string name)
        {
            KeyName = name;
            _listening = false;
            InvalidateVisual();
            KeyPicked?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnLostFocus(RoutedEventArgs e) { _listening = false; base.OnLostFocus(e); }

        // arms the field for the next keyboard press; also used by gamepad navigation
        public void BeginListen() { _listening = true; Focus(); InvalidateVisual(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_listening)
            {
                string name = KeyToName(e.Key);
                if (name.Length > 0) Pick(name);
                e.Handled = true; // swallows Tab too, so it can be bound
            }
            base.OnKeyDown(e);
        }

        // The names reNut's binds take (the SDK's rex::ui::ParseVirtualKey).
        private static string KeyToName(Key k)
        {
            if (k >= Key.F1 && k <= Key.F24) return k.ToString();
            if (k >= Key.A && k <= Key.Z) return k.ToString();
            if (k >= Key.D0 && k <= Key.D9) return k.ToString().Substring(1);
            if (k >= Key.NumPad0 && k <= Key.NumPad9) return "Numpad" + (k - Key.NumPad0);
            return k switch
            {
                Key.Escape => "Escape", Key.Enter => "Return", Key.Space => "Space", Key.Tab => "Tab",
                Key.Back => "Backspace", Key.Delete => "Delete", Key.Insert => "Insert",
                Key.Home => "Home", Key.End => "End", Key.PageUp => "PageUp", Key.PageDown => "PageDown",
                Key.Left => "Left", Key.Right => "Right", Key.Up => "Up", Key.Down => "Down",
                Key.LeftShift or Key.RightShift => "Shift",
                Key.LeftCtrl or Key.RightCtrl => "Control",
                Key.LeftAlt or Key.RightAlt => "Alt",
                Key.Add => "NumpadPlus", Key.Subtract => "NumpadMinus",
                Key.Multiply => "NumpadStar", Key.Divide => "NumpadSlash",
                Key.OemTilde => "Backtick", Key.OemMinus => "Minus", Key.OemPlus => "Plus",
                Key.OemComma => "Comma", Key.OemPeriod => "Period", Key.OemSemicolon => "Semicolon",
                Key.OemQuestion => "Slash", Key.OemPipe or Key.OemBackslash => "Backslash",
                Key.OemOpenBrackets => "LBracket", Key.OemCloseBrackets => "RBracket", Key.OemQuotes => "Quote",
                Key.CapsLock => "CapsLock", Key.NumLock => "NumLock", Key.Scroll => "ScrollLock",
                Key.Pause => "Pause", Key.PrintScreen => "PrintScreen",
                _ => "",
            };
        }

        public override void Render(DrawingContext g)
        {
            double w = Bounds.Width, h = Bounds.Height;
            g.FillRectangle(_listening ? Armed : Assets.Navy, new Rect(0, 0, w, h));
            g.DrawRectangle(null, _listening ? ArmedLine : Assets.LineDim, new Rect(0.5, 0.5, w - 1, h - 1));

            string txt = _listening ? "KEY OR CLICK..." : KeyName.ToUpperInvariant();
            Assets.DrawText(g, txt, FontSizePx, Assets.Ink, new Rect(0, 0, w, h), TextAlignment.Center, shadow: false);
            if (!_listening) BkFocusRing.Draw(g, this);
        }
    }

    // Thin blueprint progress bar.
    public class BkProgress : Control
    {
        private double _value;

        public double Value { get => _value; set { _value = Math.Clamp(value, 0, 1); InvalidateVisual(); } }

        public override void Render(DrawingContext g)
        {
            double w = Bounds.Width, h = Bounds.Height;
            g.FillRectangle(Assets.Navy, new Rect(0, 0, w, h));
            g.FillRectangle(Brushes.White, new Rect(2, 2, Math.Max(0, (w - 4) * _value), Math.Max(0, h - 4)));
            g.DrawRectangle(null, Assets.LineDim, new Rect(0.5, 0.5, w - 1, h - 1));
        }
    }

    // Plain white Andy-font label with a soft shadow.
    public class BkLabel : BkControl
    {
        public bool Dim { get; set; }
        public TextAlignment Align { get; set; } = TextAlignment.Left;

        public BkLabel() => IsHitTestVisible = false;

        public void SetText(string t) => Text = t;

        public override void Render(DrawingContext g) =>
            Assets.DrawText(g, Text, FontSizePx, Dim ? Assets.InkDim : Assets.Ink,
                new Rect(Bounds.Size), Align, wrap: true);
    }

    // Translucent navy slab with a dashed blueprint border; pages sit on it so
    // text stays readable over the busy parts of the background art.
    // (Panel.Render is sealed, so the border is a child shape instead)
    public class BkPagePanel : Canvas
    {
        private readonly Avalonia.Controls.Shapes.Rectangle _border = new()
        {
            Stroke = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)),
            StrokeThickness = 1,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 3 },
            IsHitTestVisible = false,
        };

        public BkPagePanel()
        {
            Background = new SolidColorBrush(Color.FromArgb(170, 14, 28, 80));
            SetLeft(_border, 1.5);
            SetTop(_border, 1.5);
            Children.Add(_border);
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            _border.Width = Math.Max(0, e.NewSize.Width - 3);
            _border.Height = Math.Max(0, e.NewSize.Height - 3);
        }
    }
}
