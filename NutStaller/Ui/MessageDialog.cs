using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace NutStaller.Ui
{
    // Avalonia has no MessageBox. Text is selectable so errors can be copied.
    internal static class MessageDialog
    {
        // The text typed, or null when cancelled.
        public static async Task<string?> Ask(Window owner, string text, string title, string initial = "")
        {
            var dlg = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                MaxWidth = 640,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            var box = new TextBox { Text = initial, MinWidth = 360 };
            var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
            var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
            ok.Click += (s, e) => dlg.Close(true);
            cancel.Click += (s, e) => dlg.Close(false);
            dlg.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    box,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { ok, cancel } },
                },
            };
            dlg.Opened += (s, e) => { box.Focus(); box.SelectAll(); };
            return await dlg.ShowDialog<bool>(owner) ? box.Text ?? "" : null;
        }

        // true when the user picked Yes (or OK)
        public static Task<bool> Show(Window owner, string text, string title, bool yesNo = false)
        {
            var dlg = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                MaxWidth = 640,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };
            void Add(string label, bool result, bool isDefault)
            {
                var b = new Button { Content = label, MinWidth = 80, IsDefault = isDefault, IsCancel = !result };
                b.Click += (s, e) => dlg.Close(result);
                buttons.Children.Add(b);
            }
            if (yesNo) { Add("Yes", true, true); Add("No", false, false); }
            else Add("OK", true, true);

            dlg.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new SelectableTextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    buttons,
                },
            };
            return dlg.ShowDialog<bool>(owner);
        }
    }
}
