using System.Buffers.Binary;
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
                    f.Name.Length > StampLength + 4 ? f.Name[(StampLength + 1)..^4].Replace('-', ' ') : ""))
                .OrderByDescending(b => b.Made).ToList();
        }

        // "save-yyyyMMdd-HHmmss"; a name or note follows it after a dash.
        private const int StampLength = 20;

        // Gives a backup a name (empty clears it). Keeps letters, digits, spaces,
        // - and _, so the name stays a valid file name everywhere.
        public static string Rename(string file, string name)
        {
            string clean = new string(name.Trim().Where(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray())
                .Replace(' ', '-').Trim('-');
            if (clean.Length > 60) clean = clean[..60];
            string stamp = Path.GetFileName(file)[..StampLength];
            string target = Path.Combine(Path.GetDirectoryName(file)!, stamp + (clean.Length > 0 ? "-" + clean : "") + ".zip");
            if (target != file) File.Move(file, target);
            return target;
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

        // The folder with 00000001/ (the content; Headers/ is optional: reNut
        // names content without a header after its folder).
        private static string? FindSaveRoot(string dir)
        {
            foreach (var d in new[] { dir }.Concat(Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)))
                if (Directory.Exists(Path.Combine(d, "00000001")) &&
                    Directory.EnumerateDirectories(Path.Combine(d, "00000001")).Any())
                    return d;
            return null;
        }

        // A save someone else made, as reNut keeps saves, in a new folder `into`:
        // - the 4D5307ED folder (Xenia, reNut, an older backup), or a folder that
        //   holds it, or a zip of either; missing headers are written for vehicles
        // - an Xbox 360's save packages (STFS "CON" files, one per save, vehicle
        //   or replay), alone, in a folder or in a zip.
        // The temporary folder it unzipped to is deleted by the caller (`into`
        // and into + "-zip").
        public static void Normalize(string source, string into)
        {
            string from = source;
            if (File.Exists(source) && !Stfs.IsPackage(source))
            {
                from = into + "-zip";
                if (Directory.Exists(from)) Directory.Delete(from, true);
                ZipFile.ExtractToDirectory(source, from);  // refuses entries outside the folder
            }
            if (Directory.Exists(into)) Directory.Delete(into, true);
            if (Directory.Exists(from) && FindSaveRoot(from) is { } root)
            {
                CopyDirectory(root, into);
                WriteMissingHeaders(into);
                return;
            }
            var packages = (File.Exists(from) ? new[] { from }
                    : Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories).Where(Stfs.IsPackage))
                .Select(Stfs.Read).Where(p => p is { TitleId: TitleId, ContentType: 1 }).ToList();
            if (packages.Count == 0)
                throw new InvalidDataException("That is not a Nuts & Bolts save: no 00000001 folder or Xbox 360 save packages in it.");
            foreach (var p in packages)
            {
                string folder = Path.Combine(into, "00000001", p!.FileName);
                foreach (var (path, data) in p.Files)
                {
                    string target = Path.GetFullPath(Path.Combine(folder, path));
                    if (!target.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar))
                        throw new InvalidDataException($"{p.FileName} has a file outside its folder.");
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllBytes(target, data);
                }
                WriteHeader(into, p.FileName, p.DisplayName);
            }
        }

        private const uint TitleId = 0x4D5307ED;

        public static void WriteHeader(string root, string fileName, string displayName)
        {
            var header = new byte[0x148];  // XCONTENT_AGGREGATE_DATA
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x00), 1);  // device: hard drive
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x04), 1);  // content type: save game
            if (displayName.Length > 127) displayName = displayName[..127];
            Encoding.BigEndianUnicode.GetBytes(displayName).CopyTo(header, 0x08);
            var name = Encoding.ASCII.GetBytes(fileName);
            name.AsSpan(0, Math.Min(name.Length, 41)).CopyTo(header.AsSpan(0x108));
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x140), TitleId);
            string dir = Path.Combine(root, "Headers", "00000001");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, fileName + ".header"), header);
        }

        // Content without a header that holds a blueprint gets "VEHICLE: <name>";
        // the rest keep reNut's fallback (the folder name).
        private static void WriteMissingHeaders(string root)
        {
            foreach (var folder in Directory.GetDirectories(Path.Combine(root, "00000001")))
            {
                string id = Path.GetFileName(folder);
                if (File.Exists(Path.Combine(root, "Headers", "00000001", id + ".header"))) continue;
                foreach (var f in Directory.GetFiles(folder))
                    if (BlueprintName(File.ReadAllBytes(f)) is { } name)
                    {
                        WriteHeader(root, id, "VEHICLE: " + name);
                        break;
                    }
            }
        }

        // The vehicle name in a blueprint (0x28, 32 UTF-16BE chars), or null.
        public static string? BlueprintName(byte[] data) =>
            Workshop.IsBlueprint(data, out _) ? Encoding.BigEndianUnicode.GetString(data, 0x28, 64).Split('\0')[0].Trim() : null;

        public record Vehicle(string Name, byte[] Blueprint);

        // The vehicles in a save from anywhere (see Normalize).
        public static List<Vehicle> VehiclesIn(string source)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "nutstaller-vehicles-" + Environment.ProcessId);
            try
            {
                Normalize(source, tmp);
                var list = new List<Vehicle>();
                foreach (var folder in Directory.GetDirectories(Path.Combine(tmp, "00000001")).OrderBy(d => d))
                    foreach (var f in Directory.GetFiles(folder))
                    {
                        byte[] data = File.ReadAllBytes(f);
                        if (BlueprintName(data) is { } name)
                        {
                            list.Add(new Vehicle(name.Length > 0 ? name : Path.GetFileName(folder), data));
                            break;
                        }
                    }
                return list;
            }
            finally
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                if (Directory.Exists(tmp + "-zip")) Directory.Delete(tmp + "-zip", true);
            }
        }

        // Adds them to the save as new vehicles; the count.
        public static int ImportVehicles(string saveDir, IEnumerable<Vehicle> vehicles)
        {
            int n = 0;
            foreach (var v in vehicles)
            {
                Workshop.Install(saveDir, v.Blueprint, v.Name);
                n++;
            }
            return n;
        }

        // Replaces the save with a backup or an imported one, after backing up
        // the current save (when there is one). Returns that backup, or null.
        public static string? Replace(string saveDir, string source, string note)
        {
            if (RenutRunning())
                throw new InvalidOperationException("Close reNut first: it would write over the save on exit.");
            string tmp = saveDir + ".nutstaller-tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(saveDir)!);
                Normalize(source, tmp);

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
                if (Directory.Exists(tmp + "-zip")) Directory.Delete(tmp + "-zip", true);
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
