using FFPMobile.Shared.WarehouseProtocol;
using Microsoft.Data.Sqlite;

namespace FFPMobile.Api;

public sealed record RegisteredDevice(DeviceDetails Details, byte[] PublicKey, long Version);

public sealed class DeviceStore(string connectionString, TimeProvider clock)
{
    public void Initialize()
    {
        using var db = Open();
        using var command = Command(db, """
            CREATE TABLE IF NOT EXISTS mobile_devices (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, application TEXT NOT NULL, public_key BLOB NOT NULL,
                registered INTEGER NOT NULL, last_connection INTEGER, status INTEGER NOT NULL, version INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS mobile_device_nonces (
                device_id TEXT NOT NULL, nonce TEXT NOT NULL, expiry INTEGER NOT NULL, PRIMARY KEY(device_id, nonce));
            CREATE INDEX IF NOT EXISTS ix_mobile_device_nonce_expiry ON mobile_device_nonces(expiry);
            CREATE TABLE IF NOT EXISTS mobile_device_audit (
                id INTEGER PRIMARY KEY, device_id TEXT NOT NULL, actor TEXT NOT NULL, action TEXT NOT NULL,
                occurred INTEGER NOT NULL);
            """);
        command.ExecuteNonQuery();
    }

    public RegisteredDevice? Find(string id)
    {
        using var db = Open();
        using var command = Command(db, Select + " WHERE id=$id", ("$id", id));
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<DeviceDetails> List(string after)
    {
        using var db = Open();
        using var command = Command(db, Select + " WHERE id > $after ORDER BY id LIMIT 100", ("$after", after));
        using var reader = command.ExecuteReader();
        var result = new List<DeviceDetails>();
        while (reader.Read()) result.Add(Read(reader).Details);
        return result;
    }

    public RegisteredDevice Register(string id, string name, string application, byte[] key)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        long now = clock.GetUtcNow().ToUnixTimeSeconds();
        using var command = Command(db, """
            INSERT OR IGNORE INTO mobile_devices
            (id,name,application,public_key,registered,status,version) VALUES ($id,$name,$app,$key,$now,0,0)
            """, ("$id", id), ("$name", name), ("$app", application), ("$key", key), ("$now", now));
        command.Transaction = tx;
        if (command.ExecuteNonQuery() == 1) Audit(db, tx, id, id, "Registered", now);
        tx.Commit();
        return Find(id)!;
    }

    public bool ConsumeNonce(string id, string nonce, long expiration)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var purge = Command(db, "DELETE FROM mobile_device_nonces WHERE expiry < $now",
            ("$now", clock.GetUtcNow().ToUnixTimeSeconds()));
        purge.Transaction = tx;
        purge.ExecuteNonQuery();
        using var insert = Command(db, """
            INSERT OR IGNORE INTO mobile_device_nonces (device_id,nonce,expiry) VALUES ($id,$nonce,$expiry)
            """, ("$id", id), ("$nonce", nonce), ("$expiry", expiration));
        insert.Transaction = tx;
        bool fresh = insert.ExecuteNonQuery() == 1;
        tx.Commit();
        return fresh;
    }

    public bool ChangeStatus(string id, DeviceStatus status, string actor)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var command = Command(db, """
            UPDATE mobile_devices SET status=$status, version=version+1
            WHERE id=$id AND status<>3 AND status<>$status
            """, ("$id", id), ("$status", (int)status));
        command.Transaction = tx;
        if (command.ExecuteNonQuery() == 0) return false;
        Audit(db, tx, id, actor, status.ToString(), clock.GetUtcNow().ToUnixTimeSeconds());
        tx.Commit();
        return true;
    }

    public bool RecordConnection(string id, long version)
    {
        using var db = Open();
        using var command = Command(db, """
            UPDATE mobile_devices SET last_connection=$now WHERE id=$id AND version=$version AND status=1
            """, ("$id", id), ("$version", version), ("$now", clock.GetUtcNow().ToUnixTimeSeconds()));
        return command.ExecuteNonQuery() == 1;
    }

    private const string Select = "SELECT id,name,application,registered,last_connection,status,public_key,version FROM mobile_devices";

    private static RegisteredDevice Read(SqliteDataReader reader) => new(
        new(reader.GetString(0), reader.GetString(1), reader.GetString(2), DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
            reader.IsDBNull(4) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)), (DeviceStatus)reader.GetInt32(5)),
        reader.GetFieldValue<byte[]>(6), reader.GetInt64(7));

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        db.Open();
        return db;
    }

    private static void Audit(SqliteConnection db, SqliteTransaction tx, string id, string actor, string action, long at)
    {
        using var command = Command(db, """
            INSERT INTO mobile_device_audit (device_id,actor,action,occurred) VALUES ($id,$actor,$action,$time)
            """, ("$id", id), ("$actor", actor), ("$action", action), ("$time", at));
        command.Transaction = tx;
        command.ExecuteNonQuery();
    }

    private static SqliteCommand Command(SqliteConnection db, string sql, params (string Name, object Value)[] values)
    {
        var command = db.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return command;
    }
}
