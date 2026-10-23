using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NutStaller.Core;
using NutStaller.Ui;

namespace NutStaller
{
    public class NutStallerMainWindow : Window
    {
        private const int DesignW = 1200;
        private const int DesignH = 675;

        private LauncherState _state = null!;
        private bool _busy;

        private readonly Dictionary<string, BkKeyField> _binds = new();

        private GamepadNavigator? _gamepad;

        // everything is laid out on a 1200x675 canvas over the art; the Viewbox
        // scales it to the window and letterboxes in blueprint blue
        private readonly Canvas _root = new() { Width = DesignW, Height = DesignH };

        private readonly BkButton navSetup = new() { Text = "Set Up Game", Selected = true };
        private readonly BkButton navUpdate = new() { Text = "Check Updates" };
        private readonly BkButton navKeybinds = new() { Text = "Keybinds" };
        private readonly BkButton navWorkshop = new() { Text = "Workshop" };
        private readonly BkButton navSaves = new() { Text = "Saves" };
        private readonly BkButton navCredits = new() { Text = "Credits" };
        private readonly BkButton navPlay = new() { Text = "Play", FontSizePx = 24 };

        private readonly BkPagePanel pageSetup = new();
        private readonly BkValueBox installBox = new() { Transparent = true };
        private readonly BkLabel renutStatus = new() { Dim = true, FontSizePx = 17 };
        private readonly BkLabel xisoStatus = new() { Dim = true, FontSizePx = 17 };
        private readonly BkLabel gameStatus = new() { Dim = true, FontSizePx = 17 };
        private readonly BkProgress progressBar = new();
        private readonly BkLabel setupStatus = new() { Dim = true, FontSizePx = 17 };

        private readonly BkPagePanel pageKeybinds = new() { IsVisible = false };
        private readonly BkValueBox mouseSpeedBox = new() { FontSizePx = 15 };
        private readonly Dictionary<string, (BkButton Button, string Label)> _toggles = new();
        private readonly Dictionary<string, bool> _toggleValues = new();
        private readonly BkLabel keybindsStatus = new() { Dim = true, FontSizePx = 16 };

        private readonly BkPagePanel pageCredits = new() { IsVisible = false };

        private readonly BkPagePanel pageSaves = new() { IsVisible = false };
        private readonly BkLabel savesWhere = new() { Dim = true, FontSizePx = 13 };
        private readonly BkLabel savesSummary = new() { FontSizePx = 17 };
        private readonly StackPanel backupList = new() { Spacing = 4 };
        private readonly BkLabel savesStatus = new() { Dim = true, FontSizePx = 15 };
        private readonly BkLabel savesListTitle = new() { Text = "BACKUPS", Dim = true, FontSizePx = 14 };
        private List<Saves.Vehicle>? _pickVehicles;      // Import vehicles: the other save's
        private readonly HashSet<int> _picked = new();

        // The workshop is its own scene over the menu: Browse (vehicles), Parts
        // (reNut part mods), Installed, Share.
        private readonly Canvas workshopScene = new() { IsVisible = false };
        private readonly BkButton tabBrowse = new() { Text = "Vehicles", Selected = true };
        private readonly BkButton tabParts = new() { Text = "Parts" };
        private readonly BkButton tabInstalled = new() { Text = "Installed" };
        private readonly BkButton tabShare = new() { Text = "Share" };
        private readonly ScrollViewer browsePane = new();
        private readonly WrapPanel browseGrid = new();
        private readonly ScrollViewer partsPane = new() { IsVisible = false };
        private readonly WrapPanel partsGrid = new();
        private readonly ScrollViewer installedPane = new() { IsVisible = false };
        private readonly WrapPanel installedGrid = new();
        private readonly Canvas sharePane = new() { IsVisible = false };
        private readonly BkButton workshopBack = new() { Text = "Back", FontSizePx = 20 };
        private readonly BkButton workshopRefresh = new() { Text = "Refresh", FontSizePx = 18 };
        private readonly BkLabel workshopStatus = new() { Dim = true, FontSizePx = 16 };

        private readonly BkButton shareModeVehicle = new() { Text = "Vehicle", Selected = true, FontSizePx = 18 };
        private readonly BkButton shareModeMod = new() { Text = "Part mod", FontSizePx = 18 };
        private readonly Canvas shareVehicleRow = new();
        private readonly Canvas shareModRow = new() { IsVisible = false };
        private readonly BkLabel shareModFile = new() { FontSizePx = 16 };
        private readonly BkLabel shareUses = new() { FontSizePx = 14, Dim = true };
        private readonly BkLabel shareHelp = new() { FontSizePx = 14, Dim = true };
        private readonly BkLabel shareVehicle = new() { FontSizePx = 18 };
        private readonly BkValueBox shareName = new();
        private readonly BkValueBox shareDescription = new() { FontSizePx = 15 };
        private readonly Image sharePreview = new() { Stretch = Stretch.Uniform };
        private readonly BkLabel shareStatus = new() { Dim = true, FontSizePx = 16 };

        public NutStallerMainWindow()
        {
            Title = "NutStaller";
            Width = DesignW;
            Height = DesignH;
            MinWidth = 800;
            MinHeight = 450;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Assets.Blueprint);
            BuildLayout();
            Content = new Viewbox { Stretch = Stretch.Uniform, Child = _root };

            _state = LauncherState.Load();
            installBox.Value = _state.InstallDir;


            RefreshSetupStatus();

            _gamepad = new GamepadNavigator(this,
                candidates: PadCandidates,
                activate: ActivatePadControl,
                cyclePage: CyclePage,
                start: () => navPlay.PerformClick(),
                back: () => { if (workshopScene.IsVisible) CloseWorkshop(); else CurrentNav().Focus(); });
            Closed += (s, e) => _gamepad?.Dispose();

            _ = CheckForUpdatesAsync();
        }

        private static T Place<T>(Panel parent, T c, double x, double y, double w, double h) where T : Control
        {
            Canvas.SetLeft(c, x);
            Canvas.SetTop(c, y);
            c.Width = w;
            c.Height = h;
            parent.Children.Add(c);
            return c;
        }

        private static BkButton Button(Panel parent, string text, double x, double y, double w, double h,
            Action onClick, double fontSize = 22)
        {
            var b = Place(parent, new BkButton { Text = text, FontSizePx = fontSize }, x, y, w, h);
            b.Click += (s, e) => onClick();
            return b;
        }

        private static void Label(Panel parent, string text, double x, double y, double w, double h,
            double fontSize = 18, bool dim = false) =>
            Place(parent, new BkLabel { Text = text, FontSizePx = fontSize, Dim = dim }, x, y, w, h);

        private void BuildLayout()
        {
            _root.Children.Add(new Image { Source = Assets.Background, Width = DesignW, Height = DesignH });

            Place(_root, navSetup, 402, 318, 185, 31).Click += (s, e) => NavSetup_Click();
            Place(_root, navUpdate, 402, 351, 185, 31).Click += async (s, e) => await NavUpdate_Click();
            Place(_root, navKeybinds, 402, 384, 185, 31).Click += (s, e) => NavKeybinds_Click();
            Place(_root, navWorkshop, 402, 417, 185, 31).Click += (s, e) => NavWorkshop_Click();
            Place(_root, navSaves, 402, 450, 185, 31).Click += (s, e) => NavSaves_Click();
            Place(_root, navCredits, 402, 483, 185, 31).Click += (s, e) => ShowPage(pageCredits, navCredits);
            Place(_root, navPlay, 402, 522, 185, 36).Click += (s, e) => LaunchGame();

            var p = Place(_root, pageSetup, 634, 281, 551, 323);
            Label(p, "SET UP RENUT", 18, 16, 300, 30, 26);
            Button(p, "1. Get latest reNut", 19, 58, 240, 32, () => _ = RunBusy(DoDownloadRenut), 20);
            Button(p, "2. Get extract-xiso", 19, 98, 240, 32, () => _ = RunBusy(DoDownloadXiso), 20);
            Button(p, "3. Pick ISO + extract", 19, 138, 240, 32, () => _ = RunBusy(DoPickIsoAndExtract), 20);
            Button(p, "Do everything", 19, 178, 240, 34, () => _ = RunBusy(async () =>
            {
                await DoDownloadRenut();
                await DoDownloadXiso();
                await DoPickIsoAndExtract();
                if (_state.GameExtracted) await DoAddTitleUpdate();
            }));
            Button(p, "4. Add title update", 268, 178, 274, 34, () => _ = RunBusy(DoAddTitleUpdate), 20);
            Place(p, renutStatus, 268, 58, 284, 32);
            Place(p, xisoStatus, 268, 98, 284, 32);
            Place(p, gameStatus, 268, 138, 284, 32);
            Label(p, "INSTALL FOLDER", 19, 230, 150, 30, dim: true);
            Place(p, installBox, 175, 228, 299, 30);
            Button(p, "...", 499, 230, 40, 30, () => _ = BrowseButton_Click(), 20);
            Place(p, progressBar, 19, 267, 523, 14);
            Place(p, setupStatus, 16, 284, 536, 36);

            p = Place(_root, pageKeybinds, 634, 279, 545, 382);
            Label(p, "KEYBINDS", 16, 8, 300, 30, 26);
            Toggle(p, "renut_keyboard", "Keyboard", 16);
            Toggle(p, "renut_mouse_camera", "Mouse look", 192);
            Toggle(p, "renut_mouse_invert_y", "Invert Y", 368);
            Label(p, "MOUSE SPEED", 16, 80, 120, 26, 14, true);
            Place(p, mouseSpeedBox, 136, 80, 60, 26);
            Label(p, "CLICK A KEY, THEN PRESS A KEY OR CLICK A MOUSE BUTTON", 210, 80, 330, 26, 12, true);
            for (int i = 0; i < PadKeys.Length; i++)
                KeyRow(PadKeys[i].Key, PadKeys[i].Label, i);
            Button(p, "Save", 16, 344, 110, 30, SaveKeybindsButton_Click, 20);
            Place(p, keybindsStatus, 136, 344, 320, 30);

            p = Place(_root, pageSaves, 634, 279, 545, 382);
            Label(p, "SAVES", 16, 10, 200, 30, 26);
            Place(p, savesWhere, 16, 44, 400, 22);
            Button(p, "Open folder", 420, 40, 110, 28, () => OpenUrl(SaveDir), 15);
            Place(p, savesSummary, 16, 70, 516, 26);
            Button(p, "Back up now", 16, 102, 146, 32, () => SavesAction(() => $"BACKED UP TO {Path.GetFileName(Saves.BackUp(SaveDir))}"), 17);
            Button(p, "Import a save...", 170, 102, 172, 32, () => _ = ImportSave(), 17);
            Button(p, "Import vehicles...", 350, 102, 182, 32, () => _ = ImportVehicles(), 17);
            Place(p, savesListTitle, 16, 142, 516, 22);
            Place(p, new ScrollViewer { Content = backupList }, 16, 164, 516, 172);
            Place(p, savesStatus, 16, 340, 516, 36);

            p = Place(_root, pageCredits, 634, 281, 548, 382);
            Label(p, "CREDITS", 16, 14, 300, 30, 26);
            Label(p, "RENUT BY MASTERSPIKE52 AND CONTRIBUTORS", 16, 56, 516, 26);
            string[] lines =
            {
                "REXGLUE TEAM - REXGLUE SDK",
                "SOLARCOOKIES - MIDASM HOOKS AND CRT FUNCTIONS",
                "VALCOMDRIFTY - RENUT LOGO",
                "OLIEGAMERTV - NUTS AND BOLTS DOCUMENTATION",
                "EXTRACT-XISO BY THE XBOXDEV TEAM",
                "THIS - ETONEDEMID",
            };
            for (int i = 0; i < lines.Length; i++)
                Label(p, lines[i], 16, 88 + i * 26, 516, 24, 16, true);
            Button(p, "reNut on GitHub", 16, 268, 240, 32, () => OpenUrl("https://github.com/masterspike52/reNut"), 20);
            Button(p, "Join the Discord", 268, 268, 240, 32, () => OpenUrl("https://discord.gg/D5Bz2ZsHdY"), 20);
            Label(p, "GAMEPAD: DPAD MOVE - A SELECT - LB/RB SWITCH PAGE - START PLAY", 16, 330, 516, 24, 14, true);
            BuildWorkshopPages();
        }

        private void BuildWorkshopPages()
        {
            var sc = Place(_root, workshopScene, 0, 0, DesignW, DesignH);
            sc.Children.Add(new Image { Source = Assets.Background, Width = DesignW, Height = DesignH });
            var p = Place(sc, new BkPagePanel { Background = new SolidColorBrush(Color.FromArgb(215, 14, 28, 80)) },
                30, 24, 1140, 627);
            Label(p, "WORKSHOP", 22, 14, 190, 36, 30);
            var tabs = new[] { tabBrowse, tabParts, tabInstalled, tabShare };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                Place(p, tab, 220 + i * 160, 16, 150, 32).Click += (s, e) => ShowTab(tab);
            }
            Place(p, workshopBack, 1010, 16, 110, 32).Click += (s, e) => CloseWorkshop();

            browsePane.Content = browseGrid;
            partsPane.Content = partsGrid;
            installedPane.Content = installedGrid;
            foreach (var pane in new[] { browsePane, partsPane, installedPane })
            {
                pane.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
                Place(p, pane, 14, 62, 1114, 512);
            }
            Place(p, workshopRefresh, 1000, 584, 120, 30).Click += (s, e) => _ = RefreshWorkshop();
            Place(p, workshopStatus, 22, 584, 960, 30);

            var sh = Place(p, sharePane, 14, 62, 1114, 512);
            Label(sh, "SHARE", 10, 6, 100, 30, 22);
            Place(sh, shareModeVehicle, 110, 6, 140, 30).Click += (s, e) => ShareMode(false);
            Place(sh, shareModeMod, 260, 6, 140, 30).Click += (s, e) => ShareMode(true);

            var row = Place(sh, shareVehicleRow, 0, 50, 520, 30);
            Label(row, "VEHICLE", 10, 0, 120, 30, dim: true);
            Button(row, "<", 130, 0, 34, 30, () => PickVehicle(-1), 18);
            Place(row, shareVehicle, 174, 0, 300, 30);
            Button(row, ">", 480, 0, 34, 30, () => PickVehicle(1), 18);
            row = Place(sh, shareModRow, 0, 50, 520, 30);
            Label(row, "MOD FILE", 10, 0, 120, 30, dim: true);
            Button(row, "Pick .nbpatch", 130, 0, 170, 30, () => _ = PickModFile(), 18);
            Place(row, shareModFile, 310, 0, 210, 30);

            Label(sh, "NAME", 10, 92, 120, 30, dim: true);
            Place(sh, shareName, 130, 92, 384, 30);
            Label(sh, "ABOUT IT", 10, 134, 120, 30, dim: true);
            Place(sh, shareDescription, 130, 134, 384, 30);
            Place(sh, shareUses, 10, 168, 504, 40);
            Button(sh, "Pick screenshot", 10, 212, 220, 32, () => _ = PickScreenshot(), 18);
            Label(sh, "A PICTURE OF IT, FROM THE GAME OR ANYWHERE", 10, 250, 504, 24, 14, true);
            Button(sh, "Copy and open GitHub", 10, 286, 260, 34, () => _ = CopySubmission(), 20);
            Place(sh, shareStatus, 10, 328, 504, 66);
            Place(sh, shareHelp, 10, 400, 504, 100);
            Place(sh, sharePreview, 540, 50, 560, 315);
            ShareMode(false);
        }

        // ------------------------------------------------------------- workshop

        private List<Workshop.Entry>? _entries;
        private List<Workshop.ModEntry> _mods = new();
        private List<Workshop.Installed> _installed = new();
        private List<Workshop.InstalledMod> _installedMods = new();
        private List<Workshop.LocalMod> _localMods = new();
        private readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> _previews = new();
        private List<Workshop.LocalVehicle> _vehicles = new();
        private int _vehicleIndex;
        private string? _screenshot;
        private string? _modFile;
        private bool _shareMod;

        private string SaveDir => Workshop.SaveDir(_state);
        private string ModsDir => Workshop.ModsDir(_state);

        private void NavWorkshop_Click()
        {
            workshopScene.IsVisible = true;
            ShowTab(tabBrowse);
            if (_entries == null) _ = RefreshWorkshop();
            tabBrowse.Focus();
        }

        private void CloseWorkshop()
        {
            workshopScene.IsVisible = false;
            navWorkshop.Focus();
        }

        private void ShowTab(BkButton tab)
        {
            foreach (var t in new[] { tabBrowse, tabParts, tabInstalled, tabShare }) t.Selected = t == tab;
            browsePane.IsVisible = tab == tabBrowse;
            partsPane.IsVisible = tab == tabParts;
            installedPane.IsVisible = tab == tabInstalled;
            sharePane.IsVisible = tab == tabShare;
            workshopRefresh.IsVisible = tab == tabBrowse || tab == tabParts;
            if (tab == tabShare) OpenShare();
            else RebuildGrids();
        }

        private async Task RefreshWorkshop()
        {
            workshopStatus.SetText("LOADING THE WORKSHOP...");
            try { (_entries, _mods) = await Workshop.FetchIndexAsync(); }
            catch (Exception ex)
            {
                workshopStatus.SetText("COULD NOT REACH THE WORKSHOP: " + ex.Message.Split('\n')[0].ToUpperInvariant());
                return;
            }
            RebuildGrids();
        }

        // What a vehicle needs: the names of the workshop mods it uses, then
        // parts from mods the workshop does not have.
        private string Needs(Workshop.Entry e) => string.Join(", ",
            e.Requires.Select(r => _mods.FirstOrDefault(m => m.Id == r)?.Name ?? "mod #" + r).Concat(e.Missing));

        private bool ModPresent(string modId) =>
            _installedMods.Any(m => m.ModId == modId) || _localMods.Any(m => m.ModId == modId);

        private void RebuildGrids()
        {
            _installed = Workshop.LoadInstalled(SaveDir);
            _installedMods = Workshop.LoadInstalledMods(SaveDir);
            _localMods = Workshop.LocalMods(ModsDir);
            browseGrid.Children.Clear();
            foreach (var e in _entries ?? new())
            {
                bool have = _installed.Any(i => i.Id == e.Id);
                browseGrid.Children.Add(Tile(e.Name, e.Author, e.Description, $"{e.Parts} PARTS", Needs(e), e.PreviewPath,
                    have ? "Remove" : "Download", have ? () => RemoveBlueprint(e.Id, e.Name) : () => _ = InstallBlueprint(e)));
            }
            partsGrid.Children.Clear();
            foreach (var m in _mods)
            {
                bool ours = _installedMods.Any(i => i.ModId == m.ModId);
                string info = $"{m.PartNames.Count} PARTS - V{m.Version} - {Math.Max(1, m.Size / 1024)} KB";
                partsGrid.Children.Add(ours
                    ? Tile(m.Name, m.Author, m.Description, info, "PARTS: " + string.Join(", ", m.PartNames), m.PreviewPath,
                        "Remove", () => RemoveMod(m.ModId, m.Name))
                    : ModPresent(m.ModId)
                        ? Tile(m.Name, m.Author, m.Description, "IN YOUR MODS FOLDER", "PARTS: " + string.Join(", ", m.PartNames),
                            m.PreviewPath, null, null)
                        : Tile(m.Name, m.Author, m.Description, info, "PARTS: " + string.Join(", ", m.PartNames), m.PreviewPath,
                            "Download", () => _ = InstallMod(m)));
            }
            installedGrid.Children.Clear();
            foreach (var i in _installed)
                installedGrid.Children.Add(Tile(i.Name, i.Author, i.Description, $"{i.Parts} PARTS", "", i.PreviewPath,
                    "Remove", () => RemoveBlueprint(i.Id, i.Name)));
            foreach (var m in _installedMods)
                installedGrid.Children.Add(Tile(m.Name, m.Author, m.Description, $"PART MOD - {m.Parts} PARTS", "",
                    m.PreviewPath, "Remove", () => RemoveMod(m.ModId, m.Name)));
            if (installedPane.IsVisible)
                workshopStatus.SetText(_installed.Count + _installedMods.Count == 0 ? "NOTHING INSTALLED FROM THE WORKSHOP YET"
                    : $"{_installed.Count} VEHICLES, {_installedMods.Count} PART MODS INSTALLED");
            else if (partsPane.IsVisible && _entries != null)
                workshopStatus.SetText(_mods.Count == 0 ? "NO PART MODS YET - SHARE THE FIRST ONE!"
                    : $"{_mods.Count} PART MODS - THEY LOAD WHEN RENUT STARTS ({ModsDir})");
            else if (_entries != null)
                workshopStatus.SetText(_entries.Count == 0 ? "NOTHING HERE YET - SHARE THE FIRST ONE!"
                    : $"{_entries.Count} BLUEPRINTS - {_installed.Count} INSTALLED");
        }

        // One card of a grid: picture, name, author, about, a line of info, what
        // it needs (or has), and its button.
        private Control Tile(string name, string author, string description, string info, string needs, string preview,
            string? button, Action? onClick)
        {
            var tile = new Canvas
            {
                Width = 266, Height = 262, Margin = new Thickness(6),
                Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 40)),
            };
            var image = Place(tile, new Image { Stretch = Stretch.UniformToFill }, 4, 4, 258, 145);
            Place(tile, new BkLabel { Text = name, FontSizePx = 19 }, 6, 152, 254, 24);
            Place(tile, new BkLabel { Text = $"BY {author}".ToUpperInvariant(), FontSizePx = 13, Dim = true }, 6, 176, 254, 18);
            Place(tile, new BkLabel { Text = description, FontSizePx = 13, Dim = true }, 6, 194, 254, needs.Length > 0 ? 18 : 32);
            if (needs.Length > 0)
                Place(tile, new BkLabel { Text = needs.StartsWith("PARTS: ") ? needs : "NEEDS: " + needs, FontSizePx = 12 },
                    6, 211, 254, 17);
            Place(tile, new BkLabel { Text = info, FontSizePx = 13, Dim = true }, 6, 232, button == null ? 254 : 128, 24);
            if (button != null && onClick != null)
                Button(tile, button, 136, 228, 124, 30, onClick, 18);
            _ = LoadPreview(image, preview);
            return tile;
        }

        private async Task LoadPreview(Image image, string path)
        {
            try
            {
                if (!_previews.TryGetValue(path, out var bmp))
                {
                    using var ms = new MemoryStream(await Workshop.DownloadAsync(path));
                    _previews[path] = bmp = new Avalonia.Media.Imaging.Bitmap(ms);
                }
                image.Source = bmp;
            }
            catch { } // no picture is fine
        }

        // The vehicle, and first every workshop mod it needs that is not there yet.
        private async Task InstallBlueprint(Workshop.Entry e)
        {
            try
            {
                int mods = 0;
                foreach (var m in e.Requires.Select(r => _mods.FirstOrDefault(m => m.Id == r)).OfType<Workshop.ModEntry>())
                {
                    if (ModPresent(m.ModId)) continue;
                    workshopStatus.SetText($"DOWNLOADING THE PART MOD {m.Name.ToUpperInvariant()}...");
                    await Workshop.InstallModAsync(SaveDir, ModsDir, m);
                    _installedMods = Workshop.LoadInstalledMods(SaveDir);
                    mods++;
                }
                workshopStatus.SetText($"DOWNLOADING {e.Name.ToUpperInvariant()}...");
                var item = Workshop.InstallEntry(SaveDir, e, await Workshop.DownloadAsync(e.BlueprintPath));
                RebuildGrids();
                workshopStatus.SetText($"INSTALLED {e.Name.ToUpperInvariant()}" + (mods > 0 ? $" AND {mods} PART MOD(S)" : "") +
                    (e.Missing.Count > 0 ? " - IT ALSO USES PARTS NOT IN THE WORKSHOP: " + string.Join(", ", e.Missing).ToUpperInvariant()
                                         : " - LOAD IT IN THE GARAGE"));
            }
            catch (Exception ex) { workshopStatus.SetText("DOWNLOAD FAILED: " + ex.Message.ToUpperInvariant()); }
        }

        private void RemoveBlueprint(string id, string name)
        {
            try
            {
                Workshop.Remove(SaveDir, id);
                RebuildGrids();
                workshopStatus.SetText($"REMOVED {name.ToUpperInvariant()} FROM YOUR SAVE");
            }
            catch (Exception ex) { workshopStatus.SetText("REMOVE FAILED: " + ex.Message.ToUpperInvariant()); }
        }

        private async Task InstallMod(Workshop.ModEntry m)
        {
            try
            {
                workshopStatus.SetText($"DOWNLOADING {m.Name.ToUpperInvariant()}...");
                await Workshop.InstallModAsync(SaveDir, ModsDir, m);
                RebuildGrids();
                workshopStatus.SetText($"INSTALLED {m.Name.ToUpperInvariant()} - ITS PARTS ARE IN THE GARAGE NEXT TIME RENUT STARTS");
            }
            catch (Exception ex) { workshopStatus.SetText("DOWNLOAD FAILED: " + ex.Message.ToUpperInvariant()); }
        }

        private void RemoveMod(string modId, string name)
        {
            try
            {
                Workshop.RemoveMod(SaveDir, modId);
                RebuildGrids();
                workshopStatus.SetText($"REMOVED {name.ToUpperInvariant()} - VEHICLES USING ITS PARTS GET THEIR BASE PARTS");
            }
            catch (Exception ex) { workshopStatus.SetText("REMOVE FAILED: " + ex.Message.ToUpperInvariant()); }
        }

        // Your own builds only: the workshop's are someone else's to share.
        private void OpenShare()
        {
            var fromWorkshop = Workshop.LoadInstalled(SaveDir)
                .Select(i => Path.GetFullPath(Path.Combine(SaveDir, "00000001", i.ContentId, i.ContentId[2..]))).ToHashSet();
            _vehicles = Workshop.LocalVehicles(SaveDir).Where(v => !fromWorkshop.Contains(Path.GetFullPath(v.File))).ToList();
            _vehicleIndex = Math.Min(_vehicleIndex, Math.Max(0, _vehicles.Count - 1));
            workshopStatus.SetText("");
            ShareMode(_shareMod);
        }

        private void ShareMode(bool mod)
        {
            _shareMod = mod;
            shareModeVehicle.Selected = !mod;
            shareModeMod.Selected = mod;
            shareVehicleRow.IsVisible = !mod;
            shareModRow.IsVisible = mod;
            shareStatus.SetText("");
            if (mod)
            {
                shareUses.SetText("");
                if (_modFile != null && !File.Exists(_modFile)) { _modFile = null; shareModFile.SetText(""); }
                shareName.Value = _modFile != null ? Workshop.ReadMod(_modFile).Name : "";
                shareHelp.SetText("A RENUT PART MOD (.NBPATCH). NUTSTALLER COPIES THE DETAILS AND PUTS THE MOD IN A FOLDER " +
                    "AS A .ZIP: PASTE INTO THE FORM AND DRAG THE ZIP INTO 'MOD FILE'. ONLY PART MODS ARE HOSTED, " +
                    "AND A MAINTAINER APPROVES EACH ONE.");
            }
            else
            {
                PickVehicle(0);
                if (_vehicles.Count == 0) shareStatus.SetText("NO VEHICLES IN YOUR SAVE YET - BUILD ONE IN THE GAME FIRST");
                shareHelp.SetText("NUTSTALLER COPIES YOUR VEHICLE AND PICTURE. PASTE IT INTO THE GITHUB FORM, " +
                    "TICK THE BOX AND SUBMIT. A BOT PUBLISHES IT WITHIN A MINUTE (3 PER 10 MINUTES).");
            }
        }

        private void PickVehicle(int delta)
        {
            if (_vehicles.Count == 0) { shareVehicle.SetText("NONE"); shareUses.SetText(""); return; }
            _vehicleIndex = (_vehicleIndex + delta + _vehicles.Count) % _vehicles.Count;
            shareVehicle.SetText(_vehicles[_vehicleIndex].Name);
            shareName.Value = _vehicles[_vehicleIndex].Name;
            var custom = Workshop.CustomPartsIn(File.ReadAllBytes(_vehicles[_vehicleIndex].File), ModsDir);
            shareUses.SetText(custom.Count == 0 ? "" : "USES CUSTOM PARTS: " +
                string.Join(", ", custom.Select(c => $"{c.Id} ({c.Mod})")).ToUpperInvariant());
        }

        private async Task PickModFile()
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Pick a reNut part mod",
                FileTypeFilter = new[] { new FilePickerFileType("reNut mod") { Patterns = new[] { "*.nbpatch" } } },
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(ModsDir),
            });
            string? path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (path == null) return;
            try
            {
                var mod = Workshop.ReadMod(path);
                _modFile = path;
                shareModFile.SetText($"{Path.GetFileName(path)} - {mod.PartIds.Count} PARTS");
                shareName.Value = mod.Name;
                shareStatus.SetText(mod.PartIds.Count == 0 ? "THAT MOD HAS NO PARTS; THE WORKSHOP HOSTS PART MODS" : "");
            }
            catch (Exception ex) { shareStatus.SetText("ERROR: " + ex.Message.ToUpperInvariant()); }
        }

        private async Task PickScreenshot()
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Pick a picture",
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
            });
            string? path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (path == null) return;
            try
            {
                sharePreview.Source = new Avalonia.Media.Imaging.Bitmap(path);
                _screenshot = path;
            }
            catch { shareStatus.SetText("THAT PICTURE COULD NOT BE READ"); }
        }

        private async Task CopySubmission()
        {
            if (_shareMod ? _modFile == null : _vehicles.Count == 0)
            {
                shareStatus.SetText(_shareMod ? "PICK THE .NBPATCH FIRST" : "NO VEHICLE TO SHARE");
                return;
            }
            if (shareName.Value.Trim().Length == 0) { shareStatus.SetText("GIVE IT A NAME"); return; }
            if (_screenshot == null) { shareStatus.SetText("PICK A SCREENSHOT FIRST"); return; }
            try
            {
                if (_shareMod)
                {
                    var (json, zip) = Workshop.BuildModSubmission(_modFile!, shareName.Value, shareDescription.Value, _screenshot);
                    await Clipboard!.SetTextAsync(json);
                    OpenUrl(Workshop.SubmitModUrl);
                    OpenUrl(Path.GetDirectoryName(zip)!);
                    shareStatus.SetText($"COPIED - PASTE IT INTO THE FORM, DRAG {Path.GetFileName(zip).ToUpperInvariant()} " +
                        "FROM THE FOLDER THAT OPENED INTO 'MOD FILE', TICK THE BOX, SUBMIT");
                }
                else
                {
                    byte[] data = File.ReadAllBytes(_vehicles[_vehicleIndex].File);
                    string json = Workshop.BuildSubmission(data, shareName.Value, shareDescription.Value, _screenshot,
                        Workshop.CustomPartsIn(data, ModsDir));
                    await Clipboard!.SetTextAsync(json);
                    OpenUrl(Workshop.SubmitUrl);
                    shareStatus.SetText("COPIED - PASTE IT INTO THE GITHUB FORM, TICK THE BOX, SUBMIT");
                }
            }
            catch (Exception ex) { shareStatus.SetText("ERROR: " + ex.Message.ToUpperInvariant()); }
        }

        // ------------------------------------------------------------- saves

        private void NavSaves_Click()
        {
            _pickVehicles = null;
            ShowPage(pageSaves, navSaves);
            savesStatus.SetText("");
            RefreshSaves();
        }

        private void RefreshSaves()
        {
            savesWhere.SetText(SaveDir);
            var sum = Saves.Summarize(SaveDir);
            savesSummary.SetText(sum.Bytes == 0 ? "NO SAVE YET - PLAY THE GAME ONCE, OR IMPORT ONE"
                : $"{sum.Slots} SAVE SLOT(S) - {sum.Vehicles} VEHICLES - {sum.Replays} REPLAYS - {Math.Max(1, sum.Bytes / 1024)} KB");
            backupList.Children.Clear();
            savesListTitle.SetText(_pickVehicles == null ? "BACKUPS" : "PICK THE VEHICLES TO ADD TO YOUR SAVE");
            if (_pickVehicles != null)
            {
                ShowVehiclePicker(_pickVehicles);
                return;
            }
            foreach (var b in Saves.Backups(SaveDir))
            {
                var row = new Canvas { Height = 30, Width = 500 };
                Place(row, new BkLabel
                {
                    Text = b.Note.Length > 0 ? $"{b.Note.ToUpperInvariant()} ({b.Made:MM-dd HH:mm})"
                        : $"{b.Made:yyyy-MM-dd HH:mm:ss} - {Math.Max(1, b.Bytes / 1024)} KB",
                    FontSizePx = 14,
                }, 4, 3, 236, 24);
                Button(row, "Rename", 240, 0, 84, 28, () => _ = RenameBackup(b), 15);
                Button(row, "Restore", 328, 0, 88, 28, () => _ = RestoreBackup(b), 15);
                Button(row, "Delete", 420, 0, 76, 28, () => _ = DeleteBackup(b), 15);
                backupList.Children.Add(row);
            }
            if (backupList.Children.Count == 0)
                backupList.Children.Add(new BkLabel { Text = "NO BACKUPS YET", FontSizePx = 14, Dim = true, Height = 24 });
        }

        private void SavesAction(Func<string> action)
        {
            try { savesStatus.SetText(action()); }
            catch (Exception ex) { savesStatus.SetText("ERROR: " + ex.Message.ToUpperInvariant()); }
            RefreshSaves();
        }

        private async Task RestoreBackup(Saves.Backup b)
        {
            if (!await MessageDialog.Show(this, $"Replace your save with the backup from {b.Made:yyyy-MM-dd HH:mm:ss}?" +
                    $"{Environment.NewLine}{Environment.NewLine}Your current save is backed up first.", "Restore a backup", yesNo: true))
                return;
            SavesAction(() => Saves.Replace(SaveDir, b.File, "restore") is { } kept
                ? $"RESTORED - THE SAVE BEFORE IT IS IN {Path.GetFileName(kept)}" : "RESTORED");
        }

        private async Task RenameBackup(Saves.Backup b)
        {
            string? name = await MessageDialog.Ask(this, $"Name the backup from {b.Made:yyyy-MM-dd HH:mm:ss}" +
                " (letters, digits, spaces, - and _; empty clears it):", "Rename a backup", b.Note);
            if (name == null) return;
            SavesAction(() => $"RENAMED TO {Path.GetFileName(Saves.Rename(b.File, name))}");
        }

        private async Task DeleteBackup(Saves.Backup b)
        {
            if (!await MessageDialog.Show(this, $"Delete the backup from {b.Made:yyyy-MM-dd HH:mm:ss}? This cannot be undone.",
                    "Delete a backup", yesNo: true))
                return;
            SavesAction(() => { File.Delete(b.File); return "BACKUP DELETED"; });
        }

        // A save from elsewhere (Saves.Normalize): a zip, a folder, or an Xbox
        // 360 save package; null when cancelled.
        private async Task<string?> PickSaveSource(string title)
        {
            bool file = await MessageDialog.Show(this, "Is the save a single file?" + Environment.NewLine + Environment.NewLine +
                "Yes: pick it (a .zip, or an Xbox 360 save package)." + Environment.NewLine +
                "No: pick the folder (the 4D5307ED folder, one that has it inside, or a folder of Xbox 360 saves).",
                title, yesNo: true);
            if (file)
            {
                var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Pick the save" });
                return picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            }
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Pick the save folder" });
            return folder.Count > 0 ? folder[0].TryGetLocalPath() : null;
        }

        private async Task ImportSave()
        {
            string? path = await PickSaveSource("Import a save");
            if (path == null) return;
            if (!await MessageDialog.Show(this, $"Replace your save with{Environment.NewLine}{path}?" +
                    $"{Environment.NewLine}{Environment.NewLine}Your current save is backed up first.", "Import a save", yesNo: true))
                return;
            SavesAction(() => Saves.Replace(SaveDir, path, "import") is { } kept
                ? $"IMPORTED - YOUR OLD SAVE IS IN {Path.GetFileName(kept)}" : "IMPORTED");
        }

        // Vehicles from another save, picked one by one, added to this one.
        private async Task ImportVehicles()
        {
            string? path = await PickSaveSource("Import vehicles");
            if (path == null) return;
            try
            {
                var found = Saves.VehiclesIn(path);
                if (found.Count == 0)
                {
                    savesStatus.SetText("NO VEHICLES IN THAT SAVE");
                    return;
                }
                _pickVehicles = found;
                _picked.Clear();
                savesStatus.SetText($"{found.Count} VEHICLES FOUND");
            }
            catch (Exception ex) { savesStatus.SetText("ERROR: " + ex.Message.ToUpperInvariant()); }
            RefreshSaves();
        }

        private void ShowVehiclePicker(List<Saves.Vehicle> vehicles)
        {
            // toggled in place, so the list keeps its scroll and the gamepad its focus
            var top = new Canvas { Height = 34, Width = 500 };
            var rows = new List<BkButton>();
            BkButton add = null!, all = null!;
            void Show()
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    rows[i].Selected = _picked.Contains(i);
                    rows[i].Text = $"[{(rows[i].Selected ? "X" : " ")}]  {vehicles[i].Name}";
                }
                add.Text = $"Add {_picked.Count} to my save";
                all.Text = _picked.Count == vehicles.Count ? "None" : "All";
            }
            add = Button(top, "", 0, 0, 200, 30, () =>
            {
                if (_picked.Count == 0) { savesStatus.SetText("PICK AT LEAST ONE VEHICLE"); return; }
                var chosen = _picked.OrderBy(i => i).Select(i => vehicles[i]).ToList();
                _pickVehicles = null;
                SavesAction(() => $"ADDED {Saves.ImportVehicles(SaveDir, chosen)} VEHICLES - THEY ARE IN THE GARAGE");
            }, 16);
            all = Button(top, "", 206, 0, 90, 30, () =>
            {
                if (_picked.Count == vehicles.Count) _picked.Clear();
                else _picked.UnionWith(Enumerable.Range(0, vehicles.Count));
                Show();
            }, 16);
            Button(top, "Cancel", 302, 0, 90, 30, () => { _pickVehicles = null; savesStatus.SetText(""); RefreshSaves(); }, 16);
            backupList.Children.Add(top);
            for (int i = 0; i < vehicles.Count; i++)
            {
                int index = i;
                var row = new Canvas { Height = 30, Width = 500 };
                rows.Add(Button(row, "", 0, 0, 496, 28, () =>
                {
                    if (!_picked.Remove(index)) _picked.Add(index);
                    Show();
                }, 16));
                backupList.Children.Add(row);
            }
            Show();
        }

        // ------------------------------------------------------------- updates

        private string? _latestTag;

        // Windows: asset names change between releases (renut-win-x64.zip,
        // renut.zip), so any zip will do; one that says "win" wins over one
        // that doesn't. Elsewhere: the AppImage (renut.AppImage, reNut-x86_64.AppImage).
        private static int RenutAssetRank(string n) =>
            !OperatingSystem.IsWindows() ? (n.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase) ? 0 : -1)
            : !n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? -1
            : n.Contains("win", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        private static void MakeExecutable(string path)
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, File.GetUnixFileMode(path) |
                    UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var asset = await GithubDownloader.GetLatestAssetAsync("masterspike52/reNut", RenutAssetRank);
                _latestTag = asset.Tag;
            }
            catch
            {
                return; // offline or rate limited; the button stays "Check Updates"
            }
            RefreshUpdateButton();
        }

        private void RefreshUpdateButton()
        {
            if (_latestTag == null) navUpdate.Text = "Check Updates";
            else if (!_state.RenutInstalled || _state.RenutVersion != _latestTag) navUpdate.Text = "Update reNut!";
            else navUpdate.Text = "Up To Date";
        }

        private async Task NavUpdate_Click()
        {
            // progress and status live on the setup page, so show it
            ShowPage(pageSetup, navSetup);
            await RunBusy(async () =>
            {
                SetStatus("Checking for reNut updates...");
                var asset = await GithubDownloader.GetLatestAssetAsync("masterspike52/reNut", RenutAssetRank);
                _latestTag = asset.Tag;
                if (_state.RenutInstalled && _state.RenutVersion == asset.Tag)
                {
                    SetStatus($"reNut is already up to date ({asset.Tag}).");
                    return;
                }
                await DoDownloadRenut();
            });
            RefreshUpdateButton();
        }

        // ------------------------------------------------------------- gamepad

        private IEnumerable<Control> PadCandidates()
        {
            if (workshopScene.IsVisible)
            {
                foreach (var c in WorkshopPadCandidates()) yield return c;
                yield break;
            }
            foreach (var nav in new Control[] { navSetup, navUpdate, navKeybinds, navWorkshop, navSaves, navCredits, navPlay })
                yield return nav;
            if (pageSaves.IsVisible)
                foreach (var b in backupList.GetVisualDescendants().OfType<BkButton>())
                    yield return b;
            var page = CurrentPage();
            foreach (var c in page.Children)
                if (c is BkButton or BkValueBox or BkKeyField)
                    yield return c;
        }

        private IEnumerable<Control> WorkshopPadCandidates()
        {
            foreach (var c in new Control[] { tabBrowse, tabParts, tabInstalled, tabShare })
                yield return c;
            yield return workshopBack;
            if (workshopRefresh.IsVisible) yield return workshopRefresh;
            if (sharePane.IsVisible)
                foreach (var c in sharePane.GetVisualDescendants().OfType<Control>())
                    if (c is BkButton or BkValueBox && c.IsEffectivelyVisible) yield return c;
            var grid = browsePane.IsVisible ? browseGrid : partsPane.IsVisible ? partsGrid : installedPane.IsVisible ? installedGrid : null;
            if (grid != null)
                foreach (var tile in grid.Children.OfType<Canvas>())
                    foreach (var b in tile.Children.OfType<BkButton>())
                        yield return b;
        }

        private void ActivatePadControl(Control c)
        {
            switch (c)
            {
                case BkButton b: b.PerformClick(); break;
                case BkValueBox v: v.BeginEdit(); break;
                case BkKeyField k: k.BeginListen(); break;
            }
        }

        private BkPagePanel CurrentPage()
        {
            if (pageKeybinds.IsVisible) return pageKeybinds;
            if (pageCredits.IsVisible) return pageCredits;
            if (pageSaves.IsVisible) return pageSaves;
            return pageSetup;
        }

        private BkButton CurrentNav()
        {
            if (pageKeybinds.IsVisible) return navKeybinds;
            if (pageCredits.IsVisible) return navCredits;
            if (pageSaves.IsVisible) return navSaves;
            return navSetup;
        }

        private void CyclePage(int delta)
        {
            if (workshopScene.IsVisible)
            {
                var tabs = new[] { tabBrowse, tabParts, tabInstalled, tabShare };
                var tab = tabs[(Array.FindIndex(tabs, t => t.Selected) + delta + tabs.Length) % tabs.Length];
                ShowTab(tab);
                tab.Focus();
                return;
            }
            var navs = new[] { navSetup, navKeybinds, navSaves, navCredits };
            int idx = Array.IndexOf(navs, CurrentNav());
            var next = navs[(idx + delta + navs.Length) % navs.Length];
            next.PerformClick();
            next.Focus();
        }

        // ------------------------------------------------------------- nav

        private void NavSetup_Click()
        {
            ShowPage(pageSetup, navSetup);
            RefreshSetupStatus();
        }

        private void NavKeybinds_Click()
        {
            LoadKeybindsIntoUi();
            ShowPage(pageKeybinds, navKeybinds);
        }

        // UseShellExecute hands the url to the default browser (xdg-open on Linux)
        private static void OpenUrl(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private void ShowPage(BkPagePanel page, BkButton nav)
        {
            foreach (var p in new[] { pageSetup, pageKeybinds, pageSaves, pageCredits })
                p.IsVisible = p == page;
            foreach (var b in new[] { navSetup, navKeybinds, navSaves, navCredits })
                b.Selected = b == nav;
        }

        // ------------------------------------------------------------- setup

        private async Task BrowseButton_Click()
        {
            var start = await StorageProvider.TryGetFolderFromPathAsync(
                Directory.Exists(_state.InstallDir) ? _state.InstallDir : LauncherState.HomeDir);
            var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Where should reNut live?",
                SuggestedStartLocation = start,
            });
            string? path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (path != null)
            {
                _state.InstallDir = path;
                installBox.Value = _state.InstallDir;
                _state.Save();
                RefreshSetupStatus();
            }
        }

        private void RefreshSetupStatus()
        {
            if (installBox.Value.Trim().Length > 0) _state.InstallDir = installBox.Value.Trim();
            renutStatus.SetText(_state.RenutInstalled
                ? $"INSTALLED {(_state.RenutVersion.Length > 0 ? "(" + _state.RenutVersion.ToUpperInvariant() + ")" : "")}"
                : "NOT INSTALLED");
            xisoStatus.SetText(_state.XisoInstalled ? "READY" : "NOT INSTALLED");
            gameStatus.SetText(!_state.GameExtracted ? "NO GAME DATA YET"
                : _state.UpdateInstalled ? "GAME DATA + TITLE UPDATE" : "GAME DATA FOUND");
        }

        private async Task RunBusy(Func<Task> work)
        {
            if (_busy) return;
            _busy = true;
            try { await work(); }
            catch (Exception ex) { ReportFailure(ex); }
            finally
            {
                _busy = false;
                // Progress<T> posts its reports, so the last one can land after
                // this; queue the reset behind them
                Dispatcher.UIThread.Post(() => progressBar.Value = 0, DispatcherPriority.Background);
                RefreshSetupStatus();
                RefreshUpdateButton();
            }
        }

        private void SetStatus(string text) => setupStatus.SetText(text.ToUpperInvariant());

        private static string LogPath => Path.Combine(LauncherState.HomeDir, "nutstaller.log");

        // The status line is one short strip and long paths get ellipsized out of it,
        // so failures also go to a dialog (selectable, Ctrl+C copies it) and a log file
        // next to the exe. Without this an error naming a folder is unreadable.
        private void ReportFailure(Exception ex)
        {
            string firstLine = ex.Message.Split('\n')[0].Trim();
            SetStatus("ERROR: " + firstLine + " (SEE NUTSTALLER.LOG)");

            try
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            _ = MessageDialog.Show(this, ex.Message, "NutStaller - something went wrong");
        }

        private async Task DoDownloadRenut()
        {
            _state.InstallDir = installBox.Value.Trim();
            SetStatus("Looking up the latest reNut release...");
            var asset = await GithubDownloader.GetLatestAssetAsync("masterspike52/reNut", RenutAssetRank);

            SetStatus($"Downloading {asset.Name} ({asset.Tag})...");
            string download = Path.Combine(Path.GetTempPath(), asset.Name);
            await GithubDownloader.DownloadAsync(asset, download, new Progress<double>(v => progressBar.Value = v));

            if (OperatingSystem.IsWindows())
            {
                SetStatus("Unpacking reNut...");
                GithubDownloader.ExtractZip(download, _state.InstallDir);
                GithubDownloader.FlattenSingleFolder(_state.InstallDir);
                File.Delete(download);
            }
            else
            {
                // the AppImage is the whole game; renamed so the launcher finds it
                // whatever the release called it
                Directory.CreateDirectory(_state.InstallDir);
                File.Move(download, _state.RenutExe, overwrite: true);
                MakeExecutable(_state.RenutExe);
            }

            _state.WriteRenutCfg();
            _state.RenutVersion = asset.Tag;
            _state.Save();
            SetStatus($"reNut {asset.Tag} installed.");
        }

        private async Task DoDownloadXiso()
        {
            _state.InstallDir = installBox.Value.Trim();
            SetStatus("Looking up the latest extract-xiso build...");
            var asset = await GithubDownloader.GetLatestAssetAsync("XboxDev/extract-xiso",
                n => !OperatingSystem.IsWindows()
                        ? (n.Contains(OperatingSystem.IsMacOS() ? "macOS" : "Linux", StringComparison.OrdinalIgnoreCase) ? 1 : -1)
                   : n.Contains("Win64_Release", StringComparison.OrdinalIgnoreCase) ? 2
                   : n.Contains("Win32_Release", StringComparison.OrdinalIgnoreCase) ? 1 : -1);

            SetStatus($"Downloading {asset.Name}...");
            string zip = Path.Combine(Path.GetTempPath(), asset.Name);
            await GithubDownloader.DownloadAsync(asset, zip, new Progress<double>(v => progressBar.Value = v));

            SetStatus("Unpacking extract-xiso...");
            string tmp = Path.Combine(Path.GetTempPath(), "nutstaller_xiso");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            GithubDownloader.ExtractZip(zip, tmp);
            File.Delete(zip);

            string exeName = Path.GetFileName(_state.XisoExe);
            string? exe = Directory.EnumerateFiles(tmp, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).Equals(exeName, StringComparison.OrdinalIgnoreCase));
            if (exe == null) throw new InvalidOperationException($"{exeName} not found in the release zip.");

            Directory.CreateDirectory(_state.ToolsDir);
            File.Copy(exe, _state.XisoExe, overwrite: true);
            MakeExecutable(_state.XisoExe);
            Directory.Delete(tmp, true);

            _state.XisoVersion = asset.Tag;
            _state.Save();
            SetStatus("extract-xiso ready.");
        }

        private async Task DoPickIsoAndExtract()
        {
            _state.InstallDir = installBox.Value.Trim();
            if (!_state.XisoInstalled)
                throw new InvalidOperationException("get extract-xiso first (step 2).");

            // an iso already dropped into the install/game folder wins over the picker
            string? iso = FindLocalIso();
            if (iso != null)
            {
                SetStatus($"Found {Path.GetFileName(iso)} - using it.");
            }
            else
            {
                var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Pick your Banjo-Kazooie: Nuts & Bolts (US) ISO",
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Xbox 360 ISO") { Patterns = new[] { "*.iso", "*.ISO" } },
                        FilePickerFileTypes.All,
                    },
                });
                iso = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
                if (iso == null) return;
            }
            Directory.CreateDirectory(_state.GameDataDir);

            // extract-xiso has no overwrite switch (see its -h): it aborts with
            // "read error: File exists" the moment it meets a file from an earlier
            // run, so a half-finished extraction would make every retry fail until
            // the folder is cleared by hand. Do it for them.
            if (HasExtractedData(_state.GameDataDir, iso))
            {
                bool replace = await MessageDialog.Show(this,
                    $"{_state.GameDataDir}{Environment.NewLine}{Environment.NewLine}" +
                    "already holds game data, and extract-xiso cannot write over it." +
                    $"{Environment.NewLine}{Environment.NewLine}Replace it and extract again?",
                    "Replace existing game data?", yesNo: true);
                if (!replace)
                {
                    SetStatus("Extraction cancelled - existing game data was kept.");
                    return;
                }

                SetStatus("Clearing the previous extraction...");
                await Task.Run(() => ClearExtractTarget(_state.GameDataDir, iso));
            }

            SetStatus("Extracting ISO...");
            long isoSize = Math.Max(1, new FileInfo(iso).Length);
            var psi = new ProcessStartInfo
            {
                FileName = _state.XisoExe,
                ArgumentList = { "-x", "-d", _state.GameDataDir, iso },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi)!;
            // drain both pipes so extract-xiso can't block on a full buffer
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            // extract-xiso prints no percentage, so track bytes landing on disk
            // against the iso size instead. The scan runs off the UI thread and
            // backs off proportionally to how long it takes, so a slow disk is
            // not spending most of its seeks answering us instead of extracting.
            long scanMs = 0;
            while (!proc.HasExited)
            {
                await Task.Delay((int)Math.Clamp(scanMs * 4, 500, 5000));
                if (proc.HasExited) break;

                long t0 = Stopwatch.GetTimestamp();
                long done = await Task.Run(() => DirectorySize(_state.GameDataDir));
                scanMs = (Stopwatch.GetTimestamp() - t0) * 1000 / Stopwatch.Frequency;

                progressBar.Value = Math.Min(0.99, (double)done / isoSize);
                SetStatus($"Extracting ISO... {done / (1024 * 1024)} / {isoSize / (1024 * 1024)} MB");
            }
            await proc.WaitForExitAsync();
            string stderr = (await stderrTask).Trim();
            string stdout = (await stdoutTask).Trim();
            if (proc.ExitCode != 0)
            {
                // extract-xiso reports some failures on stdout, so fall back to it
                string detail = stderr.Length > 0 ? stderr : LastLines(stdout, 6);

                // extract-xiso says "read error: File exists" for anything it cannot
                // parse, which is the same thing it says about a genuine leftover
                // file, so spell out the likelier cause rather than leaving the user
                // with a message that points at the wrong problem.
                string hint = LooksLikeXboxIso(iso) ? "" :
                    Environment.NewLine + Environment.NewLine +
                    "This file does not look like a complete Xbox disc image. If it is " +
                    "still being copied, or the copy was interrupted partway, let it " +
                    "finish and try again.";

                throw new InvalidOperationException(
                    $"extract-xiso failed (exit code {proc.ExitCode})." +
                    $"{Environment.NewLine}{Environment.NewLine}{detail}{hint}" +
                    $"{Environment.NewLine}{Environment.NewLine}ISO: {iso}" +
                    $"{Environment.NewLine}Size: {new FileInfo(iso).Length / (1024 * 1024)} MB" +
                    $"{Environment.NewLine}Target folder: {_state.GameDataDir}");
            }
            progressBar.Value = 1;

            _state.WriteRenutCfg();
            _state.Save();
            SetStatus("Game data extracted and paths written. You are good to go!");
        }

        // The title update (a LIVE package, or a zip with one): its files into
        // game/update, default.xexp next to default.xex, update_data_root set.
        private async Task DoAddTitleUpdate()
        {
            _state.InstallDir = installBox.Value.Trim();
            if (!_state.GameExtracted)
                throw new InvalidOperationException("extract the game first (step 3).");
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Pick the title update (the TU_... file, or a zip with it; cancel to skip)",
            });
            string? path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (path == null)
            {
                SetStatus(_state.UpdateInstalled ? "Title update unchanged." : "No title update added; you can add it later (step 4).");
                return;
            }

            SetStatus("Unpacking the title update...");
            string name = await Task.Run(() => TitleUpdate.Install(path, _state.GameDataDir, _state.UpdateDataDir));
            _state.WriteRenutCfg();
            SetStatus($"{name} installed.");
        }

        // Every disc layout puts the volume descriptor one sector into its data
        // partition, so the magic lands at the partition base plus 0x10000.
        // (the partition bases are the ones extract-xiso itself probes)
        private static readonly long[] XisoMagicOffsets =
        {
            0x00000000 + 0x10000, // plain xiso, no video partition
            0x02080000 + 0x10000, // XGD3
            0x0FD90000 + 0x10000, // XGD2, which Nuts & Bolts is
            0x18300000 + 0x10000, // XGD1
            0x4A980000 + 0x10000, // XGD3, redump style
        };

        // Advisory only: a false negative must never block an extraction, it just
        // adds a hint to an error extract-xiso already raised.
        private static bool LooksLikeXboxIso(string path)
        {
            const string magic = "MICROSOFT*XBOX*MEDIA";
            try
            {
                using var fs = File.OpenRead(path);
                var buf = new byte[magic.Length];
                foreach (long offset in XisoMagicOffsets)
                {
                    if (offset + buf.Length > fs.Length) continue;
                    fs.Position = offset;
                    if (fs.ReadAtLeast(buf, buf.Length, throwOnEndOfStream: false) != buf.Length) continue;
                    if (System.Text.Encoding.ASCII.GetString(buf) == magic) return true;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return false;
        }

        // Anything in the extract target other than the source iso itself, which the
        // user may well have dropped into this very folder.
        private static bool HasExtractedData(string dir, string isoPath) =>
            Directory.Exists(dir) &&
            Directory.EnumerateFileSystemEntries(dir).Any(e => !SamePath(e, isoPath));

        private static bool SamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; }
        }

        // Empties the extract target, carefully stepping around the source iso so a
        // re-extract never deletes the user's own disc image.
        private static void ClearExtractTarget(string dir, string isoPath)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir).ToList())
            {
                if (SamePath(entry, isoPath)) continue;
                try
                {
                    if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                    else File.Delete(entry);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        // Total bytes currently on disk under dir. Enumerated through DirectoryInfo
        // so each size comes from the directory entry itself rather than a separate
        // stat call per file, and with IgnoreInaccessible so folders that appear or
        // vanish underneath us (extract-xiso is writing into this tree) do not abort
        // the whole walk and make progress lurch backwards.
        private static long DirectorySize(string dir)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };

            long total = 0;
            try
            {
                foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", options))
                {
                    try { total += f.Length; }
                    catch (IOException) { }
                }
            }
            catch (DirectoryNotFoundException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            return total;
        }

        // last n non-empty lines, for surfacing the tail of a tool's output
        private static string LastLines(string text, int n)
        {
            var lines = text.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.Trim().Length > 0)
                .ToArray();
            return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - n)));
        }

        // looks for a .iso sitting in the install folder or the game data folder;
        // largest one wins (the real game iso dwarfs anything else)
        private string? FindLocalIso()
        {
            var dirs = new[] { _state.InstallDir, _state.GameDataDir };
            string? best = null;
            long bestSize = -1;
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir, "*.iso", SearchOption.TopDirectoryOnly))
                {
                    long size = new FileInfo(f).Length;
                    if (size > bestSize) { bestSize = size; best = f; }
                }
            }
            return best;
        }

        private void LaunchGame()
        {
            if (!_state.RenutInstalled)
            {
                ShowPage(pageSetup, navSetup);
                SetStatus("reNut is not installed yet. Run setup first.");
                return;
            }
            try
            {
                // started directly, not through the shell: on Linux UseShellExecute
                // would hand the AppImage to xdg-open instead of running it
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = _state.RenutExe,
                    WorkingDirectory = _state.InstallDir,
                    UseShellExecute = false,
                })!;
                _ = WatchForEarlyExit(proc);
            }
            catch (Exception ex) { ReportFailure(ex); }
        }

        // A game that dies within seconds never showed a window, so the user sees
        // nothing at all. Its output is not redirected (the pipes would break, and
        // take the game down, once the launcher closes), so point at a terminal.
        // On Linux the usual cause is a glibc older than the reNut build wants.
        private async Task WatchForEarlyExit(Process proc)
        {
            using (proc)
            {
                await Task.WhenAny(proc.WaitForExitAsync(), Task.Delay(TimeSpan.FromSeconds(10)));
                if (!proc.HasExited || proc.ExitCode == 0) return;

                string glibc = OperatingSystem.IsLinux() && GlibcVersion() is { } v
                    ? $"{Environment.NewLine}{Environment.NewLine}This system has glibc {v}. If running it " +
                      "from a terminal shows \"GLIBC_x.yy not found\", this reNut build needs a newer " +
                      "Linux distribution."
                    : "";
                ReportFailure(new InvalidOperationException(
                    $"reNut quit right after starting (exit code {proc.ExitCode}). Run it from a terminal " +
                    $"to see why:{Environment.NewLine}{Environment.NewLine}{_state.RenutExe}{glibc}"));
            }
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "gnu_get_libc_version")]
        private static extern IntPtr GnuGetLibcVersion();

        private static string? GlibcVersion()
        {
            try { return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(GnuGetLibcVersion()); }
            catch (Exception) { return null; } // musl and friends have no such call
        }

        // ------------------------------------------------------------- keybinds

        // reNut's keyboard binds (game/keyboard_input.cpp) and their defaults.
        private static readonly (string Key, string Label, string Default)[] PadKeys =
        {
            ("renut_key_forward", "FORWARD", "W"), ("renut_key_back", "BACK", "S"),
            ("renut_key_left", "LEFT", "A"), ("renut_key_right", "RIGHT", "D"),
            ("renut_key_walk", "WALK", "Control"), ("renut_key_a", "A / JUMP", "Space"),
            ("renut_key_b", "B", "RMB"), ("renut_key_x", "X", "LMB"),
            ("renut_key_y", "Y", "E"), ("renut_key_lb", "LB", "Q"),
            ("renut_key_rb", "RB", "R"), ("renut_key_lt", "LT", "Shift"),
            ("renut_key_rt", "RT", "F"), ("renut_key_start", "START", "Escape"),
            ("renut_key_back_button", "BACK BTN", "Tab"), ("renut_key_ls", "L STICK", "C"),
            ("renut_key_rs", "R STICK", "V"), ("renut_key_dpad_up", "DPAD UP", "Up"),
            ("renut_key_dpad_down", "DPAD DOWN", "Down"), ("renut_key_dpad_left", "DPAD LEFT", "Left"),
            ("renut_key_dpad_right", "DPAD RIGHT", "Right"),
        };

        // Three columns of eight rows under the toggles.
        private void KeyRow(string key, string label, int slot)
        {
            if (slot >= 24) return;  // ponytail: room for 24 binds; a scroll list if reNut ever has more
            double x = 16 + slot / 8 * 176, y = 116 + slot % 8 * 28;
            Place(pageKeybinds, new BkLabel { Text = label, FontSizePx = 13, Dim = true }, x, y, 92, 24);
            _binds[key] = Place(pageKeybinds, new BkKeyField { FontSizePx = 14 }, x + 92, y, 78, 24);
        }

        private void Toggle(Panel parent, string key, string label, double x)
        {
            var b = Place(parent, new BkButton { FontSizePx = 16 }, x, 44, 166, 28);
            b.Click += (s, e) => { _toggleValues[key] = !_toggleValues[key]; ShowToggle(key); };
            _toggles[key] = (b, label);
        }

        private void ShowToggle(string key) =>
            _toggles[key].Button.Text = $"{_toggles[key].Label}: {(_toggleValues[key] ? "On" : "Off")}";

        private void LoadKeybindsIntoUi()
        {
            var toml = new TomlConfig(_state.RenutToml);
            foreach (var key in _toggles.Keys)
            {
                _toggleValues[key] = toml.GetBool(key, false);
                ShowToggle(key);
            }
            mouseSpeedBox.Value = toml.GetNumber("renut_mouse_sensitivity", 1.0)
                .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            foreach (var (key, _, def) in PadKeys)
                _binds[key].KeyName = toml.Has(key) ? toml.GetString(key) : def;
            // reNut's own hotkeys (bind_*: log overlay, settings...) after the pad keys
            foreach (var key in toml.Keys.Where(k => k.StartsWith("bind_", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (!_binds.ContainsKey(key))
                    KeyRow(key, key[5..].Replace("renut_", "").Replace('_', ' ').ToUpperInvariant(), _binds.Count);
                if (_binds.TryGetValue(key, out var field)) field.KeyName = toml.GetString(key);
            }
        }

        private void SaveKeybindsButton_Click()
        {
            try
            {
                var toml = new TomlConfig(_state.RenutToml);
                foreach (var (key, field) in _binds)
                    if (field.KeyName.Trim().Length > 0)
                        toml.SetString(key, field.KeyName.Trim());
                foreach (var (key, on) in _toggleValues)
                    toml.SetBool(key, on);
                if (double.TryParse(mouseSpeedBox.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double speed))
                    toml.SetNumber("renut_mouse_sensitivity", Math.Clamp(speed, 0.05, 10));
                Directory.CreateDirectory(Path.GetDirectoryName(_state.RenutToml)!);
                toml.Save();
                keybindsStatus.SetText("SAVED - RESTART RENUT IF IT IS RUNNING");
            }
            catch (Exception ex)
            {
                keybindsStatus.SetText("ERROR: " + ex.Message.ToUpperInvariant());
            }
        }
    }
}
