using System;
using System.Collections.Generic;
using System.Text;

namespace BubbleWorld
{
    /// <summary>
    /// Minimal QR code encoder (ISO/IEC 18004) — byte mode, error correction
    /// level M, versions 1-6 (up to 106 bytes). Pure C# with no engine
    /// dependencies, so it runs anywhere and can be verified by a standalone
    /// test harness. Covers pairing deep links like
    /// "bubbleworld://pair?code=XXXXXX" with room to spare.
    /// </summary>
    public static class QrCodeEncoder
    {
        public sealed class QrCode
        {
            /// <summary>Dark modules, row-major. Quiet zone (4 modules) included.</summary>
            public bool[,] Modules { get; internal set; }
            /// <summary>Modules per side including the quiet zone.</summary>
            public int Size { get; internal set; }
            /// <summary>QR version used (1..6).</summary>
            public int Version { get; internal set; }
        }

        // ---- Per-version tables (EC level M), index 0 unused ----
        private static readonly int[] TotalCodewords = { 0, 26, 44, 70, 100, 134, 172 };
        private static readonly int[] EcPerBlock     = { 0, 10, 16, 26, 18, 24, 16 };
        private static readonly int[] BlockCounts    = { 0, 1, 1, 1, 2, 2, 4 };

        // Alignment pattern center coordinates per version (index = version,
        // index 0 unused). v1 has none; for v2-6 only the (last, last) combo
        // survives — the combos touching row/col 6 overlap the finder corners
        // and are skipped below.
        private static readonly int[][] AlignmentCenters =
        {
            new int[0],       // 0 (unused)
            new int[0],       // v1: none
            new[] { 6, 18 },  // v2
            new[] { 6, 22 },  // v3
            new[] { 6, 26 },  // v4
            new[] { 6, 30 },  // v5
            new[] { 6, 34 },  // v6
        };

        private const int QuietZone = 4;
        // Error-correction level indicator in the format bits: L=1, M=0, Q=3, H=2.
        private const int FormatEcLevel = 0;

        // ---- Galois field GF(256), primitive polynomial 0x11D ----
        private static readonly int[] ExpTable = new int[512];
        private static readonly int[] LogTable = new int[256];

        static QrCodeEncoder()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                ExpTable[i] = x;
                LogTable[x] = i;
                x <<= 1;
                if ((x & 0x100) != 0) x ^= 0x11D;
            }
            for (int i = 255; i < 512; i++) ExpTable[i] = ExpTable[i - 255];
        }

        private static int GfMul(int a, int b) =>
            a == 0 || b == 0 ? 0 : ExpTable[LogTable[a] + LogTable[b]];

        /// <summary>
        /// Encodes UTF-8 text into a QR symbol. Throws when the text exceeds
        /// the 106-byte capacity of versions 1-6 at EC level M.
        /// </summary>
        public static QrCode Encode(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            byte[] payload = Encoding.UTF8.GetBytes(text);

            int version = SelectVersion(payload.Length);
            int dataCodewords = TotalCodewords[version] - EcPerBlock[version] * BlockCounts[version];
            byte[] codewords = BuildCodewords(payload, version, dataCodewords);
            return BuildMatrix(codewords, version);
        }

        // Smallest version whose byte-mode capacity (4-bit mode + 8-bit count
        // header) fits the payload.
        private static int SelectVersion(int byteCount)
        {
            for (int v = 1; v <= 6; v++)
            {
                int dataCw = TotalCodewords[v] - EcPerBlock[v] * BlockCounts[v];
                int capacity = (dataCw * 8 - 12) / 8;
                if (byteCount <= capacity) return v;
            }
            throw new ArgumentException(
                $"Text too long for QR versions 1-6 at EC level M ({byteCount} bytes, max 106).");
        }

        // Data codewords + Reed-Solomon ECC, split into blocks and interleaved.
        private static byte[] BuildCodewords(byte[] payload, int version, int dataCodewords)
        {
            int capacityBits = dataCodewords * 8;
            var bits = new List<bool>(capacityBits + 8);

            AppendBits(bits, 0x4, 4);                    // byte mode indicator
            AppendBits(bits, payload.Length, 8);         // char count (versions 1-9)
            foreach (byte b in payload) AppendBits(bits, b, 8);

            // Terminator, then align to a byte boundary, then pad codewords.
            AppendBits(bits, 0, Math.Min(4, capacityBits - bits.Count));
            while (bits.Count % 8 != 0) bits.Add(false);
            bool padToggle = false;
            while (bits.Count < capacityBits)
            {
                AppendBits(bits, padToggle ? 0x11 : 0xEC, 8);
                padToggle = !padToggle;
            }

            var data = new byte[dataCodewords];
            for (int i = 0; i < dataCodewords; i++)
            {
                byte value = 0;
                for (int j = 0; j < 8; j++)
                    if (bits[i * 8 + j]) value |= (byte)(0x80 >> j);
                data[i] = value;
            }

            int blockCount = BlockCounts[version];
            int ecLen = EcPerBlock[version];
            int perBlock = dataCodewords / blockCount;   // equal blocks in versions 1-6

            var blocks = new byte[blockCount][];
            var ecBlocks = new byte[blockCount][];
            for (int b = 0; b < blockCount; b++)
            {
                blocks[b] = new byte[perBlock];
                Array.Copy(data, b * perBlock, blocks[b], 0, perBlock);
                ecBlocks[b] = RsEncode(blocks[b], ecLen);
            }

            var result = new byte[dataCodewords + ecLen * blockCount];
            int idx = 0;
            for (int i = 0; i < perBlock; i++)
                for (int b = 0; b < blockCount; b++)
                    result[idx++] = blocks[b][i];
            for (int i = 0; i < ecLen; i++)
                for (int b = 0; b < blockCount; b++)
                    result[idx++] = ecBlocks[b][i];
            return result;
        }

        // Polynomial remainder of data * x^ecLen modulo the generator — the
        // Reed-Solomon error-correction codewords.
        private static byte[] RsEncode(byte[] data, int ecLen)
        {
            int[] gen = RsGenerator(ecLen);
            var residual = new int[ecLen];
            foreach (int d in data)
            {
                int factor = d ^ residual[0];
                Array.Copy(residual, 1, residual, 0, ecLen - 1);
                residual[ecLen - 1] = 0;
                if (factor == 0) continue;
                for (int i = 0; i < ecLen; i++)
                    residual[i] ^= GfMul(gen[i + 1], factor);
            }
            var ec = new byte[ecLen];
            for (int i = 0; i < ecLen; i++) ec[i] = (byte)residual[i];
            return ec;
        }

        // Generator polynomial (highest-degree coefficient first) for the
        // product of (x - a^i) for i in 0..degree-1.
        private static int[] RsGenerator(int degree)
        {
            var g = new[] { 1 };
            for (int i = 0; i < degree; i++)
            {
                var next = new int[g.Length + 1];
                int alpha = ExpTable[i];
                for (int j = 0; j < g.Length; j++)
                {
                    next[j] ^= g[j];                      // x * g[j]
                    next[j + 1] ^= GfMul(g[j], alpha);    // a^i * g[j]
                }
                g = next;
            }
            return g;
        }

        private static void AppendBits(List<bool> bits, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
                bits.Add(((value >> i) & 1) != 0);
        }

        // ---- Matrix construction ----

        private static QrCode BuildMatrix(byte[] codewords, int version)
        {
            int size = 17 + 4 * version;
            var modules = new bool[size, size];
            var function = new bool[size, size];   // function/reserved modules (never masked)

            void SetFunction(int row, int col, bool dark)
            {
                modules[row, col] = dark;
                function[row, col] = true;
            }

            // Finder patterns + separators (three corners; bottom-right is the
            // alignment pattern's job).
            PlaceFinder(SetFunction, 0, 0, size);
            PlaceFinder(SetFunction, 0, size - 7, size);
            PlaceFinder(SetFunction, size - 7, 0, size);

            // Vertical/horizontal timing patterns between the finders.
            for (int i = 8; i < size - 8; i++)
            {
                bool dark = (i & 1) == 0;
                SetFunction(6, i, dark);
                SetFunction(i, 6, dark);
            }

            // Alignment patterns (one per version in 2-6).
            int[] centers = AlignmentCenters[version];
            for (int a = 0; a < centers.Length; a++)
            {
                for (int b = 0; b < centers.Length; b++)
                {
                    int cr = centers[a], cc = centers[b];
                    if ((cr <= 8 && cc <= 8) ||
                        (cr <= 8 && cc >= size - 9) ||
                        (cr >= size - 9 && cc <= 8)) continue;   // overlaps a finder
                    for (int dr = -2; dr <= 2; dr++)
                        for (int dc = -2; dc <= 2; dc++)
                            SetFunction(cr + dr, cc + dc,
                                Math.Max(Math.Abs(dr), Math.Abs(dc)) != 1);
                }
            }

            // Reserve the format-info areas (values drawn per mask below).
            // Row 8 / col 8 skip index 6 — those cells belong to the timing
            // patterns.
            for (int i = 0; i <= 8; i++)
            {
                if (i == 6) continue;
                SetFunction(8, i, false);
                SetFunction(i, 8, false);
            }
            for (int i = 0; i < 8; i++)
            {
                SetFunction(8, size - 1 - i, false);     // row 8, right edge
                SetFunction(size - 1 - i, 8, false);     // col 8, bottom edge
            }

            PlaceDataBits(modules, function, codewords, size);

            // Try all 8 masks, score them, keep the best.
            int bestMask = 0;
            int bestPenalty = int.MaxValue;
            var candidate = new bool[size, size];
            for (int mask = 0; mask < 8; mask++)
            {
                Array.Copy(modules, candidate, modules.Length);
                ApplyMask(candidate, function, size, mask);
                DrawFormatBits(candidate, size, mask);
                int penalty = Penalty(candidate, size);
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    bestMask = mask;
                }
            }
            ApplyMask(modules, function, size, bestMask);
            DrawFormatBits(modules, size, bestMask);

            // Copy into a larger matrix with the quiet zone.
            int finalSize = size + QuietZone * 2;
            var finalModules = new bool[finalSize, finalSize];
            for (int r = 0; r < size; r++)
                for (int c = 0; c < size; c++)
                    finalModules[r + QuietZone, c + QuietZone] = modules[r, c];

            return new QrCode
            {
                Modules = finalModules,
                Size = finalSize,
                Version = version,
            };
        }

        private static void PlaceFinder(Action<int, int, bool> set, int row, int col, int size)
        {
            // 7x7 pattern + 1-module separator ring (light) around it.
            for (int dr = -1; dr <= 7; dr++)
            {
                for (int dc = -1; dc <= 7; dc++)
                {
                    int r = row + dr, c = col + dc;
                    if (r < 0 || c < 0 || r >= size || c >= size) continue;
                    bool inPattern = dr >= 0 && dr <= 6 && dc >= 0 && dc <= 6;
                    bool dark = inPattern && (
                        dr == 0 || dr == 6 || dc == 0 || dc == 6 ||
                        (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4));
                    set(r, c, dark);
                }
            }
        }

        // Zigzag: pairs of columns from the right edge, alternating direction;
        // the timing column (6) is skipped. Stores the raw (pre-mask) bits.
        private static void PlaceDataBits(bool[,] modules, bool[,] function, byte[] codewords, int size)
        {
            int bitIndex = 0;
            int totalBits = codewords.Length * 8;

            for (int col = size - 1; col >= 1; col -= 2)
            {
                if (col == 6) col = 5;
                bool upward = ((col + 1) & 2) == 0;

                for (int i = 0; i < size; i++)
                {
                    int row = upward ? size - 1 - i : i;
                    for (int k = 0; k < 2; k++)
                    {
                        int c = col - k;
                        if (function[row, c]) continue;
                        bool bit = false;
                        if (bitIndex < totalBits)
                        {
                            bit = ((codewords[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                            bitIndex++;
                        }
                        modules[row, c] = bit;   // remainder bits stay 0
                    }
                }
            }
        }

        // XOR mask over data modules only (function modules are skipped).
        private static void ApplyMask(bool[,] modules, bool[,] function, int size, int mask)
        {
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (function[r, c]) continue;
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (r + c) % 2 == 0; break;
                        case 1: invert = r % 2 == 0; break;
                        case 2: invert = c % 3 == 0; break;
                        case 3: invert = (r + c) % 3 == 0; break;
                        case 4: invert = (r / 2 + c / 3) % 2 == 0; break;
                        case 5: invert = (r * c) % 2 + (r * c) % 3 == 0; break;
                        case 6: invert = ((r * c) % 2 + (r * c) % 3) % 2 == 0; break;
                        default: invert = ((r + c) % 2 + (r * c) % 3) % 2 == 0; break;
                    }
                    if (invert) modules[r, c] = !modules[r, c];
                }
            }
        }

        // 15-bit format string: 5 data bits (EC level + mask) with BCH(15,5)
        // protection, XOR-masked with 0x5412. Two copies are written — one
        // around the top-left finder, one split between top-right/bottom-left —
        // plus the always-dark module.
        private static void DrawFormatBits(bool[,] modules, int size, int mask)
        {
            int data = (FormatEcLevel << 3) | mask;
            int rem = data;
            for (int i = 0; i < 10; i++)
                rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
            int bits = ((data << 10) | rem) ^ 0x5412;

            bool Bit(int i) => ((bits >> i) & 1) != 0;

            // Copy 1 (around the top-left finder).
            for (int i = 0; i <= 5; i++) modules[i, 8] = Bit(i);
            modules[7, 8] = Bit(6);
            modules[8, 8] = Bit(7);
            modules[8, 7] = Bit(8);
            for (int i = 9; i < 15; i++) modules[8, 14 - i] = Bit(i);

            // Copy 2 (top-right row + bottom-left column).
            for (int i = 0; i < 8; i++) modules[8, size - 1 - i] = Bit(i);
            for (int i = 8; i < 15; i++) modules[size - 15 + i, 8] = Bit(i);

            modules[size - 8, 8] = true;   // always-dark module
        }

        // ---- Mask penalty scoring (ISO/IEC 18004 §8.8.2) ----

        private static readonly bool[] FinderLikeA =
            { true, false, true, true, true, false, true, false, false, false, false };
        private static readonly bool[] FinderLikeB =
            { false, false, false, false, true, false, true, true, true, false, true };

        private static int Penalty(bool[,] m, int size)
        {
            int penalty = 0;

            // Rule 1: runs of 5+ same-colored modules.
            for (int r = 0; r < size; r++)
            {
                int run = 1;
                for (int c = 1; c < size; c++)
                {
                    if (m[r, c] == m[r, c - 1]) run++;
                    else { if (run >= 5) penalty += 3 + (run - 5); run = 1; }
                }
                if (run >= 5) penalty += 3 + (run - 5);
            }
            for (int c = 0; c < size; c++)
            {
                int run = 1;
                for (int r = 1; r < size; r++)
                {
                    if (m[r, c] == m[r - 1, c]) run++;
                    else { if (run >= 5) penalty += 3 + (run - 5); run = 1; }
                }
                if (run >= 5) penalty += 3 + (run - 5);
            }

            // Rule 2: 2x2 blocks of one color.
            for (int r = 0; r < size - 1; r++)
                for (int c = 0; c < size - 1; c++)
                    if (m[r, c] == m[r, c + 1] && m[r, c] == m[r + 1, c] && m[r, c] == m[r + 1, c + 1])
                        penalty += 3;

            // Rule 3: finder-lookalike 11-module sequences.
            for (int r = 0; r < size; r++)
                for (int c = 0; c <= size - 11; c++)
                {
                    if (MatchAt(m, size, r, c, true, FinderLikeA) || MatchAt(m, size, r, c, true, FinderLikeB))
                        penalty += 40;
                }
            for (int c = 0; c < size; c++)
                for (int r = 0; r <= size - 11; r++)
                {
                    if (MatchAt(m, size, r, c, false, FinderLikeA) || MatchAt(m, size, r, c, false, FinderLikeB))
                        penalty += 40;
                }

            // Rule 4: deviation of the dark-module proportion from 50%.
            int dark = 0;
            for (int r = 0; r < size; r++)
                for (int c = 0; c < size; c++)
                    if (m[r, c]) dark++;
            int total = size * size;
            penalty += Math.Abs(dark * 100 - total * 50) / total / 5 * 10;

            return penalty;
        }

        // Matches an 11-module pattern along a row (rowMajor) or column.
        private static bool MatchAt(bool[,] m, int size, int start0, int start1, bool rowMajor, bool[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                bool value = rowMajor ? m[start0, start1 + i] : m[start0 + i, start1];
                if (value != pattern[i]) return false;
            }
            return true;
        }
    }
}
