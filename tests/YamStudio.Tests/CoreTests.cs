using YamStudio.Core;
using Xunit;

namespace YamStudio.Tests;

public class EthCalTests
{
    [Theory]
    [InlineData("2026-10-25", 2019, 2, 15)]   // 15 Tikimt 2019
    [InlineData("2026-09-11", 2019, 1, 1)]    // Enkutatash 2019
    [InlineData("2026-09-10", 2018, 13, 5)]   // last day of Pagume 2018
    [InlineData("2027-01-07", 2019, 4, 29)]   // Genna (29 Tahsas)
    public void GregorianToEthiopian(string iso, int y, int m, int d)
    {
        var e = EthCal.IsoToEth(iso);
        Assert.Equal((y, m, d), (e.y, e.m, e.d));
    }

    [Fact]
    public void RoundTripEveryDay()
    {
        for (long j = EthCal.GregToJdn(2020, 1, 1); j < EthCal.GregToJdn(2040, 1, 1); j++)
        {
            var e = EthCal.JdnToEth(j);
            Assert.Equal(j, EthCal.EthToJdn(e.y, e.m, e.d));
            Assert.InRange(e.d, 1, EthCal.DaysInMonth(e.y, e.m));
        }
    }

    [Fact]
    public void LeapPagumeHasSixDays()
    {
        Assert.Equal(6, EthCal.DaysInMonth(2019, 13));
        Assert.Equal(5, EthCal.DaysInMonth(2018, 13));
        Assert.Equal(30, EthCal.DaysInMonth(2019, 5));
    }

    [Fact]
    public void EthiopianClock()
    {
        Loc.Lang = "am";
        Assert.Equal("ጠዋት 12:00", EthCal.Time("06:00"));
        Assert.Equal("ጠዋት 3:30", EthCal.Time("09:30"));
        Assert.Equal("ቀን 6:00", EthCal.Time("12:00"));
        Assert.Equal("ማታ 12:30", EthCal.Time("18:30"));
        Assert.Equal("ሌሊት 6:00", EthCal.Time("00:00"));
        Loc.Lang = "en";
        Assert.Equal("3:30 morning", EthCal.Time("09:30"));
        Loc.Lang = "am";
    }

    [Fact]
    public void FolderNameUsesEthiopianDateAndPhone()
    {
        var c = new Client { Name = "ሄኖክ አበበ", Phone = "0911223344", Date = "2026-10-25" };
        Assert.Equal("ሄኖክ አበበ_0911223344_2019-02-15", LocalStudio.FolderNameFor(c));
    }
}

public class DbTests
{
    [Fact]
    public void RawAndJpgBecomeOnePhoto_AndSelectionIsCounted()
    {
        string dir = Path.Combine(Path.GetTempPath(), "yamtest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new Db(Path.Combine(dir, "t.db"));
            int id = db.SaveClient(new Client { Name = "Test", Phone = "0900000000", Date = "2026-10-25", Time = "09:00", Price = 1000 });
            db.AddPayment(id, 400, "2026-10-25");
            var p1 = db.UpsertPhoto(id, "DSC00001.ARW");
            var p2 = db.UpsertPhoto(id, "DSC00001.JPG");
            db.UpsertPhoto(id, "DSC00002.JPG");
            Assert.Equal(p1.Id, p2.Id);
            var list = db.GetPhotos(id);
            Assert.Equal(2, list.Count);
            Assert.Equal("DSC00001.ARW", list[0].RawFile);
            Assert.Equal("DSC00001.JPG", list[0].JpgFile);
            db.SetSelected(list[0].Id, true);
            var c = db.GetClient(id);
            Assert.Equal(2, c.PhotoCount);
            Assert.Equal(1, c.SelectedCount);
            Assert.Equal(600, c.Balance);
            db.SetStatus(id, JobStatus.Selected);
            Assert.Equal(JobStatus.Selected, db.GetClient(id).Status);
            db.BackupTo(Path.Combine(dir, "copy.db"));
            Assert.True(File.Exists(Path.Combine(dir, "copy.db")));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}

public class RawPreviewTests
{
    // SOI, APP0 (len 4), SOS (len 2), entropy data without 0xFF, EOI: a valid marker stream
    static byte[] FakeJpeg(int size)
    {
        var b = new byte[size];
        byte[] head = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xDA, 0x00, 0x02 };
        Array.Copy(head, b, head.Length);
        for (int i = head.Length; i < size - 2; i++) b[i] = 0x11;
        b[size - 2] = 0xFF; b[size - 1] = 0xD9;
        return b;
    }

    [Fact]
    public void FindsEmbeddedJpegInsideBinaryBlob()
    {
        var jpeg = FakeJpeg(50_000);
        var file = new byte[200_000];
        Array.Copy(jpeg, 0, file, 70_000, jpeg.Length);
        string p = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(p, file);
            var got = RawPreview.ExtractLargestJpeg(p);
            Assert.NotNull(got);
            Assert.Equal(jpeg.Length, got.Length);
        }
        finally { File.Delete(p); }
    }
}
