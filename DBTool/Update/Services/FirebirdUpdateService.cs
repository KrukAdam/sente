using DBTool.Build.Services;
using DBTool.Config;
using DBTool.Export.Models;
using DBTool.Export.Services;
using FirebirdSql.Data.FirebirdClient;

namespace DBTool.Update.Services;

public interface IUpdateService
{
    void Update(string connectionString, string scriptsDirectory);
}

public sealed class FirebirdUpdateService : IUpdateService
{
    private readonly IMetadataReader _metadataReader;
    private readonly IDatabaseBuilder _databaseBuilder;
    private readonly IFirebirdSchemaDiffService _diffService;
    private readonly ISqlScriptExecutor _scriptExecutor;

    public FirebirdUpdateService(
        IMetadataReader metadataReader,
        IDatabaseBuilder databaseBuilder,
        IFirebirdSchemaDiffService diffService,
        ISqlScriptExecutor scriptExecutor)
    {
        _metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
        _databaseBuilder = databaseBuilder ?? throw new ArgumentNullException(nameof(databaseBuilder));
        _diffService = diffService ?? throw new ArgumentNullException(nameof(diffService));
        _scriptExecutor = scriptExecutor ?? throw new ArgumentNullException(nameof(scriptExecutor));
    }

    public void Update(string connectionString, string scriptsDirectory)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("connectionString is required.", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(scriptsDirectory))
            throw new ArgumentException("scriptsDirectory is required.", nameof(scriptsDirectory));

        if (!Directory.Exists(scriptsDirectory))
            throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsDirectory}");

        var tempDir = Path.Combine(Path.GetTempPath(), "fbtool_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string tempDbPath = _databaseBuilder.Build(tempDir, scriptsDirectory);

        try
        {
            var desiredConnectionString = BuildConnectionStringForTempDb(tempDbPath);

            DatabaseSchemaDto desired = _metadataReader.Read(desiredConnectionString);
            DatabaseSchemaDto current = _metadataReader.Read(connectionString);

            var sqlStatements = _diffService.BuildDiffSql(current, desired);

            if (sqlStatements.Count > 0)
            {
                using var connection = new FbConnection(connectionString);
                connection.Open();

                using var transaction = connection.BeginTransaction();
                try
                {
                    foreach (var sql in sqlStatements)
                    {
                        using var cmd = new FbCommand(sql, connection, transaction);
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            _scriptExecutor.ExecuteProceduresOnly(connectionString, scriptsDirectory);
        }
        finally
        {
            TryDeleteFile(tempDbPath);
            TryDeleteDirectory(tempDir);
        }
    }

    private static string BuildConnectionStringForTempDb(string dbPath)
    {
        var host = Environment.GetEnvironmentVariable(FirebirdDefaults.EnvHost) ?? FirebirdDefaults.Host;
        var portRaw = Environment.GetEnvironmentVariable(FirebirdDefaults.EnvPort);
        var port = int.TryParse(portRaw, out var parsedPort) ? parsedPort : FirebirdDefaults.Port;
        var user = Environment.GetEnvironmentVariable(FirebirdDefaults.EnvUser) ?? FirebirdDefaults.User;
        var pass = Environment.GetEnvironmentVariable(FirebirdDefaults.EnvPassword);

        if (string.IsNullOrWhiteSpace(pass))
            throw new InvalidOperationException($"Missing required env var: {FirebirdDefaults.EnvPassword}");

        var csb = new FbConnectionStringBuilder
        {
            DataSource = host,
            Port = port,
            Database = dbPath,
            UserID = user,
            Password = pass,
            Dialect = FirebirdDefaults.DefaultDialect,
            Charset = FirebirdDefaults.DefaultCharset
        };

        return csb.ToString();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
        }
    }
}