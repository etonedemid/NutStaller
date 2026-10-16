using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
        private readonly BkKeyField bindLogField = new() { KeyName = "F2" };
        private readonly BkLabel keybindsStatus = new() { Dim = true, FontSizePx = 16 };

        private readonly BkPagePanel pageCredits = new() { IsVisible = false };

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

            _binds["bind_renut_log_overlay"] = bindLogField;

            RefreshSetupStatus();

            _gamepad = new GamepadNavigator(this,
                candidates: PadCandidates,
                activate: ActivatePadControl,
                cyclePage: CyclePage,
                start: () => navPlay.PerformClick(),
                back: () => CurrentNav().Focus());
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

            Place(_root, navSetup, 402, 335, 185, 32).Click += (s, e) => NavSetup_Click();
            Place(_root, navUpdate, 402, 371, 185, 32).Click += async (s, e) => await NavUpdate_Click();
            Place(_root, navKeybinds, 402, 409, 185, 32).Click += (s, e) => NavKeybinds_Click();
            Place(_root, navCredits, 402, 445, 185, 32).Click += (s, e) => ShowPage(pageCredits, navCredits);
            Place(_root, navPlay, 402, 496, 185, 36).Click += (s, e) => LaunchGame();

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
            }));
            Place(p, renutStatus, 268, 58, 284, 32);
            Place(p, xisoStatus, 268, 98, 284, 32);
            Place(p, gameStatus, 268, 138, 284, 32);
            Label(p, "INSTALL FOLDER", 19, 230, 150, 30, dim: true);
            Place(p, installBox, 175, 228, 299, 30);
            Button(p, "...", 499, 230, 40, 30, () => _ = BrowseButton_Click(), 20);
            Place(p, progressBar, 19, 267, 523, 14);
            Place(p, setupStatus, 16, 284, 536, 36);

            p = Place(_root, pageKeybinds, 634, 279, 545, 382);
            Label(p, "KEYBINDS", 16, 14, 300, 30, 26);
            Label(p, "LOG OVERLAY", 16, 58, 220, 30, dim: true);
            Place(p, bindLogField, 246, 58, 140, 30);
            Label(p, "F4 OPENS THE IN-GAME CVAR MENU", 16, 106, 400, 24, 15, true);
            Label(p, "MORE BINDS SHOW UP HERE ONCE THE GAME WRITES THEM", 16, 132, 520, 24, 15, true);
            Button(p, "Save", 16, 336, 110, 32, SaveKeybindsButton_Click, 20);
            Place(p, keybindsStatus, 136, 336, 320, 32);

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
            foreach (var nav in new Control[] { navSetup, navUpdate, navKeybinds, navCredits, navPlay })
                yield return nav;
            var page = CurrentPage();
            foreach (var c in page.Children)
                if (c is BkButton or BkValueBox or BkKeyField)
                    yield return c;
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
            return pageSetup;
        }

        private BkButton CurrentNav()
        {
            if (pageKeybinds.IsVisible) return navKeybinds;
            if (pageCredits.IsVisible) return navCredits;
            return navSetup;
        }

        private void CyclePage(int delta)
        {
            var navs = new[] { navSetup, navKeybinds, navCredits };
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
            foreach (var p in new[] { pageSetup, pageKeybinds, pageCredits })
                p.IsVisible = p == page;
            foreach (var b in new[] { navSetup, navKeybinds, navCredits })
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
            gameStatus.SetText(_state.GameExtracted ? "GAME DATA FOUND" : "NO GAME DATA YET");
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

        private void LoadKeybindsIntoUi()
        {
            var toml = new TomlConfig(_state.RenutToml);

            // pick up any bind_* keys the game has persisted that we don't know about;
            // extra rows go below the notes so nothing overlaps
            int extra = _binds.Count - 1;
            foreach (var key in toml.Keys.Where(k => k.StartsWith("bind_", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (_binds.ContainsKey(key)) continue;
                int y = 170 + extra * 40;
                extra++;

                string label = key.Replace("bind_", "").Replace('_', ' ').ToUpperInvariant();
                var lbl = new BkLabel { Text = label, Dim = true, FontSizePx = 18 };
                var field = new BkKeyField { KeyName = toml.GetString(key) };
                Place(pageKeybinds, lbl, 16, y, 220, 30);
                Place(pageKeybinds, field, 246, y, 140, 30);
                _binds[key] = field;
            }

            foreach (var (key, field) in _binds)
                if (toml.Has(key)) field.KeyName = toml.GetString(key);
        }

        private void SaveKeybindsButton_Click()
        {
            try
            {
                var toml = new TomlConfig(_state.RenutToml);
                foreach (var (key, field) in _binds)
                    if (field.KeyName.Trim().Length > 0)
                        toml.SetString(key, field.KeyName.Trim());
                Directory.CreateDirectory(Path.GetDirectoryName(_state.RenutToml)!);
                toml.Save();
                keybindsStatus.SetText("SAVED");
            }
            catch (Exception ex)
            {
                keybindsStatus.SetText("ERROR: " + ex.Message.ToUpperInvariant());
            }
        }
    }
}
