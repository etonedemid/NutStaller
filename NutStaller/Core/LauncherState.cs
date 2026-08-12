using System.Text;

namespace NutStaller.Core
{
    // Where everything lives on disk, plus the tiny launcher ini.
    internal class LauncherState
    {
        public string InstallDir { get; set; }
        public string RenutVersion { get; set; } = "";
        public string XisoVersion { get; set; } = "";

        public string RenutExe => Path.Combine(InstallDir, "renut.exe");
        public string RenutToml => Path.Combine(InstallDir, "renut.toml");
        public string RenutCfg => Path.Combine(InstallDir, "renut.cfg");
        public string GameDataDir => Path.Combine(InstallDir, "game");
        public string UserDataDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "renut");
        public string ToolsDir => Path.Combine(InstallDir, "tools");
        public string XisoExe => Path.Combine(ToolsDir, "extract-xiso.exe");

        public bool RenutInstalled => File.Exists(RenutExe);
        public bool XisoInstalled => File.Exists(XisoExe);
        public bool GameExtracted => Directory.Exists(GameDataDir) &&
                                     Directory.EnumerateFileSystemEntries(GameDataDir).Any();

        private static string IniPath => Path.Combine(AppContext.BaseDirectory, "nutstaller.ini");

        private LauncherState(string installDir) => InstallDir = installDir;

        public static LauncherState Load()
        {
            var state = new LauncherState(Path.Combine(AppContext.BaseDirectory, "reNut"));
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
            sb.Append("update_data_root = \"\"\n");
            File.WriteAllText(RenutCfg, sb.ToString());
        }
    }
}
