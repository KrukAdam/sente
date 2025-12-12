using DBTool.Export.Models;
using System.Text.Json;

namespace DBTool.Export.Services;

public interface ISchemaWriter
{
    void Write(DatabaseSchemaDto schema, string outputDirectory);
}

public sealed class JsonSchemaWriter : ISchemaWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public void Write(DatabaseSchemaDto schema, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var path = Path.Combine(outputDirectory, ExportFiles.SchemaJson);
        var json = JsonSerializer.Serialize(schema, Options);
        File.WriteAllText(path, json);
    }
}
