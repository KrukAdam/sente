using System.Text;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Isql;

namespace DBTool.Update.Services;

public interface ISqlScriptExecutor
{
    void ExecuteDirectory(string connectionString, string scriptsDirectory);
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
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("connectionString is required.", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(scriptsDirectory))
            throw new ArgumentException("scriptsDirectory is required.", nameof(scriptsDirectory));

        if (!Directory.Exists(scriptsDirectory))
            throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsDirectory}");

        var files = Directory.GetFiles(scriptsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .ToArray();

        if (files.Length == 0)
            throw new FileNotFoundException($"No .sql files found in: {scriptsDirectory}");

        var orderedFiles = SortScripts(files);

        using var connection = new FbConnection(connectionString);
        connection.Open();

        foreach (var file in orderedFiles)
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

        Console.WriteLine("[DBTool] Update finished.");
    }

    /// <summary>
    /// Dodatkowo: w obrębie kategorii sortujemy po nazwie pliku (stabilnie).
    /// Jeśli masz prefiksy 010/020/030 – też zadziała, bo nazwa pliku jest częścią sortowania.
    /// </summary>
    private static string[] SortScripts(string[] files)
    {
        return files
            .Select(f => new
            {
                File = f,
                Category = DetectCategory(File.ReadAllText(f, Encoding.UTF8))
            })
            .OrderBy(x => x.Category) // najpierw kategoria
            .ThenBy(x => Path.GetFileName(x.File), StringComparer.OrdinalIgnoreCase) 
            .Select(x => x.File)
            .ToArray();
    }

    /// <summary>
    /// Bardzo prosta “detekcja” typu skryptu.
    /// Nie parsujemy całego SQL-a, tylko szukamy słów kluczowych po preprocessie.
    /// </summary>
    private static ScriptCategory DetectCategory(string sqlRaw)
    {
        var sql = Preprocess(sqlRaw);

        // domain
        if (Contains(sql, "CREATE DOMAIN") || Contains(sql, "ALTER DOMAIN"))
            return ScriptCategory.Domains;

        // table
        if (Contains(sql, "CREATE TABLE") || Contains(sql, "ALTER TABLE"))
            return ScriptCategory.Tables;

        // procedure
        if (Contains(sql, "CREATE PROCEDURE") || Contains(sql, "ALTER PROCEDURE"))
            return ScriptCategory.Procedures;

        // inne/nieobsługiwane
        return ScriptCategory.Other;
    }


    private static bool Contains(string sql, string needle)
        => sql.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static void ExecuteScript(FbConnection connection, string sql, string filePathForErrors)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("Connection must be open before executing scripts.");

        try
        {
            // EXECUTE BLOCK ma średniki w środku - batch parser go rozcina i psuje.
            // Dlatego blok wykonujemy jako JEDNO polecenie (FbCommand).
            if (ContainsExecuteBlock(sql))
            {
                using var cmd = new FbCommand(sql, connection);
                cmd.ExecuteNonQuery();
                return;
            }

            // Dla zwykłych plików z wieloma statementami: FbScript + batch
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

    /// <summary>
    /// Minimalne czyszczenie SQL z exportu narzędzi (IBExpert itd.),
    /// żeby Update (na istniejącej bazie) nie próbował np. CREATE DATABASE / CONNECT / itp.
    /// </summary>
    private static string Preprocess(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        using var reader = new StringReader(sql);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var t = line.TrimStart();

            // komentarze blokowe z IBExpert potrafią psuć parser (UnknownStatement)
            if (t.StartsWith("/*", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.StartsWith("*/", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.StartsWith("*", StringComparison.OrdinalIgnoreCase)) continue;

            // Narzędziowe / niepotrzebne do update
            if (t.StartsWith("SET SQL DIALECT", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.StartsWith("SET NAMES", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.StartsWith("SET CLIENTLIB", StringComparison.OrdinalIgnoreCase)) continue;

            // Nie wolno tworzyć bazy w update istniejącej
            if (t.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.StartsWith("CONNECT", StringComparison.OrdinalIgnoreCase)) continue;

            // pomiń linie "USER 'SYSDBA' PASSWORD '...'"
            if (t.StartsWith("USER ", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.StartsWith("PASSWORD ", StringComparison.OrdinalIgnoreCase)) continue;

            sb.AppendLine(line);
        }

        return sb.ToString().Trim();
    }
}
