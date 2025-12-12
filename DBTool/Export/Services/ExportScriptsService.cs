using DBTool.Export.Models;

namespace DBTool.Export.Services;

public interface IExportScriptsService
{
    void Export(string connectionString, string outputDirectory);
}

public sealed class ExportScriptsService : IExportScriptsService
{
    private readonly IMetadataReader _reader;
    private readonly ISchemaWriter _writer;

    public ExportScriptsService(IMetadataReader reader, ISchemaWriter writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public void Export(string connectionString, string outputDirectory)
    {
        DatabaseSchemaDto schema = _reader.Read(connectionString);
        _writer.Write(schema, outputDirectory);
    }
}
