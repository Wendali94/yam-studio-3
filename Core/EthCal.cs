namespace YamStudio.Core;

/// <summary>
/// Single source of truth for Ethiopian dates and clock. Everything stored is ISO Gregorian
/// (yyyy-MM-dd and HH:mm); everything shown goes through this class so screens can never disagree.
/// </summary>
public static class EthCal
{
    public static readonly string[] MonthAm = { "መስከረም", "ጥቅምት", "ኅዳር", "ታኅሣሥ", "ጥር", "የካቲት", "መጋቢት", "ሚያዝያ", "ግንቦት", "ሰኔ", "ሐምሌ", "ነሐሴ", "ጳጉሜን" };
    public static readonly string[] MonthEn = { "Meskerem", "Tikimt", "Hidar", "Tahsas", "Tir", "Yekatit", "Megabit", "Miazia", "Ginbot", "Sene", "Hamle", "Nehase", "Pagume" };
    static readonly string[] WdAm = { "ሰኞ", "ማክሰኞ", "ረቡዕ", "ሐሙስ", "አርብ", "ቅዳሜ", "እሑድ" };
    static readonly string[] WdEn = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public static long GregToJdn(int y, int m, int d)
    {
        int a = (14 - m) / 12, yy = y + 4800 - a, mm = m + 12 * a - 3;
        return d + (153 * mm + 2) / 5 + 365L * yy + yy / 4 - yy / 100 + yy / 400 - 32045;
    }

    public static (int y, int m, int d) JdnToGreg(long j)
    {
        long a = j + 32044, b = (4 * a + 3) / 146097, c = a - 146097 * b / 4;
        long d = (4 * c + 3) / 1461, e = c - 1461 * d / 4, m = (5 * e + 2) / 153;
        int day = (int)(e - (153 * m + 2) / 5 + 1);
        int month = (int)(m + 3 - 12 * (m / 10));
        int year = (int)(100 * b + d - 4800 + m / 10);
        return (year, month, day);
    }

    public static (int y, int m, int d) JdnToEth(long j)
    {
        long r = (j - 1723856) % 1461;
        long n = (r % 365) + 365 * (r / 1460);
        int year = (int)(4 * ((j - 1723856) / 1461) + r / 365 - r / 1460);
        return (year, (int)(n / 30) + 1, (int)(n % 30) + 1);
    }

    public static long EthToJdn(int y, int m, int d) =>
        1723856 + 365 + 365L * (y - 1) + y / 4 + 30 * m + d - 31;

    public static long IsoToJdn(string iso)
    {
        var p = iso.Split('-');
        return GregToJdn(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
    }

    public static string JdnToIso(long j)
    {
        var g = JdnToGreg(j);
        return $"{g.y:0000}-{g.m:00}-{g.d:00}";
    }

    public static string TodayIso() => DateTime.Now.ToString("yyyy-MM-dd");
    public static string NowHm() => DateTime.Now.ToString("HH:mm");
    public static (int y, int m, int d) IsoToEth(string iso) => JdnToEth(IsoToJdn(iso));
    public static string EthToIso(int y, int m, int d) => JdnToIso(EthToJdn(y, m, d));
    public static int DaysInMonth(int y, int m) => m < 13 ? 30 : (y % 4 == 3 ? 6 : 5);

    public static string Date(string iso)
    {
        var e = IsoToEth(iso);
        return $"{e.d} {(Loc.Am ? MonthAm : MonthEn)[e.m - 1]} {e.y}";
    }

    public static string DateWd(string iso)
    {
        int wd = (int)(IsoToJdn(iso) % 7);
        return $"{(Loc.Am ? WdAm : WdEn)[wd]} {Date(iso)}";
    }

    /// <summary>Folder-safe Ethiopian date: 2019-02-15</summary>
    public static string FileDate(string iso)
    {
        var e = IsoToEth(iso);
        return $"{e.y}-{e.m:00}-{e.d:00}";
    }

    public static string Time(string hm)
    {
        var p = hm.Split(':');
        int H = int.Parse(p[0]), M = int.Parse(p[1]);
        int h = (H + 6) % 12; if (h == 0) h = 12;
        string per = H >= 6 && H < 12 ? Loc.T("ጠዋት", "morning")
                   : H >= 12 && H < 18 ? Loc.T("ቀን", "afternoon")
                   : H >= 18 ? Loc.T("ማታ", "evening")
                   : Loc.T("ሌሊት", "night");
        string s = $"{h}:{M:00}";
        return Loc.Am ? $"{per} {s}" : $"{s} {per}";
    }

    /// <summary>Ethiopian month range -> ISO start/end (inclusive) for reports.</summary>
    public static (string from, string to) MonthRange(int ey, int em)
    {
        return (EthToIso(ey, em, 1), EthToIso(ey, em, DaysInMonth(ey, em)));
    }
}
