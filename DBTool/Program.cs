using DBTool.Export;
using System;
using System.IO;

namespace DbMetaTool;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Użycie:");
            Console.WriteLine(" build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
            Console.WriteLine(" export-scripts --connection-string  --output-dir <ścieżka>");
            Console.WriteLine(" update-db --connection-string  --scripts-dir <ścieżka>");
            return 1;
        }

        try
        {
            var command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "build-db":
                    {
                        string dbDir = GetArgValue(args, "--db-dir");
                        string scriptsDir = GetArgValue(args, "--scripts-dir");
                        BuildDatabase(dbDir, scriptsDir);
                        Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                        return 0;
                    }

                case "export-scripts":
                    {
                        string connStr = GetArgValue(args, "--connection-string");
                        string outputDir = GetArgValue(args, "--output-dir");
                        ExportScripts(connStr, outputDir);
                        Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                        return 0;
                    }

                case "update-db":
                    {
                        string connStr = GetArgValue(args, "--connection-string");
                        string scriptsDir = GetArgValue(args, "--scripts-dir");
                        UpdateDatabase(connStr, scriptsDir);
                        Console.WriteLine("Baza danych została zaktualizowana pomyślnie.");
                        return 0;
                    }

                default:
                    Console.WriteLine($"Nieznane polecenie: {command}");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Błąd: " + ex.Message);
            return -1;
        }
    }

    private static string GetArgValue(string[] args, string name)
    {
        int idx = Array.IndexOf(args, name);
        if (idx == -1 || idx + 1 >= args.Length)
            throw new ArgumentException($"Brak wymaganego parametru {name}");

        return args[idx + 1];
    }

    public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
    {
        if (string.IsNullOrWhiteSpace(databaseDirectory))
            throw new ArgumentException("databaseDirectory is required.", nameof(databaseDirectory));

        if (string.IsNullOrWhiteSpace(scriptsDirectory))
            throw new ArgumentException("scriptsDirectory is required.", nameof(scriptsDirectory));

        Console.WriteLine("[DBTool] Build started.");
        Console.WriteLine($"[DBTool] Target dir: {databaseDirectory}");
        Console.WriteLine($"[DBTool] Scripts dir: {scriptsDirectory}");

        try
        {
            var executor = new DBTool.Update.Services.FirebirdSqlScriptExecutor();
            var builder = new DBTool.Build.Services.FirebirdDatabaseBuilder(executor);
            var dbPath = builder.Build(databaseDirectory, scriptsDirectory);

            var statsReader = new DBTool.Build.Services.FirebirdMetadataStatsReader();
            var stats = statsReader.ReadForDatabaseFile(dbPath);

            Console.WriteLine($"[DBTool] Database created: {dbPath}");
            Console.WriteLine($"[DBTool] Metadata counts: Domains={stats.Domains}, Tables={stats.Tables}, Procedures={stats.Procedures}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DBTool] Build failed: " + ex.Message);
            throw;
        }
    }

    public static void ExportScripts(string connectionString, string outputDirectory)
    {
        var reader = new DBTool.Export.Services.FirebirdMetadataReader();
        var writer = new DBTool.Export.Services.JsonSchemaWriter();
        var exporter = new DBTool.Export.Services.ExportScriptsService(reader, writer);
        exporter.Export(connectionString, outputDirectory);
        Console.WriteLine($"Wygenerowano: {Path.Combine(outputDirectory, ExportFiles.SchemaJson)}");
    }

    public static void UpdateDatabase(string connectionString, string scriptsDirectory)
    {
        var executor = new DBTool.Update.Services.FirebirdSqlScriptExecutor();
        var builder = new DBTool.Build.Services.FirebirdDatabaseBuilder(executor);
        var metadataReader = new DBTool.Export.Services.FirebirdMetadataReader();
        var diffService = new DBTool.Update.Services.FirebirdSchemaDiffService();
        var updateService = new DBTool.Update.Services.FirebirdUpdateService(
            metadataReader,
            builder,
            diffService,
            executor);

        updateService.Update(connectionString, scriptsDirectory);
    }
}