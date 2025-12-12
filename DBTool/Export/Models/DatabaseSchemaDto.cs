namespace DBTool.Export.Models;

public sealed class DatabaseSchemaDto
{
    public string Database { get; init; } = "";
    public List<DomainDto> Domains { get; init; } = new();
    public List<TableDto> Tables { get; init; } = new();
    public List<ProcedureDto> Procedures { get; init; } = new();
}

public sealed class DomainDto
{
    public string Name { get; init; } = "";
    public string FieldType { get; init; } = "";   // np. VARCHAR, DECIMAL
    public int? Length { get; init; }              // np. 1000
    public int? Precision { get; init; }           // np. 18
    public int? Scale { get; init; }               // np. 2
}

public sealed class TableDto
{
    public string Name { get; init; } = "";
    public List<ColumnDto> Columns { get; init; } = new();
}

public sealed class ColumnDto
{
    public string Name { get; init; } = "";
    public string? Domain { get; init; }           // jeśli kolumna używa domeny
    public string? FieldType { get; init; }        // jeśli bez domeny
    public bool NotNull { get; init; }
}

public sealed class ProcedureDto
{
    public string Name { get; init; } = "";
}
