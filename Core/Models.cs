namespace YamStudio.Core;

public static class JobStatus
{
    public const string Booked = "booked";
    public const string Shot = "shot";
    public const string Selected = "selected";
    public const string Editing = "editing";
    public const string Review = "review";
    public const string Delivered = "delivered";
    public static readonly string[] All = { Booked, Shot, Selected, Editing, Review, Delivered };

    public static string Label(string s) => s switch
    {
        Booked => Loc.T("ቀጠሮ", "Booked"),
        Shot => Loc.T("ተነስቷል", "Shot"),
        Selected => Loc.T("ምርጫ ተጠናቋል", "Selected"),
        Editing => Loc.T("በኤዲቲንግ ላይ", "Editing"),
        Review => Loc.T("ለማረጋገጫ", "In review"),
        Delivered => Loc.T("ተረክቧል", "Delivered"),
        _ => s
    };
}

public static class JobTypes
{
    public static readonly (string am, string en)[] All =
    {
        ("ሠርግ", "Wedding"),
        ("ቀለበት/ኢንጌጅመንት", "Engagement"),
        ("የልደት", "Birthday"),
        ("ስቱዲዮ ፎቶ", "Studio portrait"),
        ("ምርቃት", "Graduation"),
        ("የንግድ/ማስታወቂያ", "Commercial"),
        ("ሌላ", "Other"),
    };
    public static string Name(int i) => i >= 0 && i < All.Length ? Loc.T(All[i].am, All[i].en) : "";
}

public class Payment
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public decimal Amount { get; set; }
    public string Date { get; set; } = "";
}

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public int Type { get; set; }
    public string Date { get; set; } = "";   // ISO Gregorian
    public string Time { get; set; } = "09:00"; // HH:mm 24h
    public decimal Price { get; set; }
    public string Status { get; set; } = JobStatus.Booked;
    public string Notes { get; set; } = "";
    public string Folder { get; set; } = "";
    public List<Payment> Payments { get; set; } = new();
    public int PhotoCount { get; set; }
    public int SelectedCount { get; set; }
    public decimal Paid => Payments.Sum(p => p.Amount);
    public decimal Balance => Price - Paid;
}

public class Photo
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Base { get; set; } = "";       // file name without extension
    public string JpgFile { get; set; } = "";    // file name inside Originals, or ""
    public string RawFile { get; set; } = "";    // file name inside Originals, or ""
    public bool Selected { get; set; }
    public string AddedAt { get; set; } = "";
    public string DisplayName => !string.IsNullOrEmpty(JpgFile) ? JpgFile : RawFile;
}

public class AppSettings
{
    public string Mode { get; set; } = "";            // "host" (PC1) or "client" (PC2); empty = first run
    public string Lang { get; set; } = "am";
    public string RootDir { get; set; } = @"C:\YamStudio";   // PC1: all data lives here
    public string BackupDir { get; set; } = "";               // second disk or \\PC2\Backup
    public string BackupTime { get; set; } = "22:00";
    public bool BackupOnExit { get; set; } = true;
    public string WatchDir { get; set; } = "";                // optional extra folder to watch (e.g. Imaging Edge output)
    public int FtpPort { get; set; } = 2121;
    public string FtpUser { get; set; } = "yam";
    public string FtpPass { get; set; } = "yam123";
    public int ApiPort { get; set; } = 5810;
    public string Pin { get; set; } = "";
    public string HostAddress { get; set; } = "";             // PC2: IP of PC1, e.g. 192.168.1.10
    public string WorkDir { get; set; } = @"C:\YamWork";      // PC2: local work folder
    public int ActiveClientId { get; set; }
    public string LastBackup { get; set; } = "";
}
