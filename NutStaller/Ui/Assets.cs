using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NutStaller.Ui
{
    // Embedded resources (blueprint art + the Andy font) and the shared palette.
    // Loads lazily so controls also work inside the VS designer.
    internal static class Assets
    {
        // Blueprint palette, sampled from the menu art.
        public static readonly Color Blueprint = Color.FromArgb(45, 82, 168);
        public static readonly Color Ink = Color.White;
        public static readonly Color InkDim = Color.FromArgb(200, 255, 255, 255);
        public static readonly Color LineDim = Color.FromArgb(110, 255, 255, 255);

        private static Bitmap? _background;
        private static Bitmap? _toggleBox;
        private static PrivateFontCollection? _fonts;
        private static IntPtr _fontMem = IntPtr.Zero;
        private static readonly Dictionary<int, Font> _fontCache = new();

        public static Bitmap? Background => _background ??= TryLoadBitmap("bg.png");
        public static Bitmap? ToggleBox => _toggleBox ??= TryLoadBitmap("toggle.png");

        public static Font Andy(float sizePx, FontStyle style = FontStyle.Bold)
        {
            int key = (int)Math.Round(sizePx * 4);
            if (_fontCache.TryGetValue(key, out var cached)) return cached;

            Font f;
            try
            {
                _fonts ??= LoadFontCollection();
                f = new Font(_fonts.Families[0], sizePx, style, GraphicsUnit.Pixel);
            }
            catch
            {
                // design-time / missing-resource fallback
                f = new Font("Segoe UI", Math.Max(sizePx, 6), style, GraphicsUnit.Pixel);
            }
            _fontCache[key] = f;
            return f;
        }

        private static Stream? TryOpenResource(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            string? full = Array.Find(asm.GetManifestResourceNames(),
                n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            return full == null ? null : asm.GetManifestResourceStream(full);
        }

        private static Bitmap? TryLoadBitmap(string name)
        {
            try
            {
                using var s = TryOpenResource(name);
                return s == null ? null : new Bitmap(s);
            }
            catch { return null; }
        }

        private static PrivateFontCollection LoadFontCollection()
        {
            using var s = TryOpenResource("ANDYB.TTF")
                ?? throw new InvalidOperationException("ANDYB.TTF resource missing");
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            byte[] data = ms.ToArray();

            var fonts = new PrivateFontCollection();
            _fontMem = Marshal.AllocCoTaskMem(data.Length);
            Marshal.Copy(data, 0, _fontMem, data.Length);
            fonts.AddMemoryFont(_fontMem, data.Length);
            return fonts;
        }
    }
}
