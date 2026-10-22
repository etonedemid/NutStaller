using System.Buffers.Binary;
using System.Text;

namespace NutStaller.Core
{
    // Xbox 360 STFS packages (CON, LIVE, PIRS): how a 360 keeps each save, one
    // file per save with its header (display name, title) inside. Read-only,
    // enough to take the files out. Layout as documented by Free60 and Velocity:
    // 0x1000-byte blocks after the header, with hash tables between every 170
    // data blocks (two copies of each on "male" packages, which also tell
    // which copy is current).
    internal static class Stfs
    {
        public record Package(string FileName, uint TitleId, uint ContentType, string DisplayName,
            List<(string Path, byte[] Data)> Files);

        public static bool IsPackage(string file)
        {
            try
            {
                using var f = File.OpenRead(file);
                var magic = new byte[4];
                if (f.Length < 0xA000 || f.Read(magic, 0, 4) != 4) return false;
                string m = Encoding.ASCII.GetString(magic);
                return m is "CON " or "LIVE" or "PIRS";
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private const int BlocksPerTable = 0xAA, BlocksPerLevel1 = 0x70E4;

        private sealed class Reader
        {
            private readonly byte[] _d;
            private readonly int _shift;          // 0: one copy of each hash table, 1: two
            private readonly long _firstTable;
            private readonly int _topLevel;
            private readonly byte _separation;

            public Reader(byte[] d)
            {
                _d = d;
                uint headerSize = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(0x340));
                _separation = d[0x37B];
                _shift = (~_separation) & 1;
                _firstTable = (headerSize + 0xFFF) & 0xFFFFF000;
                uint allocated = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(0x395));
                _topLevel = allocated <= BlocksPerTable ? 0 : allocated <= BlocksPerLevel1 ? 1 : 2;
            }

            private int Step0 => _shift == 0 ? 0xAB : 0xAC;

            private long DataBlock(int b)
            {
                long r = ((b + BlocksPerTable) / BlocksPerTable << _shift) + b;
                if (b < BlocksPerTable) return r;
                if (b < BlocksPerLevel1) return r + ((b + BlocksPerLevel1) / BlocksPerLevel1 << _shift);
                return (1 << _shift) + r + ((b + BlocksPerLevel1) / BlocksPerLevel1 << _shift);
            }

            public long BlockOffset(int b) => (DataBlock(b) << 12) + _firstTable;

            private long Level0Table(int b)
            {
                if (b < BlocksPerTable) return 0;
                long n = (long)(b / BlocksPerTable) * Step0 + ((b / BlocksPerLevel1 + 1) << _shift);
                return b / BlocksPerLevel1 == 0 ? n : n + (1 << _shift);
            }

            // The block after b in its file, from b's hash table entry.
            public int Next(int b)
            {
                if (_topLevel > 1) throw new InvalidDataException("Package too big for a save.");
                long entry = (Level0Table(b) << 12) + _firstTable + (b % BlocksPerTable) * 0x18;
                if (_shift == 1)
                {
                    if (_topLevel == 0)
                        entry += (_separation & 2) << 11;
                    else
                    {
                        long level1 = (Step0 << 12) + _firstTable + ((_separation & 2) << 11);
                        entry += (_d[level1 + b / BlocksPerTable * 0x18 + 0x14] & 0x40) << 6;
                    }
                }
                return _d[entry + 0x15] << 16 | _d[entry + 0x16] << 8 | _d[entry + 0x17];
            }

            public byte[] ReadChain(int start, int blocks, long size, bool consecutive)
            {
                var out_ = new byte[size];
                int b = start;
                for (long pos = 0, i = 0; pos < size && i < blocks; i++)
                {
                    int n = (int)Math.Min(0x1000, size - pos);
                    long at = BlockOffset(b);
                    if (at < 0 || at + n > _d.Length) throw new InvalidDataException("Damaged package.");
                    Array.Copy(_d, at, out_, pos, n);
                    pos += n;
                    if (pos < size) b = consecutive ? b + 1 : Next(b);
                }
                return out_;
            }
        }

        private static int Int24Le(byte[] d, long at) => d[at] | d[at + 1] << 8 | d[at + 2] << 16;

        public static Package? Read(string file)
        {
            try
            {
                byte[] d = File.ReadAllBytes(file);
                var r = new Reader(d);
                uint contentType = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(0x344));
                uint titleId = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(0x360));
                string display = Encoding.BigEndianUnicode.GetString(d, 0x411, 0x80).Split('\0')[0];
                int tableBlocks = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(0x37C));
                int tableStart = Int24Le(d, 0x37E);
                byte[] table = r.ReadChain(tableStart, tableBlocks, tableBlocks * 0x1000L, false);

                // 0x40-byte entries: name, flags (name length, consecutive 0x40,
                // folder 0x80), block count, first block, parent entry, size.
                var names = new List<string>();
                var parents = new List<int>();
                var files = new List<(string, byte[])>();
                var entries = new List<(int Index, int Start, int Blocks, long Size, bool Consecutive)>();
                for (int i = 0; i * 0x40 < table.Length; i++)
                {
                    int e = i * 0x40;
                    int flags = table[e + 0x28];
                    int len = flags & 0x3F;
                    if (len == 0) break;
                    names.Add(Encoding.ASCII.GetString(table, e, len));
                    parents.Add(BinaryPrimitives.ReadInt16BigEndian(table.AsSpan(e + 0x32)));
                    if ((flags & 0x80) == 0)
                        entries.Add((i, Int24Le(table, e + 0x2F), Int24Le(table, e + 0x29),
                            BinaryPrimitives.ReadUInt32BigEndian(table.AsSpan(e + 0x34)), (flags & 0x40) != 0));
                }
                foreach (var en in entries)
                {
                    string path = names[en.Index];
                    for (int p = parents[en.Index], guard = 0; p >= 0 && p < names.Count && guard < 64; p = parents[p], guard++)
                        path = names[p] + "/" + path;
                    files.Add((path, r.ReadChain(en.Start, en.Blocks, en.Size, en.Consecutive)));
                }
                return new Package(Path.GetFileName(file), titleId, contentType, display, files);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or IndexOutOfRangeException or ArgumentException)
            {
                return null;
            }
        }
    }
}
