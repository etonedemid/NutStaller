using System.IO.Compression;

namespace NutStaller.Core
{
    // The game's title update: a LIVE package (content type 0x000B0000) with
    // default.xexp and the files it replaces. reNut takes the files from
    // update_data_root (game/update) and default.xexp next to default.xex too.
    internal static class TitleUpdate
    {
        private const uint NutsAndBolts = 0x4D5307ED, ContentTypeUpdate = 0x000B0000;

        // The package itself, or a zip holding one (as title updates are passed around).
        public static Stfs.Package Read(string source)
        {
            if (Stfs.IsPackage(source))
                return Check(Stfs.Read(source)) ?? throw new InvalidDataException(
                    "That is not a Nuts & Bolts title update.");

            ZipArchive zip;
            try { zip = ZipFile.OpenRead(source); }
            catch (InvalidDataException)
            {
                throw new InvalidDataException("That is not a title update (the TU_... file, or a zip with it).");
            }
            using var _ = zip;
            foreach (var entry in zip.Entries.Where(e => e.Length >= 0xA000))
            {
                string tmp = Path.GetTempFileName();
                try
                {
                    entry.ExtractToFile(tmp, overwrite: true);
                    if (Stfs.IsPackage(tmp) && Check(Stfs.Read(tmp)) is { } package)
                        return package;
                }
                finally { File.Delete(tmp); }
            }
            throw new InvalidDataException("No Nuts & Bolts title update in that zip.");
        }

        private static Stfs.Package? Check(Stfs.Package? p) =>
            p != null && p.TitleId == NutsAndBolts && p.ContentType == ContentTypeUpdate &&
            p.Files.Any(f => f.Path.Equals("default.xexp", StringComparison.OrdinalIgnoreCase))
                ? p : null;

        // Its files into updateDir (replacing what was there) and default.xexp
        // into gameDir. Returns the package's name ("... Title Update #3").
        public static string Install(string source, string gameDir, string updateDir)
        {
            var package = Read(source);
            string tmp = updateDir + ".new";
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            string root = Path.GetFullPath(tmp) + Path.DirectorySeparatorChar;
            foreach (var (path, data) in package.Files)
            {
                string target = Path.GetFullPath(Path.Combine(tmp, path));
                if (!target.StartsWith(root, StringComparison.Ordinal))
                    throw new InvalidDataException($"Bad path in the title update: {path}");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, data);
            }
            if (Directory.Exists(updateDir)) Directory.Delete(updateDir, true);
            Directory.Move(tmp, updateDir);

            var xexp = package.Files.First(f => f.Path.Equals("default.xexp", StringComparison.OrdinalIgnoreCase));
            File.WriteAllBytes(Path.Combine(gameDir, "default.xexp"), xexp.Data);
            return package.DisplayName;
        }
    }
}
