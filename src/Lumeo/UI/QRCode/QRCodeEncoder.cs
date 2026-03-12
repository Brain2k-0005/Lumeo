using System;
using System.Collections.Generic;
using System.Text;

namespace Lumeo;

/// <summary>
/// Pure C# QR code encoder. Supports byte mode, versions 1–10, all EC levels, all 8 masks.
/// </summary>
internal static class QRCodeEncoder
{
    // ── Error Correction Level ───────────────────────────────────────────────

    public enum ErrorCorrectionLevel { Low = 0, Medium = 1, Quartile = 2, High = 3 }

    // ── Public entry point ───────────────────────────────────────────────────

    /// <summary>
    /// Encodes <paramref name="text"/> and returns a bool[,] matrix where true = dark module.
    /// Quiet zone (4 modules) is NOT included; add it in the caller if needed.
    /// </summary>
    public static bool[,] Encode(string text, ErrorCorrectionLevel ecl)
    {
        var data = Encoding.UTF8.GetBytes(text);
        int version = ChooseVersion(data.Length, ecl);
        var codewords = BuildCodewords(data, version, ecl);
        var ecCodewords = AddErrorCorrection(codewords, version, ecl);
        var matrix = BuildMatrix(ecCodewords, version, ecl);
        return matrix;
    }

    // ── Version selection ────────────────────────────────────────────────────

    // Max data bytes per version/EC level (versions 1–10, EC Low/Med/Qua/High)
    private static readonly int[,] DataCapacity =
    {
        //  L    M    Q    H
        {  17,  14,  11,   7 }, // v1
        {  32,  26,  20,  14 }, // v2
        {  53,  42,  32,  24 }, // v3
        {  78,  62,  46,  34 }, // v4
        { 106,  84,  60,  44 }, // v5
        { 134, 106,  74,  58 }, // v6
        { 154, 122,  86,  64 }, // v7
        { 192, 152, 108,  84 }, // v8
        { 230, 180, 130, 98  }, // v9
        { 271, 213, 151, 119 }, // v10
    };

    private static int ChooseVersion(int byteLength, ErrorCorrectionLevel ecl)
    {
        int col = (int)ecl;
        for (int v = 0; v < DataCapacity.GetLength(0); v++)
            if (byteLength <= DataCapacity[v, col])
                return v + 1;
        throw new InvalidOperationException($"Data too long ({byteLength} bytes) for versions 1–10.");
    }

    // ── Codeword building ────────────────────────────────────────────────────

    // Total codewords per version (1–10)
    private static readonly int[] TotalCodewords = { 26, 44, 70, 100, 134, 172, 196, 242, 292, 346 };

    // EC codewords per block per version/EC level
    private static readonly int[,] EcPerBlock =
    {
        //  L   M   Q   H
        {  7,  10, 13, 17 }, // v1
        { 10,  16, 22, 28 }, // v2
        { 15,  26, 18, 22 }, // v3
        { 20,  18, 26, 16 }, // v4
        { 26,  24, 18, 22 }, // v5
        { 18,  16, 24, 28 }, // v6
        { 20,  18, 18, 26 }, // v7
        { 24,  22, 22, 26 }, // v8
        { 30,  22, 20, 24 }, // v9
        { 18,  26, 24, 28 }, // v10
    };

    // Number of EC blocks per version/EC level
    private static readonly int[,] NumBlocks =
    {
        //  L  M  Q  H
        {  1,  1,  1,  1 }, // v1
        {  1,  1,  1,  1 }, // v2
        {  1,  1,  2,  2 }, // v3
        {  1,  2,  2,  4 }, // v4
        {  1,  2,  4,  2 }, // v5 — note: some blocks may have +1 data codeword
        {  2,  4,  4,  4 }, // v6
        {  2,  4,  2,  4 }, // v7
        {  2,  2,  4,  4 }, // v8
        {  2,  3,  4,  4 }, // v9
        {  4,  4,  6,  6 }, // v10
    };

    private static byte[] BuildCodewords(byte[] data, int version, ErrorCorrectionLevel ecl)
    {
        int totalCw = TotalCodewords[version - 1];
        int ecCwPerBlock = EcPerBlock[version - 1, (int)ecl];
        int blocks = NumBlocks[version - 1, (int)ecl];
        int totalEcCw = ecCwPerBlock * blocks;
        int dataCw = totalCw - totalEcCw;

        // Bit buffer
        var bits = new List<bool>();

        // Mode indicator: 0100 = byte mode
        AppendBits(bits, 0b0100, 4);

        // Character count indicator
        int ccBits = version <= 9 ? 8 : 16;
        AppendBits(bits, data.Length, ccBits);

        // Data bytes
        foreach (byte b in data)
            AppendBits(bits, b, 8);

        // Terminator
        int remaining = dataCw * 8 - bits.Count;
        for (int i = 0; i < Math.Min(4, remaining); i++) bits.Add(false);

        // Byte-align
        while (bits.Count % 8 != 0) bits.Add(false);

        // Padding codewords
        bool toggle = true;
        while (bits.Count < dataCw * 8)
        {
            AppendBits(bits, toggle ? 0xEC : 0x11, 8);
            toggle = !toggle;
        }

        // Convert to bytes
        var result = new byte[dataCw];
        for (int i = 0; i < dataCw; i++)
        {
            int val = 0;
            for (int b = 0; b < 8; b++)
                if (bits[i * 8 + b]) val |= 1 << (7 - b);
            result[i] = (byte)val;
        }
        return result;
    }

    private static void AppendBits(List<bool> bits, int value, int length)
    {
        for (int i = length - 1; i >= 0; i--)
            bits.Add(((value >> i) & 1) == 1);
    }

    // ── Reed-Solomon error correction ────────────────────────────────────────

    private static byte[] AddErrorCorrection(byte[] data, int version, ErrorCorrectionLevel ecl)
    {
        int ecCwPerBlock = EcPerBlock[version - 1, (int)ecl];
        int blocks = NumBlocks[version - 1, (int)ecl];
        int totalCw = TotalCodewords[version - 1];
        int totalEcCw = ecCwPerBlock * blocks;
        int totalDataCw = totalCw - totalEcCw;

        // Split data into blocks
        int shortBlockLen = totalDataCw / blocks;
        int numLongBlocks = totalDataCw % blocks;

        var dataBlocks = new byte[blocks][];
        int idx = 0;
        for (int b = 0; b < blocks; b++)
        {
            int len = shortBlockLen + (b >= blocks - numLongBlocks ? 1 : 0);
            dataBlocks[b] = new byte[len];
            Array.Copy(data, idx, dataBlocks[b], 0, len);
            idx += len;
        }

        // Compute EC for each block
        var ecBlocks = new byte[blocks][];
        for (int b = 0; b < blocks; b++)
            ecBlocks[b] = ReedSolomon(dataBlocks[b], ecCwPerBlock);

        // Interleave data codewords
        var result = new byte[totalCw];
        int ri = 0;
        for (int col = 0; col < shortBlockLen + (numLongBlocks > 0 ? 1 : 0); col++)
            for (int b = 0; b < blocks; b++)
                if (col < dataBlocks[b].Length)
                    result[ri++] = dataBlocks[b][col];

        // Interleave EC codewords
        for (int col = 0; col < ecCwPerBlock; col++)
            for (int b = 0; b < blocks; b++)
                result[ri++] = ecBlocks[b][col];

        return result;
    }

    // GF(256) tables
    private static readonly byte[] GfExp = new byte[512];
    private static readonly byte[] GfLog = new byte[256];

    static QRCodeEncoder()
    {
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            GfExp[i] = (byte)x;
            GfLog[x] = (byte)i;
            x <<= 1;
            if ((x & 0x100) != 0) x ^= 0x11D;
        }
        for (int i = 255; i < 512; i++)
            GfExp[i] = GfExp[i - 255];
    }

    private static byte GfMul(byte a, byte b)
    {
        if (a == 0 || b == 0) return 0;
        return GfExp[GfLog[a] + GfLog[b]];
    }

    private static byte[] RsGeneratorPoly(int degree)
    {
        var poly = new byte[degree + 1];
        poly[degree] = 1;
        for (int i = 0; i < degree; i++)
        {
            byte alpha = GfExp[i];
            for (int j = 0; j < degree; j++)
                poly[j] = (byte)(GfMul(poly[j], alpha) ^ poly[j + 1]);
            poly[degree] = GfMul(poly[degree], alpha);
        }
        return poly;
    }

    private static byte[] ReedSolomon(byte[] data, int ecLen)
    {
        var gen = RsGeneratorPoly(ecLen);
        var remainder = new byte[ecLen];
        foreach (byte b in data)
        {
            byte coeff = (byte)(b ^ remainder[0]);
            Array.Copy(remainder, 1, remainder, 0, ecLen - 1);
            remainder[ecLen - 1] = 0;
            if (coeff != 0)
                for (int i = 0; i < ecLen; i++)
                    remainder[i] ^= GfMul(gen[i], coeff);
        }
        return remainder;
    }

    // ── Matrix building ──────────────────────────────────────────────────────

    private static bool[,] BuildMatrix(byte[] codewords, int version, ErrorCorrectionLevel ecl)
    {
        int size = version * 4 + 17;
        var matrix = new bool[size, size];
        var reserved = new bool[size, size]; // modules that are non-data

        PlaceFinders(matrix, reserved, size);
        PlaceTimings(matrix, reserved, size);
        PlaceAlignments(matrix, reserved, version);
        PlaceDarkModule(matrix, reserved, version);
        ReserveFormatAreas(reserved, size);

        // Place data bits with best mask
        int bestMask = -1;
        int bestPenalty = int.MaxValue;
        bool[,]? bestMatrix = null;

        for (int mask = 0; mask < 8; mask++)
        {
            var m = (bool[,])matrix.Clone();
            PlaceData(m, reserved, codewords, size);
            ApplyMask(m, reserved, mask, size);
            PlaceFormatInfo(m, ecl, mask, size);
            int penalty = ComputePenalty(m, size);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMask = mask;
                bestMatrix = (bool[,])m.Clone();
            }
        }

        return bestMatrix!;
    }

    // ── Finder patterns ──────────────────────────────────────────────────────

    private static void PlaceFinders(bool[,] m, bool[,] r, int size)
    {
        // Top-left, top-right, bottom-left
        PlaceFinder(m, r, 0, 0);
        PlaceFinder(m, r, 0, size - 7);
        PlaceFinder(m, r, size - 7, 0);

        // Separators
        for (int i = 0; i <= 7; i++)
        {
            SetReserved(m, r, 7, i, false);
            SetReserved(m, r, i, 7, false);
            SetReserved(m, r, 7, size - 8 + i, false);
            SetReserved(m, r, i, size - 8, false);
            SetReserved(m, r, size - 8, i, false);
            SetReserved(m, r, size - 8 + i, 7, false);
        }
    }

    private static void PlaceFinder(bool[,] m, bool[,] r, int row, int col)
    {
        for (int dr = 0; dr < 7; dr++)
        for (int dc = 0; dc < 7; dc++)
        {
            bool dark = dr == 0 || dr == 6 || dc == 0 || dc == 6
                        || (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4);
            SetReserved(m, r, row + dr, col + dc, dark);
        }
    }

    private static void SetReserved(bool[,] m, bool[,] r, int row, int col, bool dark)
    {
        m[row, col] = dark;
        r[row, col] = true;
    }

    // ── Timing patterns ──────────────────────────────────────────────────────

    private static void PlaceTimings(bool[,] m, bool[,] r, int size)
    {
        for (int i = 8; i < size - 8; i++)
        {
            bool dark = i % 2 == 0;
            SetReserved(m, r, 6, i, dark);
            SetReserved(m, r, i, 6, dark);
        }
    }

    // ── Alignment patterns ───────────────────────────────────────────────────

    // Centers for versions 1–10 (version 1 has none)
    private static readonly int[][] AlignmentPositions =
    {
        Array.Empty<int>(), // v1
        new[] { 6, 18 }, // v2
        new[] { 6, 22 }, // v3
        new[] { 6, 26 }, // v4
        new[] { 6, 30 }, // v5
        new[] { 6, 34 }, // v6
        new[] { 6, 22, 38 }, // v7
        new[] { 6, 24, 42 }, // v8
        new[] { 6, 26, 46 }, // v9
        new[] { 6, 28, 50 }, // v10
    };

    private static void PlaceAlignments(bool[,] m, bool[,] r, int version)
    {
        var positions = AlignmentPositions[version - 1];
        foreach (int row in positions)
        foreach (int col in positions)
        {
            if (r[row, col]) continue; // overlaps finder
            for (int dr = -2; dr <= 2; dr++)
            for (int dc = -2; dc <= 2; dc++)
            {
                bool dark = dr == -2 || dr == 2 || dc == -2 || dc == 2 || (dr == 0 && dc == 0);
                SetReserved(m, r, row + dr, col + dc, dark);
            }
        }
    }

    // ── Dark module ──────────────────────────────────────────────────────────

    private static void PlaceDarkModule(bool[,] m, bool[,] r, int version)
    {
        SetReserved(m, r, 4 * version + 9, 8, true);
    }

    // ── Format information areas ─────────────────────────────────────────────

    private static void ReserveFormatAreas(bool[,] r, int size)
    {
        // Top-left horizontal + vertical
        for (int i = 0; i <= 8; i++)
        {
            r[8, i] = true;
            r[i, 8] = true;
        }
        // Top-right
        for (int i = size - 8; i < size; i++) r[8, i] = true;
        // Bottom-left
        for (int i = size - 7; i < size; i++) r[i, 8] = true;
    }

    // Format info bits lookup (ECL × 3 bits + mask × 3 bits, precomputed)
    private static int FormatInfo(ErrorCorrectionLevel ecl, int mask)
    {
        // ECL encoding: L=01, M=00, Q=11, H=10
        int[] eclBits = { 0b01, 0b00, 0b11, 0b10 };
        int data = (eclBits[(int)ecl] << 3) | mask;
        // BCH error correction for format
        int remainder = data;
        for (int i = 0; i < 10; i++)
            if ((remainder & (1 << (9 - i))) != 0)
                remainder ^= (0x537 << (3 - i));
        // Actually compute correctly: generator = 10100110111
        int gen = 0b10100110111;
        remainder = data << 10;
        for (int i = 14; i >= 10; i--)
            if ((remainder & (1 << i)) != 0)
                remainder ^= gen << (i - 10);
        int formatBits = ((data << 10) | remainder) ^ 0b101010000010010;
        return formatBits;
    }

    private static void PlaceFormatInfo(bool[,] m, ErrorCorrectionLevel ecl, int mask, int size)
    {
        int bits = FormatInfo(ecl, mask);

        // Top-left horizontal (col 0–5, skip 6 timing, col 7–8)
        int[] hPos = { 0, 1, 2, 3, 4, 5, 7, 8 };
        for (int i = 0; i < 8; i++)
            m[8, hPos[i]] = ((bits >> (14 - i)) & 1) == 1;

        // Top-left vertical (row 8–7, skip 6, row 5–0)
        int[] vPos = { 7, 5, 4, 3, 2, 1, 0 };
        m[8, 8] = ((bits >> 7) & 1) == 1;
        for (int i = 0; i < 7; i++)
            m[vPos[i], 8] = ((bits >> (6 - i)) & 1) == 1;

        // Top-right
        for (int i = 0; i < 8; i++)
            m[8, size - 1 - i] = ((bits >> i) & 1) == 1;

        // Bottom-left
        for (int i = 0; i < 7; i++)
            m[size - 7 + i, 8] = ((bits >> (14 - i)) & 1) == 1;
    }

    // ── Data placement ───────────────────────────────────────────────────────

    private static void PlaceData(bool[,] m, bool[,] r, byte[] codewords, int size)
    {
        int bitIdx = 0;
        int totalBits = codewords.Length * 8;

        // Zigzag columns from right to left, skipping column 6 (timing)
        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5; // skip timing column
            bool upward = ((size - 1 - right) / 2) % 2 == 0;

            for (int vert = 0; vert < size; vert++)
            {
                int row = upward ? size - 1 - vert : vert;
                for (int dc = 0; dc < 2; dc++)
                {
                    int col = right - dc;
                    if (r[row, col]) continue;
                    if (bitIdx < totalBits)
                    {
                        int byteIdx = bitIdx / 8;
                        int bitPos = 7 - (bitIdx % 8);
                        m[row, col] = ((codewords[byteIdx] >> bitPos) & 1) == 1;
                        bitIdx++;
                    }
                    else
                    {
                        m[row, col] = false;
                    }
                }
            }
        }
    }

    // ── Masking ──────────────────────────────────────────────────────────────

    private static void ApplyMask(bool[,] m, bool[,] r, int mask, int size)
    {
        for (int row = 0; row < size; row++)
        for (int col = 0; col < size; col++)
        {
            if (r[row, col]) continue;
            bool flip = mask switch
            {
                0 => (row + col) % 2 == 0,
                1 => row % 2 == 0,
                2 => col % 3 == 0,
                3 => (row + col) % 3 == 0,
                4 => (row / 2 + col / 3) % 2 == 0,
                5 => (row * col) % 2 + (row * col) % 3 == 0,
                6 => ((row * col) % 2 + (row * col) % 3) % 2 == 0,
                7 => ((row + col) % 2 + (row * col) % 3) % 2 == 0,
                _ => false
            };
            if (flip) m[row, col] = !m[row, col];
        }
    }

    // ── Penalty scoring ──────────────────────────────────────────────────────

    private static int ComputePenalty(bool[,] m, int size)
    {
        int penalty = 0;

        // Rule 1: 5+ consecutive same-color modules in a row or column
        for (int r = 0; r < size; r++)
        {
            int runH = 1, runV = 1;
            for (int c = 1; c < size; c++)
            {
                if (m[r, c] == m[r, c - 1]) { runH++; if (runH == 5) penalty += 3; else if (runH > 5) penalty++; }
                else runH = 1;
                if (m[c, r] == m[c - 1, r]) { runV++; if (runV == 5) penalty += 3; else if (runV > 5) penalty++; }
                else runV = 1;
            }
        }

        // Rule 2: 2x2 blocks
        for (int r = 0; r < size - 1; r++)
        for (int c = 0; c < size - 1; c++)
            if (m[r, c] == m[r, c + 1] && m[r, c] == m[r + 1, c] && m[r, c] == m[r + 1, c + 1])
                penalty += 3;

        // Rule 3: finder-like patterns
        int[] pattern1 = { 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0 };
        int[] pattern2 = { 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1 };
        for (int r = 0; r < size; r++)
        for (int c = 0; c + 10 < size; c++)
        {
            bool h1 = true, h2 = true, v1 = true, v2 = true;
            for (int i = 0; i <= 10; i++)
            {
                bool hm = m[r, c + i], vm = m[c + i, r];
                if (hm != (pattern1[i] == 1)) h1 = false;
                if (hm != (pattern2[i] == 1)) h2 = false;
                if (vm != (pattern1[i] == 1)) v1 = false;
                if (vm != (pattern2[i] == 1)) v2 = false;
            }
            if (h1 || h2) penalty += 40;
            if (v1 || v2) penalty += 40;
        }

        // Rule 4: dark proportion
        int dark = 0;
        for (int r = 0; r < size; r++)
        for (int c = 0; c < size; c++)
            if (m[r, c]) dark++;
        int total = size * size;
        int pct = dark * 100 / total;
        int prev5 = (pct / 5) * 5;
        int next5 = prev5 + 5;
        penalty += Math.Min(Math.Abs(prev5 - 50) / 5, Math.Abs(next5 - 50) / 5) * 10;

        return penalty;
    }
}
