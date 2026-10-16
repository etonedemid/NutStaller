using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NutStaller.Ui
{
    // Embedded resources (blueprint art + the Andy font), the shared palette and
    // the text drawing every Bk control goes through.
    internal static class Assets
    {
        // Blueprint palette, sampled from the menu art.
        public static readonly Color Blueprint = Color.FromRgb(45, 82, 168);
        public static readonly IBrush Ink = Brushes.White;
        public static readonly IBrush InkDim = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
        public static readonly IBrush Shadow = new SolidColorBrush(Color.FromArgb(120, 0, 0, 40));
        public static readonly IBrush Navy = new SolidColorBrush(Color.FromRgb(10, 20, 60));
        public static readonly IPen LineDim = new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)));

        public static readonly FontFamily AndyFamily = new("avares://NutStaller/Assets#Andy");
        private static readonly Typeface Andy = new(AndyFamily);

        private static Bitmap? _background;
        public static Bitmap Background =>
            _background ??= new Bitmap(AssetLoader.Open(new Uri("avares://NutStaller/Assets/bg.png")));

        // Andy text with the soft drop shadow, vertically centred in rc.
        // Single line with an ellipsis unless wrap is set.
        public static void DrawText(DrawingContext g, string text, double sizePx, IBrush ink, Rect rc,
            TextAlignment align = TextAlignment.Left, bool wrap = false,
            TextTrimming? trimming = null, bool shadow = true)
        {
            if (string.IsNullOrEmpty(text) || rc.Width <= 0 || rc.Height <= 0) return;

            FormattedText Make(IBrush brush)
            {
                var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    Andy, sizePx, brush)
                {
                    MaxTextWidth = rc.Width,
                    Trimming = trimming ?? TextTrimming.CharacterEllipsis,
                    TextAlignment = align,
                };
                if (wrap) ft.MaxTextHeight = rc.Height;
                else ft.MaxLineCount = 1;
                return ft;
            }

            var main = Make(ink);
            double y = rc.Y + (rc.Height - main.Height) / 2;
            if (shadow) g.DrawText(Make(Shadow), new Point(rc.X + 2, y + 2));
            g.DrawText(main, new Point(rc.X, y));
        }
    }
}
