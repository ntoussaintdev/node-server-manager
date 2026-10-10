using DevServerManager;
using Microsoft.Data.Sqlite;

var root = Path.Combine(Path.GetTempPath(), "DevServerManager-checks-" + Guid.NewGuid().ToString("N"));
try
{
    var executableDirectory = Path.Combine(root, "portable");
    var localData = Path.Combine(root, "localdata");
    Directory.CreateDirectory(executableDirectory);
    var installedPath = Path.Combine(localData, "DevServerManager", "dev-server-manager.db");
    Assert(PortableDatabase.ResolveDatabasePath(executableDirectory, localData) == installedPath, "Installed mode uses AppData");
    File.WriteAllText(Path.Combine(executableDirectory, "portable.flag"), "");
    var legacyPath = PortableDatabase.ResolveDatabasePath(executableDirectory, localData);
    Assert(legacyPath == Path.Combine(executableDirectory, "dev-server-manager.db"), "Portable mode uses executable folder");

    using (var source = PortableDatabase.OpenConnection(legacyPath, legacyPath))
    {
        Execute(source, "PRAGMA journal_mode=WAL; UPDATE settings SET value='Dark' WHERE key='theme'; INSERT INTO projects(name, definition_json) VALUES ('Fixture', '{}');");
        // Keep the connection alive during import to exercise committed WAL data.
        using var imported = PortableDatabase.OpenConnection(installedPath, legacyPath);
        Assert(Scalar(imported, "SELECT value FROM settings WHERE key='theme'") == "Dark", "Migration preserves settings, including WAL");
        Assert(Scalar(imported, "SELECT name FROM projects") == "Fixture", "Migration preserves projects");
        Execute(imported, "UPDATE settings SET value='Light' WHERE key='theme'");
    }
    using (var reopened = PortableDatabase.OpenConnection(installedPath, legacyPath))
        Assert(Scalar(reopened, "SELECT value FROM settings WHERE key='theme'") == "Light", "Existing installed settings are never overwritten");
    Assert(File.Exists(legacyPath), "Legacy database remains available");

    SqliteConnection.ClearAllPools();
    var movedDirectory = Path.Combine(root, "moved");
    Directory.Move(executableDirectory, movedDirectory);
    var movedPath = PortableDatabase.ResolveDatabasePath(movedDirectory, localData);
    using (var moved = PortableDatabase.OpenConnection(movedPath, movedPath))
        Assert(Scalar(moved, "SELECT value FROM settings WHERE key='theme'") == "Dark", "Portable configuration survives moving the folder");

    var cleanPath = Path.Combine(root, "fresh", "dev-server-manager.db");
    using (var fresh = PortableDatabase.OpenConnection(cleanPath, cleanPath))
        Assert(Scalar(fresh, "SELECT value FROM settings WHERE key='theme'") == "Device", "Fresh database initializes default theme");

    var invalidLegacy = Path.Combine(root, "invalid.db");
    File.WriteAllText(invalidLegacy, "This is not SQLite");
    var failedImportPath = Path.Combine(root, "failed", "dev-server-manager.db");
    try
    {
        using var unexpected = PortableDatabase.OpenConnection(failedImportPath, invalidLegacy);
        throw new Exception("Invalid database import unexpectedly succeeded.");
    }
    catch (SqliteException) { }
    Assert(!File.Exists(failedImportPath), "Failed import leaves no destination database");
    Assert(!Directory.EnumerateFiles(Path.GetDirectoryName(failedImportPath)!).Any(), "Failed import cleans temporary files");
    Console.WriteLine("All packaging storage checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static void Assert(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine("PASS: " + description);
}
static void Execute(SqliteConnection connection, string sql)
{
    using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
}
static string? Scalar(SqliteConnection connection, string sql)
{
    using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar()?.ToString();
}
