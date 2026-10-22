using System.Buffers.Binary;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace NutStaller.Core
{
    // The community blueprint workshop: a public GitHub repo (index.json plus
    // one folder per blueprint) that a bot fills from issue-form submissions,
    // and the game's save folder where blueprints are installed.
    internal static class Workshop
    {
        public const string Repo = "etonedemid/nutstaller-workshop";
        private const string Raw = "https://raw.githubusercontent.com/" + Repo + "/main/";
        public const string SubmitUrl = "https://github.com/" + Repo + "/issues/new?template=submit-blueprint.yml";
        public const string SubmitModUrl = "https://github.com/" + Repo + "/issues/new?template=submit-mod.yml";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient();
            c.DefaultRequestHeaders.UserAgent.ParseAdd("NutStaller/1.0");
            c.Timeout = TimeSpan.FromSeconds(30);
            return c;
        }

        // Requires: ids of workshop mods the vehicle needs; Missing: custom parts
        // ("part (mod)") from mods that are not in the workshop.
        public record Entry(string Id, string Name, string Description, string Author, int Parts,
            string Created, string BlueprintPath, string PreviewPath, List<string> Requires, List<string> Missing);

        public record ModEntry(string Id, string ModId, string Name, string Description, string Author,
            string Version, List<string> PartNames, long Size, string Sha256, string Download, string PreviewPath);

        public static async Task<(List<Entry>, List<ModEntry>)> FetchIndexAsync()
        {
            // raw.githubusercontent.com caches for a few minutes; the query
            // string gets a fresh copy when Refresh is pressed
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(
                Raw + "index.json?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            static string S(JsonElement e, string key) =>
                e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            static IEnumerable<JsonElement> A(JsonElement e, string key) =>
                e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : Enumerable.Empty<JsonElement>();
            var list = new List<Entry>();
            foreach (var b in A(doc.RootElement, "blueprints"))
                list.Add(new Entry(S(b, "id"), S(b, "name"), S(b, "description"), S(b, "author"),
                    b.TryGetProperty("parts", out var p) ? p.GetInt32() : 0,
                    S(b, "created"), S(b, "blueprint"), S(b, "preview"),
                    A(b, "requires").Select(r => r.GetString() ?? "").ToList(),
                    A(b, "missing").Select(m => $"{S(m, "id")} ({S(m, "mod")})").ToList()));
            var mods = new List<ModEntry>();
            foreach (var m in A(doc.RootElement, "mods"))
                mods.Add(new ModEntry(S(m, "id"), S(m, "mod_id"), S(m, "name"), S(m, "description"), S(m, "author"),
                    S(m, "version"), A(m, "parts").Select(p => S(p, "name")).ToList(),
                    m.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                    S(m, "sha256"), S(m, "download"), S(m, "preview")));
            return (list, mods);
        }

        public static Task<byte[]> DownloadAsync(string repoPath) => Http.GetByteArrayAsync(Raw + repoPath);

        // ------------------------------------------------------------ part mods

        // reNut's mods folder: renut_mods_folder (default "mods"), relative to
        // the folder reNut writes renut.toml to (app/app_paths.h).
        public static string ModsDir(LauncherState state)
        {
            string folder = new TomlConfig(state.RenutToml).GetString("renut_mods_folder", "mods");
            return Path.Combine(Path.GetDirectoryName(state.RenutToml)!, folder.Length > 0 ? folder : "mods");
        }

        // The mod id the workshop goes by: patch.json's Id, else its Name, as a slug.
        public static string ModSlug(string name) =>
            System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

        public record LocalMod(string ModId, string Name, string File, List<string> PartIds);

        // patch.json and the part ids of a .nbpatch.
        public static LocalMod ReadMod(string file)
        {
            using var zip = ZipFile.OpenRead(file);
            var manifest = zip.GetEntry("patch.json") ?? throw new InvalidDataException("That is not a reNut mod (no patch.json).");
            using var doc = JsonDocument.Parse(manifest.Open());
            string S(string key) => doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var ids = new List<string>();
            foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith("parts/") && e.FullName.EndsWith(".json")))
                try
                {
                    using var part = JsonDocument.Parse(e.Open());
                    if (part.RootElement.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } s) ids.Add(s);
                }
                catch (JsonException) { }
            string name = S("Name");
            return new LocalMod(ModSlug(S("Id") is { Length: > 0 } i ? i : name), name, file, ids);
        }

        public static List<LocalMod> LocalMods(string modsDir)
        {
            var list = new List<LocalMod>();
            if (!Directory.Exists(modsDir)) return list;
            foreach (var f in Directory.GetFiles(modsDir, "*.nbpatch"))
                try { list.Add(ReadMod(f)); }
                catch (Exception e) when (e is IOException or InvalidDataException or JsonException) { }
            return list;
        }

        // N&B's asset id, as reNut's mods::AssetId: type << 24 | ~CRC-32 & 0xFFFFFF.
        private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(i =>
        {
            uint c = (uint)i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            return c;
        }).ToArray();

        public static uint PartAid(string partId)
        {
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in Encoding.ASCII.GetBytes("banjox_vehicleblock_renut_" + partId))
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return 0x1Fu << 24 | (crc & 0xFFFFFF);  // crc here is the CRC before its final inversion
        }

        // Each part record holds its part's asset id at offset 8.
        public static HashSet<uint> BlueprintAids(byte[] blueprint)
        {
            var set = new HashSet<uint>();
            if (!IsBlueprint(blueprint, out int parts)) return set;
            for (int i = 0; i < parts; i++)
                set.Add(BinaryPrimitives.ReadUInt32BigEndian(blueprint.AsSpan(HeaderSize + i * PartSize + 8)));
            return set;
        }

        public record CustomPart(string Id, string Mod);

        // The custom parts in a vehicle, from every part reNut has seen
        // (mods/.renut_custom_parts.json) and the mods in the folder now.
        public static List<CustomPart> CustomPartsIn(byte[] blueprint, string modsDir)
        {
            var known = new Dictionary<uint, CustomPart>();
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(modsDir, ".renut_custom_parts.json")));
                foreach (var p in doc.RootElement.GetProperty("parts").EnumerateArray())
                    if (uint.TryParse(p.GetProperty("aid").GetString(), System.Globalization.NumberStyles.HexNumber, null, out uint aid))
                        known[aid] = new CustomPart(p.GetProperty("id").GetString() ?? "", p.GetProperty("mod").GetString() ?? "");
            }
            catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { }
            foreach (var mod in LocalMods(modsDir))
                foreach (var id in mod.PartIds)
                    known.TryAdd(PartAid(id), new CustomPart(id, mod.Name));
            var aids = BlueprintAids(blueprint);
            return known.Where(k => aids.Contains(k.Key)).Select(k => k.Value).OrderBy(c => c.Mod).ToList();
        }

        // Mods installed from the workshop: mods/<mod id>.nbpatch, tracked in
        // user_data_root/nutstaller-workshop-mods.json.
        public record InstalledMod(string Id, string ModId, string Name, string Author, string Description,
            int Parts, string PreviewPath, string File);

        private static string ModManifestPath(string saveDir) =>
            Path.Combine(saveDir, "..", "..", "nutstaller-workshop-mods.json");

        public static List<InstalledMod> LoadInstalledMods(string saveDir)
        {
            try
            {
                var all = JsonSerializer.Deserialize<List<InstalledMod>>(File.ReadAllText(ModManifestPath(saveDir)));
                return all?.Where(m => File.Exists(m.File)).ToList() ?? new();
            }
            catch (Exception e) when (e is IOException or JsonException) { return new(); }
        }

        public static async Task<InstalledMod> InstallModAsync(string saveDir, string modsDir, ModEntry m)
        {
            byte[] data = await Http.GetByteArrayAsync(m.Download);
            if (!Convert.ToHexString(SHA256.HashData(data)).Equals(m.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{m.Name} did not download correctly; try again.");
            Directory.CreateDirectory(modsDir);
            string file = Path.Combine(modsDir, m.ModId + ".nbpatch");
            File.WriteAllBytes(file, data);
            var item = new InstalledMod(m.Id, m.ModId, m.Name, m.Author, m.Description, m.PartNames.Count, m.PreviewPath, file);
            var list = LoadInstalledMods(saveDir);
            list.RemoveAll(i => i.ModId == m.ModId);
            list.Add(item);
            File.WriteAllText(ModManifestPath(saveDir), JsonSerializer.Serialize(list));
            return item;
        }

        // Only files NutStaller installed; vehicles keeping the mod's parts get
        // their base parts in reNut.
        public static void RemoveMod(string saveDir, string modId)
        {
            var list = LoadInstalledMods(saveDir);
            foreach (var m in list.Where(m => m.ModId == modId)) File.Delete(m.File);
            list.RemoveAll(m => m.ModId == modId);
            File.WriteAllText(ModManifestPath(saveDir), JsonSerializer.Serialize(list));
        }

        // ----------------------------------------------------------- the format

        // Big-endian: magic, u16 part count at 0x08, the name at 0x28 (32 UTF-16
        // chars), a 0x84-byte header, then 36 bytes per part.
        private static readonly byte[] Magic = Convert.FromHexString("3F9AE14840547AE1");
        private const int HeaderSize = 0x84, PartSize = 36;

        public static bool IsBlueprint(byte[] data, out int parts)
        {
            parts = 0;
            if (data.Length < HeaderSize || !data.AsSpan(0, 8).SequenceEqual(Magic)) return false;
            parts = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8));
            return parts >= 1 && data.Length == HeaderSize + parts * PartSize;
        }

        // ------------------------------------------------------------- the save

        public record LocalVehicle(string Name, string File);

        private const string TitleId = "4D5307ED";
        private const string ProfileXuid = "B13EBABEBABEBABE";  // the SDK's built-in profile
        private const int ContentHeaderSize = 0x148;            // XCONTENT_AGGREGATE_DATA

        // <user_data_root>/<xuid>/4D5307ED: user_data_root from renut.cfg when
        // it is there, else where the launcher points reNut.
        public static string SaveDir(LauncherState state)
        {
            string root = state.UserDataDir;
            if (File.Exists(state.RenutCfg))
                foreach (var line in File.ReadAllLines(state.RenutCfg))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0 && line[..eq].Trim() == "user_data_root")
                    {
                        string v = line[(eq + 1)..].Trim().Trim('"');
                        if (v.Length > 0) root = v;
                    }
                }
            return Path.Combine(root, ProfileXuid, TitleId);
        }

        private static string HeadersDir(string saveDir) => Path.Combine(saveDir, "Headers", "00000001");

        private static string DisplayName(byte[] header) =>
            Encoding.BigEndianUnicode.GetString(header, 8, 256).Split('\0')[0];

        public static List<LocalVehicle> LocalVehicles(string saveDir)
        {
            var list = new List<LocalVehicle>();
            if (!Directory.Exists(HeadersDir(saveDir))) return list;
            foreach (var path in Directory.GetFiles(HeadersDir(saveDir), "0x*.header"))
            {
                byte[] header = File.ReadAllBytes(path);
                if (header.Length < ContentHeaderSize) continue;
                string display = DisplayName(header);
                if (!display.StartsWith("VEHICLE: ")) continue;
                string id = Path.GetFileNameWithoutExtension(path);  // 0x0000000a
                string file = Path.Combine(saveDir, "00000001", id, id[2..]);
                if (File.Exists(file)) list.Add(new LocalVehicle(display["VEHICLE: ".Length..], file));
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        // Adds the blueprint to the save as a new vehicle: the next id after the
        // game's own (it numbers vehicles 0x00000001 up), a content folder and
        // its header. Returns the id used.
        public static string Install(string saveDir, byte[] blueprint, string name)
        {
            if (!IsBlueprint(blueprint, out _)) throw new InvalidDataException("Not a Nuts & Bolts blueprint.");
            Directory.CreateDirectory(HeadersDir(saveDir));
            uint next = 1;
            foreach (var path in Directory.GetFiles(HeadersDir(saveDir), "0x*.header"))
                if (uint.TryParse(Path.GetFileNameWithoutExtension(path)[2..],
                        System.Globalization.NumberStyles.HexNumber, null, out uint used) && used < 0x01000000)
                    next = Math.Max(next, used + 1);
            string id = $"0x{next:x8}";
            string folder = Path.Combine(saveDir, "00000001", id);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, id[2..]), blueprint);

            var header = new byte[ContentHeaderSize];
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x00), 1);   // device: hard drive
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x04), 1);   // content type: save game
            string display = "VEHICLE: " + name;
            if (display.Length > 127) display = display[..127];
            Encoding.BigEndianUnicode.GetBytes(display).CopyTo(header, 0x08);
            Encoding.ASCII.GetBytes(id).CopyTo(header, 0x108);              // file name
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x140), 0x4D5307ED);  // title id
            File.WriteAllBytes(Path.Combine(HeadersDir(saveDir), id + ".header"), header);
            return id;
        }

        // What came from the workshop, so the browser can offer Remove and the
        // Installed tab can list it: user_data_root/nutstaller-workshop.json,
        // outside the title folder so the game never sees it.
        public record Installed(string Id, string ContentId, string Name, string Author,
            string Description, int Parts, string PreviewPath);

        private static string ManifestPath(string saveDir) =>
            Path.Combine(saveDir, "..", "..", "nutstaller-workshop.json");

        private static string ContentFile(string saveDir, string contentId) =>
            Path.Combine(saveDir, "00000001", contentId, contentId[2..]);

        // Entries whose vehicle is still in the save (deleting it in the game
        // drops it from here too).
        public static List<Installed> LoadInstalled(string saveDir)
        {
            try
            {
                var all = JsonSerializer.Deserialize<List<Installed>>(File.ReadAllText(ManifestPath(saveDir)));
                return all?.Where(i => File.Exists(ContentFile(saveDir, i.ContentId))).ToList() ?? new();
            }
            catch (Exception e) when (e is IOException or JsonException) { return new(); }
        }

        private static void SaveInstalled(string saveDir, List<Installed> list) =>
            File.WriteAllText(ManifestPath(saveDir), JsonSerializer.Serialize(list));

        public static Installed InstallEntry(string saveDir, Entry e, byte[] blueprint)
        {
            var item = new Installed(e.Id, Install(saveDir, blueprint, e.Name), e.Name, e.Author,
                e.Description, e.Parts, e.PreviewPath);
            var list = LoadInstalled(saveDir);
            list.RemoveAll(i => i.Id == e.Id);
            list.Add(item);
            SaveInstalled(saveDir, list);
            return item;
        }

        // Takes a workshop vehicle back out of the save: its content folder and
        // header. Only ever ids from the manifest, never the player's own builds.
        public static void Remove(string saveDir, string id)
        {
            var list = LoadInstalled(saveDir);
            foreach (var i in list.Where(i => i.Id == id))
            {
                string folder = Path.Combine(saveDir, "00000001", i.ContentId);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                File.Delete(Path.Combine(HeadersDir(saveDir), i.ContentId + ".header"));
            }
            list.RemoveAll(i => i.Id == id);
            SaveInstalled(saveDir, list);
        }

        // ------------------------------------------------------------ submitting

        // What the issue form takes: name first (the bot looks for it), the
        // blueprint, the custom parts it uses, and the screenshot as a JPEG small
        // enough that the whole thing fits GitHub's 65536-character issue body.
        public static string BuildSubmission(byte[] blueprint, string name, string description, string screenshot,
            List<CustomPart> custom)
        {
            if (!IsBlueprint(blueprint, out _)) throw new InvalidDataException("That vehicle file is damaged.");
            string customJson = JsonSerializer.Serialize(custom.Select(c => new { id = c.Id, mod = c.Mod }));
            int room = (60000 - Convert.ToBase64String(blueprint).Length - description.Length * 6 - customJson.Length * 2) * 3 / 4;
            if (room < 8000) throw new InvalidDataException("That vehicle is too big to share through GitHub.");
            return JsonSerializer.Serialize(new
            {
                name = name.Trim(),
                description = description.Trim(),
                blueprint = Convert.ToBase64String(blueprint),
                preview = Convert.ToBase64String(Jpeg(screenshot, room)),
                custom_parts = custom.Select(c => new { id = c.Id, mod = c.Mod }),
            });
        }

        // A part mod: the issue gets this JSON plus the .nbpatch as an attached
        // .zip (GitHub takes no .nbpatch), whose hash ties the two together.
        // Returns the JSON and the .zip's path.
        public static (string Json, string Zip) BuildModSubmission(string nbpatch, string name, string description, string screenshot)
        {
            var mod = ReadMod(nbpatch);
            if (mod.PartIds.Count == 0) throw new InvalidDataException("That mod has no parts; the workshop hosts part mods.");
            // the same rule the bot applies: parts and the mod's own new bundles only
            using (var archive = ZipFile.OpenRead(nbpatch))
            using (var doc = JsonDocument.Parse(archive.GetEntry("patch.json")!.Open()))
            {
                var root = doc.RootElement;
                bool Has(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0;
                if (Has("ExeMods") || Has("Ops"))
                    throw new InvalidDataException("That mod patches the game's executable or world; the workshop hosts part mods only.");
                if (root.TryGetProperty("Files", out var files) && files.ValueKind == JsonValueKind.Array)
                    foreach (var f in files.EnumerateArray())
                    {
                        string kind = f.TryGetProperty("Kind", out var k) ? k.GetString() ?? "delta" : "delta";
                        string path = f.TryGetProperty("Path", out var pth) ? pth.GetString() ?? "" : "";
                        if (kind != "new" || !System.Text.RegularExpressions.Regex.IsMatch(path, "^Bundle/(4f|50)/[0-9a-fA-F]{6}$"))
                            throw new InvalidDataException($"That mod changes the game file {path}; the workshop hosts part mods only.");
                    }
            }
            if (new FileInfo(nbpatch).Length > 25 * 1024 * 1024) throw new InvalidDataException("GitHub takes files up to 25 MB.");
            byte[] data = File.ReadAllBytes(nbpatch);
            string folder = Path.Combine(Path.GetTempPath(), "nutstaller-share");
            Directory.CreateDirectory(folder);
            string zip = Path.Combine(folder, (mod.ModId.Length > 0 ? mod.ModId : "mod") + ".nbpatch.zip");
            File.WriteAllBytes(zip, data);
            string json = JsonSerializer.Serialize(new
            {
                name = name.Trim(),
                description = description.Trim(),
                sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
                preview = Convert.ToBase64String(Jpeg(screenshot, (60000 - description.Length * 6 - name.Length * 6 - 200) * 3 / 4)),
            });
            return (json, zip);
        }

        // The screenshot as a JPEG of at most room bytes: 640 wide and smaller,
        // lower quality first.
        private static byte[] Jpeg(string screenshot, int room)
        {
            using var source = SKBitmap.Decode(screenshot) ?? throw new InvalidDataException("The screenshot could not be read.");
            byte[] jpeg = Array.Empty<byte>();
            for (int width = 640; width >= 160 && (jpeg.Length == 0 || jpeg.Length > room); width -= 160)
            {
                float scale = Math.Min(1f, Math.Min((float)width / source.Width, width * 9f / 16 / source.Height));
                using var small = source.Resize(new SKImageInfo(Math.Max(1, (int)(source.Width * scale)),
                    Math.Max(1, (int)(source.Height * scale))), SKFilterQuality.High);
                for (int quality = 85; quality >= 40; quality -= 15)
                {
                    using var data = small.Encode(SKEncodedImageFormat.Jpeg, quality);
                    jpeg = data.ToArray();
                    if (jpeg.Length <= room) break;
                }
            }
            if (jpeg.Length > room) throw new InvalidDataException("The screenshot could not be made small enough.");
            return jpeg;
        }
    }
}
