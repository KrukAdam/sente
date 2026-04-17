using DBTool.Export.Models;

namespace DBTool.Update.Services;

public interface IFirebirdSchemaDiffService
{
    List<string> BuildDiffSql(DatabaseSchemaDto current, DatabaseSchemaDto desired);
}

public sealed class FirebirdSchemaDiffService : IFirebirdSchemaDiffService
{
    public List<string> BuildDiffSql(DatabaseSchemaDto current, DatabaseSchemaDto desired)
    {
        var result = new List<string>();

        result.AddRange(BuildMissingDomains(current, desired));
        result.AddRange(BuildMissingTables(current, desired));
        result.AddRange(BuildMissingColumns(current, desired));

        return result;
    }

    private static IEnumerable<string> BuildMissingDomains(DatabaseSchemaDto current, DatabaseSchemaDto desired)
    {
        var currentDomains = current.Domains
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var domain in desired.Domains)
        {
            if (currentDomains.ContainsKey(domain.Name))
                continue;

            yield return $"CREATE DOMAIN {domain.Name} AS {BuildDomainType(domain)};";
        }
    }

    private static IEnumerable<string> BuildMissingTables(DatabaseSchemaDto current, DatabaseSchemaDto desired)
    {
        var currentTables = current.Tables
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var table in desired.Tables)
        {
            if (currentTables.ContainsKey(table.Name))
                continue;

            var cols = table.Columns.Select(BuildColumnDefinition);
            yield return $"CREATE TABLE {table.Name} ({string.Join(", ", cols)});";
        }
    }

    private static IEnumerable<string> BuildMissingColumns(DatabaseSchemaDto current, DatabaseSchemaDto desired)
    {
        var currentTables = current.Tables
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var desiredTable in desired.Tables)
        {
            if (!currentTables.TryGetValue(desiredTable.Name, out var currentTable))
                continue;

            var currentColumns = currentTable.Columns
                .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var desiredColumn in desiredTable.Columns)
            {
                if (currentColumns.ContainsKey(desiredColumn.Name))
                    continue;

                yield return $"ALTER TABLE {desiredTable.Name} ADD {BuildColumnDefinition(desiredColumn)};";
            }
        }
    }

    private static string BuildDomainType(DomainDto domain)
    {
        return domain.FieldType.ToUpperInvariant() switch
        {
            "VARCHAR" => $"VARCHAR({domain.Length ?? 1})",
            "CHAR" => $"CHAR({domain.Length ?? 1})",
            "DECIMAL" => $"DECIMAL({domain.Precision ?? 18},{domain.Scale ?? 0})",
            "NUMERIC" => $"NUMERIC({domain.Precision ?? 18},{domain.Scale ?? 0})",
            _ => domain.FieldType
        };
    }

    private static string BuildColumnDefinition(ColumnDto column)
    {
        var baseType = !string.IsNullOrWhiteSpace(column.Domain)
            ? column.Domain!
            : column.FieldType ?? throw new InvalidOperationException($"Column {column.Name} has no Domain and no FieldType.");

        return column.NotNull
            ? $"{column.Name} {baseType} NOT NULL"
            : $"{column.Name} {baseType}";
    }
}