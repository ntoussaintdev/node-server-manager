using System.IO;
using Microsoft.Data.Sqlite;

namespace DevServerManager;

public static class PortableDatabase
{
    private const string DatabaseName = "dev-server-manager.db";
    public static string DatabasePath { get; } = ResolveDatabasePath(
        AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string ResolveDatabasePath(string executableDirectory, string localApplicationData) =>
        Path.Combine(File.Exists(Path.Combine(executableDirectory, "portable.flag"))
            ? executableDirectory
            : Path.Combine(localApplicationData, "DevServerManager"), DatabaseName);

    public static SqliteConnection OpenConnection() => OpenConnection(
        DatabasePath, Path.Combine(AppContext.BaseDirectory, DatabaseName));

    internal static SqliteConnection OpenConnection(string databasePath, string legacyPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        // SQLite backup includes committed WAL data. Publish the completed import atomically;
        // a failed import must not leave an empty destination that hides the legacy settings.
        if (!File.Exists(databasePath) && File.Exists(legacyPath) &&
            !Path.GetFullPath(databasePath).Equals(Path.GetFullPath(legacyPath), StringComparison.OrdinalIgnoreCase))
        {
            var temporaryPath = databasePath + ".import-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
                       { DataSource = legacyPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
                using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
                       { DataSource = temporaryPath, Pooling = false }.ToString()))
                {
                    source.Open();
                    destination.Open();
                    source.BackupDatabase(destination);
                }
                File.Move(temporaryPath, databasePath);
            }
            finally
            {
                foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                    File.Delete(temporaryPath + suffix);
            }
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS projects (id INTEGER PRIMARY KEY, name TEXT NOT NULL, definition_json TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                INSERT OR IGNORE INTO settings (key, value) VALUES ('theme', 'Device');
                """;
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
