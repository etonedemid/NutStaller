using System.Globalization;
using System.Text;

namespace NutStaller.Core
{
    // Minimal flat key = value TOML reader/writer for renut.toml.
    // Preserves keys it does not know about and keeps the file order stable.
    internal class TomlConfig
    {
        private readonly List<string> _order = new();
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        public string Path { get; }

        public TomlConfig(string path)
        {
            Path = path;
            if (!File.Exists(path)) return;
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('[')) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line[..eq].Trim();
                string val = line[(eq + 1)..].Trim();
                if (key.Length == 0) continue;
                if (!_values.ContainsKey(key)) _order.Add(key);
                _values[key] = val;
            }
        }

        public IEnumerable<string> Keys => _order;

        public bool Has(string key) => _values.ContainsKey(key);

        public string GetString(string key, string fallback = "")
        {
            if (!_values.TryGetValue(key, out var v)) return fallback;
            v = v.Trim();
            if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') v = v[1..^1];
            return v;
        }

        public bool GetBool(string key, bool fallback = false)
        {
            string v = GetString(key).ToLowerInvariant();
            if (v == "true" || v == "1" || v == "yes") return true;
            if (v == "false" || v == "0" || v == "no") return false;
            return fallback;
        }

        public double GetNumber(string key, double fallback = 0)
            => double.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;

        public void SetBool(string key, bool value) => SetRaw(key, value ? "true" : "false");

        public void SetNumber(string key, double value)
        {
            string s = value == Math.Floor(value) && Math.Abs(value) < 1e15
                ? ((long)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.######", CultureInfo.InvariantCulture);
            SetRaw(key, s);
        }

        public void SetString(string key, string value) => SetRaw(key, "\"" + value.Replace("\"", "") + "\"");

        public void Remove(string key)
        {
            if (_values.Remove(key)) _order.Remove(key);
        }

        private void SetRaw(string key, string value)
        {
            if (!_values.ContainsKey(key)) _order.Add(key);
            _values[key] = value;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Auto-generated cvar configuration");
            foreach (var key in _order)
                sb.AppendLine($"{key} = {_values[key]}");
            File.WriteAllText(Path, sb.ToString());
        }
    }
}
