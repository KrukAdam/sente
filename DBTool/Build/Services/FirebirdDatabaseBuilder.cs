using FirebirdSql.Data.FirebirdClient;
using DBTool.Config;
using DBTool.Update.Services;

namespace DBTool.Build.Services;

public interface IDatabaseBuilder
{
    string Build(string databaseDirectory, string scriptsDirectory);
}

public sealed class FirebirdDatabaseBuilder : IDatabaseBuilder
{
    private readonly ISqlScriptExecutor _executor;

    public FirebirdDatabaseBuilder(ISqlScriptExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public string Build(string databaseDirectory, string scriptsDirectory)
    {
        if (string.IsNullOrWhiteSpace(databaseDirectory))
            throw new ArgumentException("databaseDirectory is required.", nameof(databaseDirectory));

        if (string.IsNullOrWhiteSpace(scriptsDirectory))
            throw new ArgumentException("scriptsDirectory is required.", nameof(scriptsDirectory));

        if (!Directory.Exists(scriptsDirectory))
            throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsDirectory}");

        Directory.CreateDirectory(databaseDirectory);

        var dbPath = Path.Combine(databaseDirectory, FirebirdDefaults.DefaultDatabaseFileName);

        if (File.Exists(dbPath))
            throw new InvalidOperationException(
                $"Database file already exists: {dbPath}. Usuń plik albo wskaż pusty katalog.");

        var connectionString = BuildConnectionString(dbPath);

        try
        {
            FbConnection.CreateDatabase(connectionString);

            _executor.ExecuteDirectory(connectionString, scriptsDirectory);

            return dbPath;
        }
        catch
        {
            try
            {
                if (File.Exists(dbPath))
                    File.Delete(dbPath);
            }
            catch { /* ignore */ }

            throw;
        }
    }

    private static string BuildConnectionString(string databaseFilePath)
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
}
