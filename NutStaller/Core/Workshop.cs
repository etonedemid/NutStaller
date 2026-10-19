using System.Buffers.Binary;
using System.Net.Http;
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

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient();
            c.DefaultRequestHeaders.UserAgent.ParseAdd("NutStaller/1.0");
            c.Timeout = TimeSpan.FromSeconds(30);
            return c;
        }

        public record Entry(string Id, string Name, string Description, string Author, int Parts,
            string Created, string BlueprintPath, string PreviewPath);

        public static async Task<List<Entry>> FetchIndexAsync()
        {
            // raw.githubusercontent.com caches for a few minutes; the query
            // string gets a fresh copy when Refresh is pressed
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(
                Raw + "index.json?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            var list = new List<Entry>();
            foreach (var b in doc.RootElement.GetProperty("blueprints").EnumerateArray())
            {
                string S(string key) => b.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
                list.Add(new Entry(S("id"), S("name"), S("description"), S("author"),
                    b.TryGetProperty("parts", out var p) ? p.GetInt32() : 0,
                    S("created"), S("blueprint"), S("preview")));
            }
            return list;
        }

        public static Task<byte[]> DownloadAsync(string repoPath) => Http.GetByteArrayAsync(Raw + repoPath);

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
        // blueprint, and the screenshot as a JPEG small enough that the whole
        // thing fits GitHub's 65536-character issue body.
        public static string BuildSubmission(byte[] blueprint, string name, string description, string screenshot)
        {
            if (!IsBlueprint(blueprint, out _)) throw new InvalidDataException("That vehicle file is damaged.");
            int room = (60000 - Convert.ToBase64String(blueprint).Length - description.Length * 6) * 3 / 4;
            if (room < 8000) throw new InvalidDataException("That vehicle is too big to share through GitHub.");
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
            return JsonSerializer.Serialize(new
            {
                name = name.Trim(),
                description = description.Trim(),
                blueprint = Convert.ToBase64String(blueprint),
                preview = Convert.ToBase64String(jpeg),
            });
        }
    }
}
