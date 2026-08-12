using System.Diagnostics;
using System.Runtime.InteropServices;
using NutStaller.Core;
using NutStaller.Ui;

namespace NutStaller
{
    public partial class NutStallerMainWindow : Form
    {
        private const int DesignW = 1200;
        private const int DesignH = 675;

        private LauncherState _state = null!;
        private bool _busy;

        private readonly Dictionary<string, BkKeyField> _binds = new();

        // design-time bounds of every control, used to rescale the layout
        private readonly Dictionary<Control, Rectangle> _baseBounds = new();

        private GamepadNavigator? _gamepad;

        public NutStallerMainWindow()
        {
            InitializeComponent();
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            _state = LauncherState.Load();
            installBox.Value = _state.InstallDir;

            _binds["bind_renut_log_overlay"] = bindLogField;

            CaptureBaseBounds(this);
            ApplyScale();
            RefreshSetupStatus();

            _gamepad = new GamepadNavigator(this,
                candidates: PadCandidates,
                activate: ActivatePadControl,
                cyclePage: CyclePage,
                start: () => navPlay.PerformClick(),
                back: () => CurrentNav().Focus());
            FormClosed += (s, e) => _gamepad?.Dispose();

            _ = CheckForUpdatesAsync();
        }

        // ------------------------------------------------------------- updates

        private string? _latestTag;

        private static int RenutAssetRank(string n) =>
            n.Contains("win", StringComparison.OrdinalIgnoreCase) &&
            n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? 1 : -1;

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

        private async void NavUpdate_Click(object? sender, EventArgs e)
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
            foreach (Control c in page.Controls)
                if (c is BkButton or BkToggle or BkValueBox or BkKeyField)
                    yield return c;
        }

        private void ActivatePadControl(Control c)
        {
            switch (c)
            {
                case BkButton b: b.PerformClick(); break;
                case BkToggle t: t.Checked = !t.Checked; break;
                case BkValueBox v: v.BeginEdit(); break;
                case BkKeyField k: k.BeginListen(); break;
            }
        }

        private BkPagePanel CurrentPage()
        {
            if (pageKeybinds.Visible) return pageKeybinds;
            if (pageCredits.Visible) return pageCredits;
            return pageSetup;
        }

        private BkButton CurrentNav()
        {
            if (pageKeybinds.Visible) return navKeybinds;
            if (pageCredits.Visible) return navCredits;
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

        // ------------------------------------------------------------- scaling

        private void CaptureBaseBounds(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                _baseBounds[c] = c.Bounds;
                if (c is BkPagePanel) CaptureBaseBounds(c);
            }
        }

        private float ScaleFactor => Math.Min((float)ClientSize.Width / DesignW, (float)ClientSize.Height / DesignH);

        private void ApplyScale()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            float s = ScaleFactor;
            int ox = (int)((ClientSize.Width - DesignW * s) / 2);
            int oy = (int)((ClientSize.Height - DesignH * s) / 2);

            foreach (var (c, b) in _baseBounds)
            {
                bool topLevel = c.Parent == this;
                int x = (int)Math.Round(b.X * s) + (topLevel ? ox : 0);
                int y = (int)Math.Round(b.Y * s) + (topLevel ? oy : 0);
                c.SetBounds(x, y, (int)Math.Round(b.Width * s), (int)Math.Round(b.Height * s));
                if (c is IBkFontSize f)
                    c.Font = Assets.Andy(Math.Max(6f, f.FontSizePx * s));
            }
            Invalidate(true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_baseBounds.Count > 0) ApplyScale();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Assets.Blueprint))
                g.FillRectangle(b, ClientRectangle);

            var bg = Assets.Background;
            if (bg == null) return;
            float s = ScaleFactor;
            int w = (int)(DesignW * s), h = (int)(DesignH * s);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(bg, (ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
        }

        // keep the window 16:9 while the user drags an edge
        private const int WM_SIZING = 0x214;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SIZING)
            {
                var r = Marshal.PtrToStructure<RECT>(m.LParam);
                int chromeW = Width - ClientSize.Width;
                int chromeH = Height - ClientSize.Height;
                float ratio = (float)DesignW / DesignH;

                int edge = m.WParam.ToInt32();
                int clientW = (r.Right - r.Left) - chromeW;
                int clientH = (r.Bottom - r.Top) - chromeH;

                switch (edge)
                {
                    case 1: // left
                    case 2: // right
                        clientH = (int)Math.Round(clientW / ratio);
                        r.Bottom = r.Top + clientH + chromeH;
                        break;
                    case 3: // top
                    case 6: // bottom
                        clientW = (int)Math.Round(clientH * ratio);
                        r.Right = r.Left + clientW + chromeW;
                        break;
                    default: // corners: follow the width
                        clientH = (int)Math.Round(clientW / ratio);
                        if (edge == 4 || edge == 5) // top corners grow upward
                            r.Top = r.Bottom - clientH - chromeH;
                        else
                            r.Bottom = r.Top + clientH + chromeH;
                        break;
                }
                Marshal.StructureToPtr(r, m.LParam, true);
            }
            base.WndProc(ref m);
        }

        // ------------------------------------------------------------- nav

        private void NavSetup_Click(object? sender, EventArgs e)
        {
            ShowPage(pageSetup, navSetup);
            RefreshSetupStatus();
        }

        private void NavKeybinds_Click(object? sender, EventArgs e)
        {
            LoadKeybindsIntoUi();
            ShowPage(pageKeybinds, navKeybinds);
        }

        private void NavPlay_Click(object? sender, EventArgs e) => LaunchGame();

        private void NavCredits_Click(object? sender, EventArgs e) => ShowPage(pageCredits, navCredits);

        private void CreditsGithubButton_Click(object? sender, EventArgs e) =>
            Process.Start(new ProcessStartInfo("https://github.com/masterspike52/reNut") { UseShellExecute = true });

        private void CreditsDiscordButton_Click(object? sender, EventArgs e) =>
            Process.Start(new ProcessStartInfo("https://discord.gg/D5Bz2ZsHdY") { UseShellExecute = true });

        private void ShowPage(BkPagePanel page, BkButton nav)
        {
            foreach (var p in new[] { pageSetup, pageKeybinds, pageCredits })
                p.Visible = p == page;
            foreach (var b in new[] { navSetup, navKeybinds, navCredits })
            {
                b.Selected = b == nav;
                b.Invalidate();
            }
        }

        // ------------------------------------------------------------- setup

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Where should reNut live?",
                SelectedPath = Directory.Exists(_state.InstallDir) ? _state.InstallDir : AppContext.BaseDirectory,
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _state.InstallDir = dlg.SelectedPath;
                installBox.Value = _state.InstallDir;
                _state.Save();
                RefreshSetupStatus();
            }
        }

        private async void GetRenutButton_Click(object? sender, EventArgs e) => await RunBusy(DoDownloadRenut);
        private async void GetXisoButton_Click(object? sender, EventArgs e) => await RunBusy(DoDownloadXiso);
        private async void ExtractButton_Click(object? sender, EventArgs e) => await RunBusy(DoPickIsoAndExtract);

        private async void DoAllButton_Click(object? sender, EventArgs e)
        {
            await RunBusy(async () =>
            {
                await DoDownloadRenut();
                await DoDownloadXiso();
                await DoPickIsoAndExtract();
            });
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
            catch (Exception ex) { SetStatus("ERROR: " + ex.Message); }
            finally
            {
                _busy = false;
                progressBar.Value = 0;
                RefreshSetupStatus();
                RefreshUpdateButton();
            }
        }

        private void SetStatus(string text) => setupStatus.SetText(text.ToUpperInvariant());

        private async Task DoDownloadRenut()
        {
            _state.InstallDir = installBox.Value.Trim();
            SetStatus("Looking up the latest reNut release...");
            var asset = await GithubDownloader.GetLatestAssetAsync("masterspike52/reNut", RenutAssetRank);

            SetStatus($"Downloading {asset.Name} ({asset.Tag})...");
            string zip = Path.Combine(Path.GetTempPath(), asset.Name);
            await GithubDownloader.DownloadAsync(asset, zip, new Progress<double>(v => progressBar.Value = v));

            SetStatus("Unpacking reNut...");
            GithubDownloader.ExtractZip(zip, _state.InstallDir);
            GithubDownloader.FlattenSingleFolder(_state.InstallDir);
            File.Delete(zip);

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
                n => n.Contains("Win64_Release", StringComparison.OrdinalIgnoreCase) ? 2
                   : n.Contains("Win32_Release", StringComparison.OrdinalIgnoreCase) ? 1 : -1);

            SetStatus($"Downloading {asset.Name}...");
            string zip = Path.Combine(Path.GetTempPath(), asset.Name);
            await GithubDownloader.DownloadAsync(asset, zip, new Progress<double>(v => progressBar.Value = v));

            SetStatus("Unpacking extract-xiso...");
            string tmp = Path.Combine(Path.GetTempPath(), "nutstaller_xiso");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            GithubDownloader.ExtractZip(zip, tmp);
            File.Delete(zip);

            string? exe = Directory.EnumerateFiles(tmp, "*.exe", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).Contains("xiso", StringComparison.OrdinalIgnoreCase));
            if (exe == null) throw new InvalidOperationException("extract-xiso.exe not found in the release zip.");

            Directory.CreateDirectory(_state.ToolsDir);
            File.Copy(exe, _state.XisoExe, overwrite: true);
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
                using var dlg = new OpenFileDialog
                {
                    Title = "Pick your Banjo-Kazooie: Nuts & Bolts (US) ISO",
                    Filter = "Xbox 360 ISO (*.iso)|*.iso|All files (*.*)|*.*",
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                iso = dlg.FileName;
            }
            Directory.CreateDirectory(_state.GameDataDir);

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
            // against the iso size instead
            while (!proc.HasExited)
            {
                await Task.Delay(400);
                long done = DirectorySize(_state.GameDataDir);
                progressBar.Value = Math.Min(0.99, (double)done / isoSize);
                SetStatus($"Extracting ISO... {done / (1024 * 1024)} / {isoSize / (1024 * 1024)} MB");
            }
            await proc.WaitForExitAsync();
            string stderr = await stderrTask;
            await stdoutTask;
            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"extract-xiso failed ({proc.ExitCode}): {stderr.Trim()}");
            progressBar.Value = 1;

            _state.WriteRenutCfg();
            _state.Save();
            SetStatus("Game data extracted and paths written. You are good to go!");
        }

        // total bytes currently on disk under dir; tolerant of files still being written
        private static long DirectorySize(string dir)
        {
            long total = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; }
                    catch (IOException) { }
                }
            }
            catch (Exception) { }
            return total;
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
            Process.Start(new ProcessStartInfo
            {
                FileName = _state.RenutExe,
                WorkingDirectory = _state.InstallDir,
                UseShellExecute = true,
            });
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
                pageKeybinds.Controls.Add(lbl);
                pageKeybinds.Controls.Add(field);
                _binds[key] = field;

                _baseBounds[lbl] = new Rectangle(16, y, 220, 30);
                _baseBounds[field] = new Rectangle(246, y, 140, 30);
            }

            foreach (var (key, field) in _binds)
                if (toml.Has(key)) field.KeyName = toml.GetString(key);

            ApplyScale();
        }

        private void SaveKeybindsButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var toml = new TomlConfig(_state.RenutToml);
                foreach (var (key, field) in _binds)
                    if (field.KeyName.Trim().Length > 0)
                        toml.SetString(key, field.KeyName.Trim());
                Directory.CreateDirectory(_state.InstallDir);
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
