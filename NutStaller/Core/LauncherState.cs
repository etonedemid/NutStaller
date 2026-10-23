using System.Text;

namespace NutStaller.Core
{
    // Where everything lives on disk, plus the tiny launcher ini.
    internal class LauncherState
    {
        public string InstallDir { get; set; }
        public string RenutVersion { get; set; } = "";
        public string XisoVersion { get; set; } = "";

        // Windows gets a zip with renut.exe, Linux a single AppImage
        public string RenutExe => Path.Combine(InstallDir, OperatingSystem.IsWindows() ? "renut.exe" : "renut.AppImage");
        // renut.exe reads these next to itself; the Linux build reads them from
        // its user data dir (checked with strace, it never looks beside the AppImage)
        private string ConfigDir => OperatingSystem.IsWindows() ? InstallDir : UserDataDir;
        public string RenutToml => Path.Combine(ConfigDir, "renut.toml");
        public string RenutCfg => Path.Combine(ConfigDir, "renut.cfg");
        public string GameDataDir => Path.Combine(InstallDir, "game");
        // the title update's files (TitleUpdate.cs); its default.xexp also sits in game/
        public string UpdateDataDir => Path.Combine(GameDataDir, "update");
        // Documents on Windows, $XDG_DATA_HOME (~/.local/share) elsewhere. DoNotVerify:
        // by default .NET answers "" for a folder that does not exist yet (a fresh
        // home has no ~/.local/share), which would put renut/ in the working dir
        public string UserDataDir => Path.Combine(Environment.GetFolderPath(OperatingSystem.IsWindows()
            ? Environment.SpecialFolder.MyDocuments : Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify), "renut");
        public string ToolsDir => Path.Combine(InstallDir, "tools");
        public string XisoExe => Path.Combine(ToolsDir, OperatingSystem.IsWindows() ? "extract-xiso.exe" : "extract-xiso");

        public bool RenutInstalled => File.Exists(RenutExe);
        public bool XisoInstalled => File.Exists(XisoExe);
        public bool GameExtracted => Directory.Exists(GameDataDir) &&
                                     Directory.EnumerateFileSystemEntries(GameDataDir).Any();
        public bool UpdateInstalled => File.Exists(Path.Combine(UpdateDataDir, "default.xexp"));

        // Where the launcher keeps its own files: beside the exe, or beside the
        // .AppImage when run as one (its mount is read-only)
        public static string HomeDir =>
            Path.GetDirectoryName(Environment.GetEnvironmentVariable("APPIMAGE") ?? "") is { Length: > 0 } dir
                ? dir : AppContext.BaseDirectory;

        private static string IniPath => Path.Combine(HomeDir, "nutstaller.ini");

        private LauncherState(string installDir) => InstallDir = installDir;

        public static LauncherState Load()
        {
            var state = new LauncherState(Path.Combine(HomeDir, "reNut"));
            if (File.Exists(IniPath))
            {
                foreach (var line in File.ReadAllLines(IniPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line[..eq].Trim();
                    string v = line[(eq + 1)..].Trim().Trim('"');
                    if (key == "install_dir" && v.Length > 0) state.InstallDir = v;
                    else if (key == "renut_version") state.RenutVersion = v;
                    else if (key == "xiso_version") state.XisoVersion = v;
                }
            }
            return state;
        }

        public void Save()
        {
            File.WriteAllText(IniPath,
                $"install_dir = \"{InstallDir}\"\n" +
                $"renut_version = \"{RenutVersion}\"\n" +
                $"xiso_version = \"{XisoVersion}\"\n");
        }

        // renut reads its paths from renut.cfg next to the exe; writing it here
        // means the in-game path wizard never needs to show up.
        public void WriteRenutCfg()
        {
            Directory.CreateDirectory(UserDataDir);
            var sb = new StringBuilder();
            sb.Append("# renut path configuration\n");
            sb.Append($"game_data_root   = \"{GameDataDir}\"\n");
            sb.Append($"user_data_root   = \"{UserDataDir}\"\n");
            sb.Append($"update_data_root = \"{(UpdateInstalled ? UpdateDataDir : "")}\"\n");
            File.WriteAllText(RenutCfg, sb.ToString());
        }
    }
}
