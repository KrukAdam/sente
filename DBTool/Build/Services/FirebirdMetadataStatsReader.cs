using FirebirdSql.Data.FirebirdClient;
using DBTool.Config;

namespace DBTool.Build.Services;

public interface IMetadataStatsReader
{
    MetadataStats Read(string connectionString);

    /// <summary>
    /// Wygodna metoda do build-db: bierze ścieżkę do pliku .fdb i sama buduje connection string
    /// z ENV + FirebirdDefaults.
    /// </summary>
    MetadataStats ReadForDatabaseFile(string databaseFilePath);
}

public sealed record MetadataStats(int Domains, int Tables, int Procedures);

public sealed class FirebirdMetadataStatsReader : IMetadataStatsReader
{
    public MetadataStats ReadForDatabaseFile(string databaseFilePath)
    {
        if (string.IsNullOrWhiteSpace(databaseFilePath))
            throw new ArgumentException("databaseFilePath is required.", nameof(databaseFilePath));

        var connStr = BuildConnectionStringForDatabaseFile(databaseFilePath);
        return Read(connStr);
    }

    public MetadataStats Read(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("connectionString is required.", nameof(connectionString));

        using var connection = new FbConnection(connectionString);
        connection.Open();

        var domains = CountDomains(connection);
        var tables = CountTables(connection);
        var procedures = CountProcedures(connection);

        return new MetadataStats(domains, tables, procedures);
    }

    private static string BuildConnectionStringForDatabaseFile(string databaseFilePath)
    {
        var host = Environment.GetEnvironmentVariable(FirebirdDefaults.EnvHost) ?? FirebirdDefaults.Host;

        var portRaw = Environment.GetEnvironmentVariable(FirebirdDefaults.EnvPort);
        var port = int.TryParse(portRaw, out var p) ? p : FirebirdDefaults.Port;

        var user = Environment.GetEnvironmentVariable(FirebirdDefaults.EnvUser) ?? FirebirdDefaults.User;

        var pass = Environment.GetEnvironmentVariable(FirebirdDefaults.EnvPassword);
        if (string.IsNullOrWhiteSpace(pass))
            throw new InvalidOperationException($"Missing required env var: {FirebirdDefaults.EnvPassword}");

        var csb = new FbConnectionStringBuilder
        {
            DataSource = host,
            Port = port,
            Database = databaseFilePath,
            UserID = user,
            Password = pass,
            Dialect = FirebirdDefaults.DefaultDialect,
            Charset = FirebirdDefaults.DefaultCharset
        };

        return csb.ToString();
    }

    private static int CountDomains(FbConnection connection)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM RDB$FIELDS f
            WHERE COALESCE(f.RDB$SYSTEM_FLAG, 1) = 0
        """;

        return ExecuteScalarInt(connection, sql);
    }

    private static int CountTables(FbConnection connection)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM RDB$RELATIONS r
            WHERE COALESCE(r.RDB$SYSTEM_FLAG, 1) = 0
              AND r.RDB$VIEW_BLR IS NULL
        """;

        return ExecuteScalarInt(connection, sql);
    }

    private static int CountProcedures(FbConnection connection)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM RDB$PROCEDURES p
            WHERE COALESCE(p.RDB$SYSTEM_FLAG, 1) = 0
        """;

        return ExecuteScalarInt(connection, sql);
    }

    private static int ExecuteScalarInt(FbConnection connection, string sql)
    {
        using var cmd = new FbCommand(sql, connection);
        var result = cmd.ExecuteScalar();

        if (result is null || result is DBNull)
            return 0;

        return Convert.ToInt32(result);
    }
}
