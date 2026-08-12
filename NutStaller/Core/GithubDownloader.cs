using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace NutStaller.Core
{
    // Fetches the latest release of a GitHub repo, downloads a chosen asset
    // with progress and unzips it to a target directory.
    internal static class GithubDownloader
    {
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient();
            c.DefaultRequestHeaders.UserAgent.ParseAdd("NutStaller/1.0");
            c.Timeout = TimeSpan.FromMinutes(10);
            return c;
        }

        public record ReleaseAsset(string Name, string Url, long Size, string Tag);

        // rank lets callers prefer one asset over another (higher wins); all
        // ranked >= 0 are acceptable, the best match is returned.
        public static async Task<ReleaseAsset> GetLatestAssetAsync(string repo, Func<string, int> rank)
        {
            using var doc = JsonDocument.Parse(
                await Http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest"));
            string tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "?";

            ReleaseAsset? best = null;
            int bestRank = -1;
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                int r = rank(name);
                if (r <= bestRank) continue;
                bestRank = r;
                best = new ReleaseAsset(
                    name,
                    asset.GetProperty("browser_download_url").GetString() ?? "",
                    asset.GetProperty("size").GetInt64(),
                    tag);
            }
            return best ?? throw new InvalidOperationException($"No matching release asset found in {repo} ({tag}).");
        }

        public static async Task DownloadAsync(ReleaseAsset asset, string destFile, IProgress<double> progress)
        {
            using var resp = await Http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? asset.Size;

            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(destFile);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                if (total > 0) progress.Report((double)done / total);
            }
            progress.Report(1);
        }

        public static void ExtractZip(string zipFile, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            ZipFile.ExtractToDirectory(zipFile, targetDir, overwriteFiles: true);
        }

        // Some zips wrap everything in a single top folder; flatten it so the
        // target dir holds the payload directly.
        public static void FlattenSingleFolder(string dir)
        {
            var entries = Directory.GetFileSystemEntries(dir);
            if (entries.Length != 1 || !Directory.Exists(entries[0])) return;
            string inner = entries[0];
            foreach (var path in Directory.GetFileSystemEntries(inner))
            {
                string dest = Path.Combine(dir, Path.GetFileName(path));
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                else if (File.Exists(dest)) File.Delete(dest);
                if (Directory.Exists(path)) Directory.Move(path, dest);
                else File.Move(path, dest);
            }
            Directory.Delete(inner);
        }
    }
}
