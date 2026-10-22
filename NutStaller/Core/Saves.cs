using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace NutStaller.Core
{
    // The game's save folder (<user_data_root>/<xuid>/4D5307ED: progress saves,
    // vehicles and replays, each a content folder plus a header) and zipped
    // backups of it in <user_data_root>/nutstaller-backups.
    internal static class Saves
    {
        public record Summary(int Slots, int Vehicles, int Replays, long Bytes);

        public static Summary Summarize(string saveDir)
        {
            int slots = 0, vehicles = 0, replays = 0;
            string headers = Path.Combine(saveDir, "Headers", "00000001");
            if (Directory.Exists(headers))
                foreach (var h in Directory.GetFiles(headers, "*.header"))
                {
                    byte[] d = File.ReadAllBytes(h);
                    if (d.Length < 0x108) continue;
                    string name = Encoding.BigEndianUnicode.GetString(d, 8, 256).Split('\0')[0];
                    if (name.StartsWith("VEHICLE: ")) vehicles++;
                    else if (name.Contains(" - REPLAY - ")) replays++;
                    else slots++;
                }
            long bytes = Directory.Exists(saveDir)
                ? new DirectoryInfo(saveDir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            return new Summary(slots, vehicles, replays, bytes);
        }

        public static string BackupDir(string saveDir) => Path.GetFullPath(Path.Combine(saveDir, "..", "..", "nutstaller-backups"));

        public record Backup(string File, DateTime Made, long Bytes, string Note);

        public static List<Backup> Backups(string saveDir)
        {
            string dir = BackupDir(saveDir);
            if (!Directory.Exists(dir)) return new();
            return Directory.GetFiles(dir, "save-*.zip")
                .Select(f => new FileInfo(f))
                .Select(f => new Backup(f.FullName, f.LastWriteTime, f.Length,
                    f.Name.Contains("-before-") ? f.Name[(f.Name.IndexOf("-before-") + 1)..^4].Replace('-', ' ') : ""))
                .OrderByDescending(b => b.Made).ToList();
        }

        // note: why it was made ("before-restore", "before-import"), or "".
        public static string BackUp(string saveDir, string note = "")
        {
            if (!Directory.Exists(saveDir) || !Directory.EnumerateFileSystemEntries(saveDir).Any())
                throw new InvalidOperationException("There is no save to back up yet.");
            string dir = BackupDir(saveDir);
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"save-{DateTime.Now:yyyyMMdd-HHmmss}{(note.Length > 0 ? "-" + note : "")}.zip");
            ZipFile.CreateFromDirectory(saveDir, file, CompressionLevel.Optimal, includeBaseDirectory: false);
            return file;
        }

        // A save someone else made: the 4D5307ED folder itself, a folder that
        // holds it (as Xenia and reNut keep it), or a zip of either, such as a
        // backup. Returns the folder that has Headers/ and 00000001/.
        private static string? FindSaveRoot(string dir)
        {
            foreach (var d in new[] { dir }.Concat(Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)))
                if (Directory.Exists(Path.Combine(d, "Headers", "00000001")) && Directory.Exists(Path.Combine(d, "00000001")))
                    return d;
            return null;
        }

        // Replaces the save with a backup or an imported one, after backing up
        // the current save (when there is one). Returns that backup, or null.
        public static string? Replace(string saveDir, string source, string note)
        {
            if (RenutRunning())
                throw new InvalidOperationException("Close reNut first: it would write over the save on exit.");
            string tmp = saveDir + ".nutstaller-tmp";
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            string? unpacked = null;
            try
            {
                string from = source;
                if (File.Exists(source))
                {
                    unpacked = tmp + "-zip";
                    if (Directory.Exists(unpacked)) Directory.Delete(unpacked, true);
                    ZipFile.ExtractToDirectory(source, unpacked);  // refuses entries outside the folder
                    from = unpacked;
                }
                string root = FindSaveRoot(from) ?? throw new InvalidDataException(
                    "That is not a Nuts & Bolts save: no Headers/00000001 and 00000001 folders in it.");
                CopyDirectory(root, tmp);

                string? backup = Directory.Exists(saveDir) && Directory.EnumerateFileSystemEntries(saveDir).Any()
                    ? BackUp(saveDir, "before-" + note) : null;
                string old = saveDir + ".nutstaller-old";
                if (Directory.Exists(old)) Directory.Delete(old, true);
                if (Directory.Exists(saveDir)) Directory.Move(saveDir, old);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(saveDir)!);
                    Directory.Move(tmp, saveDir);
                }
                catch
                {
                    if (!Directory.Exists(saveDir) && Directory.Exists(old)) Directory.Move(old, saveDir);
                    throw;
                }
                if (Directory.Exists(old)) Directory.Delete(old, true);
                return backup;
            }
            finally
            {
                if (unpacked != null && Directory.Exists(unpacked)) Directory.Delete(unpacked, true);
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            }
        }

        private static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, f));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(f, target);
            }
        }

        public static bool RenutRunning() =>
            Process.GetProcesses().Any(p =>
            {
                try { return p.ProcessName.Equals("renut", StringComparison.OrdinalIgnoreCase); }
                catch (InvalidOperationException) { return false; }
            });
    }
}
