namespace YamStudio.Core;

/// <summary>
/// Pulls the full-size JPEG preview out of a Sony ARW (TIFF-based) file so photos show instantly,
/// without needing any RAW codec. Falls back to scanning for the largest JPEG stream in the file.
/// </summary>
public static class RawPreview
{
    public static byte[] ExtractLargestJpeg(string path)
    {
        byte[] data;
        try { data = File.ReadAllBytes(path); } catch { return null; }
        byte[] best = null;
        try { best = FromTiff(data); } catch { }
        if (best == null || best.Length < 20000) // too small = just a thumbnail; try scanning
        {
            var scan = Scan(data);
            if (scan != null && (best == null || scan.Length > best.Length)) best = scan;
        }
        return best;
    }

    static ushort U16(byte[] d, int o, bool le) => le ? (ushort)(d[o] | d[o + 1] << 8) : (ushort)(d[o] << 8 | d[o + 1]);
    static uint U32(byte[] d, int o, bool le) => le
        ? (uint)(d[o] | d[o + 1] << 8 | d[o + 2] << 16 | d[o + 3] << 24)
        : (uint)(d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3]);

    static byte[] FromTiff(byte[] d)
    {
        if (d.Length < 16) return null;
        bool le = d[0] == 'I' && d[1] == 'I';
        if (!le && !(d[0] == 'M' && d[1] == 'M')) return null;
        var candidates = new List<(int off, int len)>();
        var seen = new HashSet<int>();
        void Walk(int ifd, int depth)
        {
            if (depth > 6 || ifd <= 0 || ifd + 2 > d.Length || !seen.Add(ifd)) return;
            int n = U16(d, ifd, le);
            if (n > 500 || ifd + 2 + n * 12 > d.Length) return;
            int jOff = 0, jLen = 0, stripOff = 0, stripLen = 0, comp = 0;
            var subs = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int e = ifd + 2 + i * 12;
                ushort tag = U16(d, e, le), type = U16(d, e + 2, le);
                uint cnt = U32(d, e + 4, le);
                uint val = type == 3 && cnt == 1 ? U16(d, e + 8, le) : U32(d, e + 8, le);
                switch (tag)
                {
                    case 0x0201: jOff = (int)val; break;
                    case 0x0202: jLen = (int)val; break;
                    case 0x0103: comp = (int)val; break;
                    case 0x0111: if (cnt == 1) stripOff = (int)val; break;
                    case 0x0117: if (cnt == 1) stripLen = (int)val; break;
                    case 0x014A:
                        if (cnt == 1) subs.Add((int)val);
                        else for (int k = 0; k < cnt && k < 8; k++)
                        {
                            int p = (int)val + k * 4;
                            if (p + 4 <= d.Length) subs.Add((int)U32(d, p, le));
                        }
                        break;
                }
            }
            if (jOff > 0 && jLen > 0) candidates.Add((jOff, jLen));
            if ((comp == 6 || comp == 7) && stripOff > 0 && stripLen > 0) candidates.Add((stripOff, stripLen));
            foreach (var s in subs) Walk(s, depth + 1);
            int nextPos = ifd + 2 + n * 12;
            if (nextPos + 4 <= d.Length) Walk((int)U32(d, nextPos, le), depth + 1);
        }
        Walk((int)U32(d, 4, le), 0);
        byte[] best = null;
        foreach (var (off, len) in candidates)
        {
            if (off <= 0 || len <= 4 || (long)off + len > d.Length) continue;
            if (d[off] != 0xFF || d[off + 1] != 0xD8) continue;
            if (best == null || len > best.Length) { best = new byte[len]; Buffer.BlockCopy(d, off, best, 0, len); }
        }
        return best;
    }

    /// <summary>Walks JPEG markers from an SOI so embedded thumbnails inside APP segments do not end the stream early.</summary>
    static int JpegEnd(byte[] d, int start)
    {
        int p = start + 2;
        while (p + 4 <= d.Length)
        {
            if (d[p] != 0xFF) return -1;
            byte m = d[p + 1];
            if (m == 0xD9) return p + 2;
            if (m == 0xFF) { p++; continue; }
            if (m == 0x01 || (m >= 0xD0 && m <= 0xD7)) { p += 2; continue; }
            int len = d[p + 2] << 8 | d[p + 3];
            p += 2 + len;
            if (m == 0xDA)
            {
                while (p + 1 < d.Length)
                {
                    if (d[p] == 0xFF)
                    {
                        byte n = d[p + 1];
                        if (n == 0x00 || (n >= 0xD0 && n <= 0xD7)) { p += 2; continue; }
                        break;
                    }
                    p++;
                }
            }
        }
        return -1;
    }

    static byte[] Scan(byte[] d)
    {
        byte[] best = null;
        int limit = Math.Min(d.Length - 4, 60_000_000);
        for (int i = 0; i < limit; i++)
        {
            if (d[i] != 0xFF || d[i + 1] != 0xD8 || d[i + 2] != 0xFF) continue;
            int end = JpegEnd(d, i);
            if (end > i)
            {
                if (best == null || end - i > best.Length)
                {
                    best = new byte[end - i];
                    Buffer.BlockCopy(d, i, best, 0, best.Length);
                }
                i = end - 1;
            }
        }
        return best;
    }
}
