using Microsoft.Data.Sqlite;

namespace YamStudio.Core;

public class PaymentRow
{
    public string Date { get; set; } = "";
    public string ClientName { get; set; } = "";
    public int JobType { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>SQLite storage. Lives only on PC1 (host). One short connection per call, guarded by a lock.</summary>
public class Db
{
    readonly string _path;
    readonly object _lock = new();

    public string FilePath => _path;

    public Db(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Exec(@"
CREATE TABLE IF NOT EXISTS clients(
  id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, phone TEXT NOT NULL, type INTEGER NOT NULL DEFAULT 0,
  date TEXT NOT NULL, time TEXT NOT NULL, price REAL NOT NULL DEFAULT 0, status TEXT NOT NULL DEFAULT 'booked',
  notes TEXT NOT NULL DEFAULT '', folder TEXT NOT NULL DEFAULT '');
CREATE TABLE IF NOT EXISTS payments(
  id INTEGER PRIMARY KEY AUTOINCREMENT, client_id INTEGER NOT NULL, amount REAL NOT NULL, date TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS photos(
  id INTEGER PRIMARY KEY AUTOINCREMENT, client_id INTEGER NOT NULL, base TEXT NOT NULL,
  jpg TEXT NOT NULL DEFAULT '', raw TEXT NOT NULL DEFAULT '', selected INTEGER NOT NULL DEFAULT 0, added_at TEXT NOT NULL DEFAULT '');
CREATE INDEX IF NOT EXISTS ix_photos_client ON photos(client_id);
CREATE INDEX IF NOT EXISTS ix_pay_client ON payments(client_id);
CREATE INDEX IF NOT EXISTS ix_clients_date ON clients(date);");
    }

    SqliteConnection Open()
    {
        var c = new SqliteConnection($"Data Source={_path};Pooling=False");
        c.Open();
        return c;
    }

    void Exec(string sql, params (string, object)[] ps)
    {
        lock (_lock)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string, object)[] ps)
    {
        lock (_lock)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            using var r = cmd.ExecuteReader();
            var list = new List<T>();
            while (r.Read()) list.Add(map(r));
            return list;
        }
    }

    long Insert(string sql, params (string, object)[] ps)
    {
        lock (_lock)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql + "; SELECT last_insert_rowid();";
            foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }

    static Client MapClient(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0), Name = r.GetString(1), Phone = r.GetString(2), Type = r.GetInt32(3),
        Date = r.GetString(4), Time = r.GetString(5), Price = (decimal)r.GetDouble(6),
        Status = r.GetString(7), Notes = r.GetString(8), Folder = r.GetString(9),
        PhotoCount = r.GetInt32(10), SelectedCount = r.GetInt32(11)
    };

    const string ClientSelect = @"SELECT c.id,c.name,c.phone,c.type,c.date,c.time,c.price,c.status,c.notes,c.folder,
 (SELECT COUNT(*) FROM photos p WHERE p.client_id=c.id),
 (SELECT COUNT(*) FROM photos p WHERE p.client_id=c.id AND p.selected=1)
 FROM clients c";

    public List<Client> GetClients()
    {
        var list = Query(ClientSelect + " ORDER BY c.date DESC, c.time DESC", MapClient);
        var pays = Query("SELECT id,client_id,amount,date FROM payments ORDER BY id",
            r => new Payment { Id = r.GetInt32(0), ClientId = r.GetInt32(1), Amount = (decimal)r.GetDouble(2), Date = r.GetString(3) });
        var by = pays.GroupBy(p => p.ClientId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var c in list) if (by.TryGetValue(c.Id, out var l)) c.Payments = l;
        return list;
    }

    public Client GetClient(int id)
    {
        var c = Query(ClientSelect + " WHERE c.id=$i", MapClient, ("$i", id)).FirstOrDefault();
        if (c == null) return null;
        c.Payments = Query("SELECT id,client_id,amount,date FROM payments WHERE client_id=$i ORDER BY id",
            r => new Payment { Id = r.GetInt32(0), ClientId = r.GetInt32(1), Amount = (decimal)r.GetDouble(2), Date = r.GetString(3) },
            ("$i", id));
        return c;
    }

    public int SaveClient(Client c)
    {
        if (c.Id == 0)
        {
            int id = (int)Insert(@"INSERT INTO clients(name,phone,type,date,time,price,status,notes,folder)
VALUES($n,$p,$t,$d,$tm,$pr,$s,$no,$f)",
                ("$n", c.Name), ("$p", c.Phone), ("$t", c.Type), ("$d", c.Date), ("$tm", c.Time),
                ("$pr", (double)c.Price), ("$s", c.Status), ("$no", c.Notes), ("$f", c.Folder));
            foreach (var p in c.Payments) AddPayment(id, p.Amount, p.Date);
            return id;
        }
        Exec(@"UPDATE clients SET name=$n,phone=$p,type=$t,date=$d,time=$tm,price=$pr,notes=$no WHERE id=$id",
            ("$n", c.Name), ("$p", c.Phone), ("$t", c.Type), ("$d", c.Date), ("$tm", c.Time),
            ("$pr", (double)c.Price), ("$no", c.Notes), ("$id", c.Id));
        return c.Id;
    }

    public void DeleteClient(int id)
    {
        Exec("DELETE FROM photos WHERE client_id=$id", ("$id", id));
        Exec("DELETE FROM payments WHERE client_id=$id", ("$id", id));
        Exec("DELETE FROM clients WHERE id=$id", ("$id", id));
    }

    public void SetStatus(int id, string status) => Exec("UPDATE clients SET status=$s WHERE id=$id", ("$s", status), ("$id", id));
    public void SetFolder(int id, string folder) => Exec("UPDATE clients SET folder=$f WHERE id=$id", ("$f", folder), ("$id", id));

    public void AddPayment(int clientId, decimal amount, string date) =>
        Exec("INSERT INTO payments(client_id,amount,date) VALUES($c,$a,$d)", ("$c", clientId), ("$a", (double)amount), ("$d", date));

    public List<PaymentRow> GetPaymentsBetween(string fromIso, string toIso) =>
        Query(@"SELECT p.date,c.name,c.type,p.amount FROM payments p JOIN clients c ON c.id=p.client_id
WHERE p.date>=$f AND p.date<=$t ORDER BY p.date",
            r => new PaymentRow { Date = r.GetString(0), ClientName = r.GetString(1), JobType = r.GetInt32(2), Amount = (decimal)r.GetDouble(3) },
            ("$f", fromIso), ("$t", toIso));

    static Photo MapPhoto(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0), ClientId = r.GetInt32(1), Base = r.GetString(2), JpgFile = r.GetString(3),
        RawFile = r.GetString(4), Selected = r.GetInt32(5) == 1, AddedAt = r.GetString(6)
    };

    public List<Photo> GetPhotos(int clientId) =>
        Query("SELECT id,client_id,base,jpg,raw,selected,added_at FROM photos WHERE client_id=$c ORDER BY base, id", MapPhoto, ("$c", clientId));

    public Photo GetPhoto(int id) =>
        Query("SELECT id,client_id,base,jpg,raw,selected,added_at FROM photos WHERE id=$i", MapPhoto, ("$i", id)).FirstOrDefault();

    /// <summary>Adds a file to the client's photo list; JPG and RAW with the same base name become one photo.</summary>
    public Photo UpsertPhoto(int clientId, string fileName)
    {
        string b = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        bool isRaw = ext == ".arw" || ext == ".dng" || ext == ".cr2" || ext == ".nef";
        var ex = Query("SELECT id,client_id,base,jpg,raw,selected,added_at FROM photos WHERE client_id=$c AND base=$b",
            MapPhoto, ("$c", clientId), ("$b", b)).FirstOrDefault();
        if (ex == null)
        {
            long id = Insert("INSERT INTO photos(client_id,base,jpg,raw,selected,added_at) VALUES($c,$b,$j,$r,0,$a)",
                ("$c", clientId), ("$b", b), ("$j", isRaw ? "" : fileName), ("$r", isRaw ? fileName : ""),
                ("$a", DateTime.Now.ToString("s")));
            return GetPhoto((int)id);
        }
        if (isRaw) Exec("UPDATE photos SET raw=$f WHERE id=$i", ("$f", fileName), ("$i", ex.Id));
        else Exec("UPDATE photos SET jpg=$f WHERE id=$i", ("$f", fileName), ("$i", ex.Id));
        return GetPhoto(ex.Id);
    }

    public void SetSelected(int photoId, bool sel) =>
        Exec("UPDATE photos SET selected=$s WHERE id=$i", ("$s", sel ? 1 : 0), ("$i", photoId));

    /// <summary>Consistent copy of the database using SQLite's own backup (safe while the app is running).</summary>
    public void BackupTo(string destFile)
    {
        if (File.Exists(destFile)) File.Delete(destFile);
        lock (_lock)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"VACUUM INTO '{destFile.Replace("'", "''")}'";
            cmd.ExecuteNonQuery();
        }
    }
}
