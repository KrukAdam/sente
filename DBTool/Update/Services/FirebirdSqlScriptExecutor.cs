using System.Text;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Isql;

namespace DBTool.Update.Services;

public interface ISqlScriptExecutor
{
    void ExecuteDirectory(string connectionString, string scriptsDirectory);
    void ExecuteProceduresOnly(string connectionString, string scriptsDirectory);
}

public sealed class FirebirdSqlScriptExecutor : ISqlScriptExecutor
{
    private enum ScriptCategory
    {
        Domains = 0,
        Tables = 1,
        Procedures = 2,
        Other = 99
    }

    public void ExecuteDirectory(string connectionString, string scriptsDirectory)
    {
        ValidateInputs(connectionString, scriptsDirectory);

        var files = Directory.GetFiles(scriptsDirectory, "*.sql", SearchOption.TopDirectoryOnly).ToArray();
        if (files.Length == 0)
            throw new FileNotFoundException($"No .sql files found in: {scriptsDirectory}");

        var orderedFiles = SortScripts(files);
        var nonProcedureFiles = orderedFiles.Where(f => DetectCategory(File.ReadAllText(f, Encoding.UTF8)) != ScriptCategory.Procedures).ToList();
        var procedureFiles = orderedFiles.Where(f => DetectCategory(File.ReadAllText(f, Encoding.UTF8)) == ScriptCategory.Procedures).ToList();

        using var connection = new FbConnection(connectionString);
        connection.Open();

        foreach (var file in nonProcedureFiles)
        {
            var originalSql = File.ReadAllText(file, Encoding.UTF8);
            var sql = Preprocess(originalSql);

            if (string.IsNullOrWhiteSpace(sql))
            {
                Console.WriteLine($"[DBTool] Skipped empty/unsupported script: {Path.GetFileName(file)}");
                continue;
            }

            Console.WriteLine($"[DBTool] Executing: {Path.GetFileName(file)}");
            ExecuteScript(connection, sql, file);
        }

        ExecuteProcedureFilesWithRetry(connection, procedureFiles);

        Console.WriteLine("[DBTool] Update finished.");
    }

    public void ExecuteProceduresOnly(string connectionString, string scriptsDirectory)
    {
        ValidateInputs(connectionString, scriptsDirectory);

        var files = Directory.GetFiles(scriptsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .Where(f => DetectCategory(File.ReadAllText(f, Encoding.UTF8)) == ScriptCategory.Procedures)
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            return;

        using var connection = new FbConnection(connectionString);
        connection.Open();

        ExecuteProcedureFilesWithRetry(connection, files);
    }

    private static void ValidateInputs(string connectionString, string scriptsDirectory)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("connectionString is required.", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(scriptsDirectory))
            throw new ArgumentException("scriptsDirectory is required.", nameof(scriptsDirectory));

        if (!Directory.Exists(scriptsDirectory))
            throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsDirectory}");
    }

    private static string[] SortScripts(string[] files)
    {
        return files
            .Select(f => new
            {
                File = f,
                Category = DetectCategory(File.ReadAllText(f, Encoding.UTF8))
            })
            .OrderBy(x => x.Category)
            .ThenBy(x => Path.GetFileName(x.File), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.File)
            .ToArray();
    }

    private static ScriptCategory DetectCategory(string sqlRaw)
    {
        var sql = Preprocess(sqlRaw);

        if (Contains(sql, "CREATE DOMAIN") || Contains(sql, "ALTER DOMAIN"))
            return ScriptCategory.Domains;

        if (Contains(sql, "CREATE TABLE") || Contains(sql, "ALTER TABLE"))
            return ScriptCategory.Tables;

        if (Contains(sql, "CREATE PROCEDURE") || Contains(sql, "ALTER PROCEDURE") || Contains(sql, "CREATE OR ALTER PROCEDURE"))
            return ScriptCategory.Procedures;

        return ScriptCategory.Other;
    }

    private static bool Contains(string sql, string needle)
        => sql.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static void ExecuteProcedureFilesWithRetry(FbConnection connection, List<string> procedureFiles)
    {
        if (procedureFiles.Count == 0)
            return;

        var pending = new List<string>(procedureFiles);

        while (pending.Count > 0)
        {
            var failed = new List<string>();
            var successCount = 0;

            foreach (var file in pending)
            {
                var originalSql = File.ReadAllText(file, Encoding.UTF8);
                var sql = Preprocess(originalSql);

                if (string.IsNullOrWhiteSpace(sql))
                    continue;

                try
                {
                    Console.WriteLine($"[DBTool] Executing procedure: {Path.GetFileName(file)}");
                    ExecuteScript(connection, sql, file);
                    successCount++;
                }
                catch
                {
                    failed.Add(file);
                }
            }

            if (failed.Count == 0)
                return;

            if (successCount == 0)
            {
                throw new InvalidOperationException(
                    "Could not resolve procedure dependencies. Failed files: " +
                    string.Join(", ", failed.Select(Path.GetFileName)));
            }

            pending = failed;
        }
    }

    private static void ExecuteScript(FbConnection connection, string sql, string filePathForErrors)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("Connection must be open before executing scripts.");

        try
        {
            if (ContainsExecuteBlock(sql))
            {
                using var cmd = new FbCommand(sql, connection);
                cmd.ExecuteNonQuery();
                return;
            }

            var script = new FbScript(sql);
            script.Parse();

            var batch = new FbBatchExecution(connection);
            batch.AppendSqlStatements(script);
            batch.Execute();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to execute script: {filePathForErrors}{Environment.NewLine}{ex.Message}",
                ex);
        }
    }

    private static bool ContainsExecuteBlock(string sql)
        => sql.IndexOf("EXECUTE BLOCK", StringComparison.OrdinalIgnoreCase) >= 0;

    private static string Preprocess(string sql)
    {
        var sb = new StringBuilder(sql.Length);

        using var reader = new StringReader(sql);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var t = line.TrimStart();

            if (t.StartsWith("/*", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("*/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("*", StringComparison.OrdinalIgnoreCase))
                continue;

            if (t.StartsWith("SET SQL DIALECT", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("SET NAMES", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("SET CLIENTLIB", StringComparison.OrdinalIgnoreCase))
                continue;

            if (t.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("CONNECT", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("USER ", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("PASSWORD ", StringComparison.OrdinalIgnoreCase))
                continue;

            sb.AppendLine(line);
        }

        return sb.ToString().Trim();
    }
}