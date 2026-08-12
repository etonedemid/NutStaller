using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace NutStaller.Ui
{
    // Controls that expose their Andy-font pixel size so the form can rescale them.
    public interface IBkFontSize
    {
        int FontSizePx { get; }
    }

    // Menu-entry style button: dark on the left, fading out to the right,
    // white Andy text. Darkens when hovered, near-solid when selected/pressed.
    public class BkButton : Control, IBkFontSize
    {
        private bool _hover, _down;
        private bool _selected;
        private int _fontSizePx = 22;

        [DefaultValue(false)]
        public bool Selected { get => _selected; set { _selected = value; Invalidate(); } }

        [DefaultValue(22)]
        public int FontSizePx { get => _fontSizePx; set { _fontSizePx = value; Font = Assets.Andy(value); Invalidate(); } }

        // The Andy font is embedded, not installed; never let the designer
        // serialize Font or it falls back to a system font. Use FontSizePx.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Font Font { get => base.Font; set => base.Font = value; }

        public BkButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Assets.Andy(_fontSizePx);
            Size = new Size(185, 32);
        }

        // lets gamepad navigation press the button
        public void PerformClick() => OnClick(EventArgs.Empty);

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // gradient slab: blacker on the left, fading out to the right
            bool hot = _hover || Focused;
            int leftAlpha = _selected || _down ? 250 : hot ? 225 : 185;
            int rightAlpha = _selected || _down ? 90 : hot ? 45 : 15;
            var dark = Color.FromArgb(leftAlpha, 4, 10, 34);
            var fade = Color.FromArgb(rightAlpha, 4, 10, 34);
            var rect = new Rectangle(0, 0, Math.Max(Width, 1), Math.Max(Height, 1));
            using (var lg = new LinearGradientBrush(rect, dark, fade, LinearGradientMode.Horizontal))
                g.FillRectangle(lg, rect);

            // faint sketch line along the bottom, like the mock
            using (var p = new Pen(Assets.LineDim))
                g.DrawLine(p, 0, Height - 1, Width, Height - 1);

            var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var rc = new RectangleF(10, 1, Width - 14, Height - 2);
            string text = Text.ToUpperInvariant();
            using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 40)))
                g.DrawString(text, Font, shadow, new RectangleF(rc.X + 2, rc.Y + 2, rc.Width, rc.Height), fmt);
            using (var ink = new SolidBrush(Assets.Ink))
                g.DrawString(text, Font, ink, rc, fmt);

            BkFocusRing.Draw(g, this);
        }
    }

    // Dashed white outline drawn on whichever control the gamepad focus is on.
    internal static class BkFocusRing
    {
        public static void Draw(Graphics g, Control c)
        {
            if (!c.Focused) return;
            using var p = new Pen(Color.White, 2f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(p, 1, 1, c.Width - 3, c.Height - 3);
        }
    }

    // Checkbox drawn with the toggle.png sprite and a hand-drawn style check mark.
    public class BkToggle : Control, IBkFontSize
    {
        public event EventHandler? CheckedChanged;
        private bool _checked;
        private int _fontSizePx = 19;

        [DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        }

        [DefaultValue(19)]
        public int FontSizePx { get => _fontSizePx; set { _fontSizePx = value; Font = Assets.Andy(value); Invalidate(); } }

        // The Andy font is embedded, not installed; never let the designer
        // serialize Font or it falls back to a system font. Use FontSizePx.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Font Font { get => base.Font; set => base.Font = value; }

        public BkToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Assets.Andy(_fontSizePx);
            Size = new Size(270, 30);
        }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            int box = Height - 6;
            var boxRect = new Rectangle(0, 3, box, box);
            var sprite = Assets.ToggleBox;
            if (sprite != null)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(sprite, boxRect);
            }
            else
            {
                using var b = new SolidBrush(Color.Gainsboro);
                g.FillRectangle(b, boxRect);
            }

            if (_checked)
            {
                using var p = new Pen(Color.White, Math.Max(2f, box * 0.13f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                float x = boxRect.X, y = boxRect.Y, s = box;
                g.DrawLines(p, new[]
                {
                    new PointF(x + s * 0.22f, y + s * 0.52f),
                    new PointF(x + s * 0.44f, y + s * 0.75f),
                    new PointF(x + s * 0.85f, y + s * 0.18f),
                });
            }

            var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var rc = new RectangleF(box + 10, 0, Width - box - 12, Height);
            string text = Text.ToUpperInvariant();
            using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 40)))
                g.DrawString(text, Font, shadow, new RectangleF(rc.X + 2, rc.Y + 2, rc.Width, rc.Height), fmt);
            using (var ink = new SolidBrush(Assets.Ink))
                g.DrawString(text, Font, ink, rc, fmt);

            BkFocusRing.Draw(g, this);
        }
    }

    // Navy slab holding an editable value (numbers / short strings).
    // With Transparent = true it paints nothing but the text; clicking it
    // brings up the edit field, which hides again when focus leaves.
    public class BkValueBox : Control, IBkFontSize
    {
        private TextBox? _tb;
        private int _fontSizePx = 18;
        private bool _transparent;
        private bool _editing;

        public event EventHandler? ValueChanged;

        [DefaultValue("")]
        public string Value
        {
            get => _tb?.Text ?? "";
            set { EnsureTextBox(); _tb!.Text = value; }
        }

        [DefaultValue(false)]
        public bool Transparent
        {
            get => _transparent;
            set { _transparent = value; UpdateTextBoxVisibility(); Invalidate(); }
        }

        [DefaultValue(18)]
        public int FontSizePx
        {
            get => _fontSizePx;
            set { _fontSizePx = value; Font = Assets.Andy(value); }
        }

        // The Andy font is embedded, not installed; never let the designer
        // serialize Font or it falls back to a system font. Use FontSizePx.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Font Font { get => base.Font; set => base.Font = value; }

        public BkValueBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Assets.Andy(_fontSizePx);
            Size = new Size(120, 30);
            EnsureTextBox();
        }

        private void EnsureTextBox()
        {
            if (_tb != null) return;
            _tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(10, 20, 60),
                ForeColor = Color.White,
                Font = Font,
            };
            _tb.TextChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);
            _tb.LostFocus += (s, e) =>
            {
                if (_transparent) { _editing = false; UpdateTextBoxVisibility(); Invalidate(); }
            };
            Controls.Add(_tb);
            UpdateTextBoxVisibility();
            LayoutTextBox();
        }

        private void UpdateTextBoxVisibility()
        {
            if (_tb != null) _tb.Visible = !_transparent || _editing;
        }

        protected override void OnClick(EventArgs e)
        {
            BeginEdit();
            base.OnClick(e);
        }

        // opens the edit field; also used by gamepad navigation (A button)
        public void BeginEdit()
        {
            EnsureTextBox();
            if (_transparent && !_editing)
            {
                _editing = true;
                UpdateTextBoxVisibility();
                Invalidate();
            }
            _tb!.Focus();
            _tb.SelectAll();
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        private void LayoutTextBox()
        {
            if (_tb == null) return;
            _tb.SetBounds(6, Math.Max(0, (Height - _tb.Height) / 2), Math.Max(10, Width - 12), _tb.Height);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_tb != null) { _tb.Font = Font; LayoutTextBox(); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutTextBox();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (_transparent && !_editing)
            {
                // fully transparent: just the text over whatever is behind us
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                var fmt = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisPath,
                    FormatFlags = StringFormatFlags.NoWrap,
                };
                string text = Value;
                using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 40)))
                    g.DrawString(text, Font, shadow, new RectangleF(8, 2, Width - 12, Height), fmt);
                using (var ink = new SolidBrush(Assets.Ink))
                    g.DrawString(text, Font, ink, new RectangleF(6, 0, Width - 12, Height), fmt);
                BkFocusRing.Draw(g, this);
                return;
            }
            using (var b = new SolidBrush(Color.FromArgb(10, 20, 60)))
                g.FillRectangle(b, 0, 0, Width, Height);
            using (var p = new Pen(Assets.LineDim))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            BkFocusRing.Draw(g, this);
        }
    }

    // Click, then press a key to bind it.
    public class BkKeyField : Control, IBkFontSize
    {
        public event EventHandler? KeyPicked;
        private bool _listening;
        private string _keyName = "";
        private int _fontSizePx = 18;

        [DefaultValue("")]
        public string KeyName { get => _keyName; set { _keyName = value; Invalidate(); } }

        [DefaultValue(18)]
        public int FontSizePx { get => _fontSizePx; set { _fontSizePx = value; Font = Assets.Andy(value); Invalidate(); } }

        // The Andy font is embedded, not installed; never let the designer
        // serialize Font or it falls back to a system font. Use FontSizePx.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Font Font { get => base.Font; set => base.Font = value; }

        public BkKeyField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Assets.Andy(_fontSizePx);
            Size = new Size(140, 30);
            TabStop = true;
        }

        protected override void OnClick(EventArgs e) { BeginListen(); base.OnClick(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { _listening = false; Invalidate(); base.OnLostFocus(e); }

        // arms the field for the next keyboard press; also used by gamepad navigation
        public void BeginListen() { _listening = true; Focus(); Invalidate(); }

        protected override bool IsInputKey(Keys keyData) => true;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_listening)
            {
                string name = KeyToName(e.KeyCode);
                if (name.Length > 0)
                {
                    _keyName = name;
                    _listening = false;
                    Invalidate();
                    KeyPicked?.Invoke(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private static string KeyToName(Keys k)
        {
            if (k >= Keys.F1 && k <= Keys.F12) return k.ToString();
            if (k >= Keys.A && k <= Keys.Z) return k.ToString();
            if (k >= Keys.D0 && k <= Keys.D9) return k.ToString().Substring(1);
            return k switch
            {
                Keys.Tab => "Tab", Keys.Space => "Space", Keys.Enter => "Enter",
                Keys.Home => "Home", Keys.End => "End", Keys.Insert => "Insert",
                Keys.Delete => "Delete", Keys.PageUp => "PageUp", Keys.PageDown => "PageDown",
                _ => "",
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (var b = new SolidBrush(_listening ? Color.FromArgb(30, 55, 130) : Color.FromArgb(10, 20, 60)))
                g.FillRectangle(b, 0, 0, Width, Height);
            using (var p = new Pen(_listening ? Color.White : Assets.LineDim))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

            string txt = _listening ? "PRESS A KEY..." : _keyName.ToUpperInvariant();
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var ink = new SolidBrush(Assets.Ink);
            g.DrawString(txt, Font, ink, new RectangleF(0, 0, Width, Height), fmt);
            if (!_listening) BkFocusRing.Draw(g, this);
        }
    }

    // Thin blueprint progress bar.
    public class BkProgress : Control
    {
        private double _value;

        [DefaultValue(0d)]
        public double Value { get => _value; set { _value = Math.Clamp(value, 0, 1); Invalidate(); } }

        public BkProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(536, 14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Color.FromArgb(10, 20, 60)))
                g.FillRectangle(b, 0, 0, Width, Height);
            using (var b = new SolidBrush(Color.White))
                g.FillRectangle(b, 2, 2, (int)((Width - 4) * _value), Height - 4);
            using (var p = new Pen(Assets.LineDim))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // Plain white Andy-font label with a soft shadow.
    public class BkLabel : Control, IBkFontSize
    {
        private int _fontSizePx = 18;

        [DefaultValue(ContentAlignment.MiddleLeft)]
        public ContentAlignment Align { get; set; } = ContentAlignment.MiddleLeft;

        [DefaultValue(false)]
        public bool Dim { get; set; }

        [DefaultValue(18)]
        public int FontSizePx { get => _fontSizePx; set { _fontSizePx = value; Font = Assets.Andy(value); Invalidate(); } }

        // The Andy font is embedded, not installed; never let the designer
        // serialize Font or it falls back to a system font. Use FontSizePx.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Font Font { get => base.Font; set => base.Font = value; }

        public BkLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Assets.Andy(_fontSizePx);
            Size = new Size(200, 30);
        }

        public void SetText(string t) { Text = t; Invalidate(); }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            var fmt = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = Align == ContentAlignment.MiddleCenter ? StringAlignment.Center : StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter,
            };
            var rc = new RectangleF(0, 0, Width, Height);
            using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 40)))
                g.DrawString(Text, Font, shadow, new RectangleF(2, 2, Width, Height), fmt);
            using (var ink = new SolidBrush(Dim ? Assets.InkDim : Assets.Ink))
                g.DrawString(Text, Font, ink, rc, fmt);
        }
    }

    // Translucent navy slab with a dashed blueprint border; pages sit on it so
    // text stays readable over the busy parts of the background art.
    public class BkPagePanel : Panel
    {
        public BkPagePanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Color.FromArgb(170, 14, 28, 80)))
                g.FillRectangle(b, 0, 0, Width, Height);
            using (var p = new Pen(Assets.LineDim) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
            base.OnPaint(e);
        }
    }
}
