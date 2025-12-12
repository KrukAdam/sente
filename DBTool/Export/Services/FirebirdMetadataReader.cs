using DBTool.Export.Models;
using FirebirdSql.Data.FirebirdClient;

namespace DBTool.Export.Services;

public interface IMetadataReader
{
    DatabaseSchemaDto Read(string connectionString);
}

public sealed class FirebirdMetadataReader : IMetadataReader
{
    public DatabaseSchemaDto Read(string connectionString)
    {
        using var conn = new FbConnection(connectionString);
        conn.Open();

        return new DatabaseSchemaDto
        {
            Database = conn.Database ?? "Firebird",
            Domains = ReadDomains(conn),
            Tables = ReadTables(conn),
            Procedures = ReadProcedures(conn),
        };
    }

    private static List<DomainDto> ReadDomains(FbConnection conn)
    {
        // USER-DEFINED domains: SYSTEM_FLAG = 0
        const string sql = @"
SELECT
  TRIM(f.RDB$FIELD_NAME) AS NAME,
  f.RDB$FIELD_TYPE,
  f.RDB$FIELD_SUB_TYPE,
  f.RDB$FIELD_LENGTH,
  f.RDB$FIELD_PRECISION,
  f.RDB$FIELD_SCALE
FROM RDB$FIELDS f
WHERE COALESCE(f.RDB$SYSTEM_FLAG, 0) = 0
  AND f.RDB$FIELD_NAME NOT STARTING WITH 'RDB$'
ORDER BY 1
";

        using var cmd = new FbCommand(sql, conn);
        using var r = cmd.ExecuteReader();

        var list = new List<DomainDto>();
        while (r.Read())
        {
            var typeCode = r.IsDBNull(1) ? (short?)null : r.GetInt16(1);
            var subType = r.IsDBNull(2) ? (short?)null : r.GetInt16(2);

            list.Add(new DomainDto
            {
                Name = r.GetString(0),
                FieldType = MapType(typeCode, subType),
                Length = r.IsDBNull(3) ? null : r.GetInt32(3),
                Precision = r.IsDBNull(4) ? null : (int?)r.GetInt16(4),
                Scale = r.IsDBNull(5) ? null : (int?)Math.Abs(r.GetInt16(5)),
            });
        }

        return list;
    }

    private static List<TableDto> ReadTables(FbConnection conn)
    {
        const string tablesSql = @"
SELECT TRIM(r.RDB$RELATION_NAME) AS NAME
FROM RDB$RELATIONS r
WHERE COALESCE(r.RDB$SYSTEM_FLAG, 0) = 0
  AND r.RDB$VIEW_BLR IS NULL
ORDER BY 1
";

        const string colsSql = @"
SELECT
  TRIM(rf.RDB$RELATION_NAME) AS TABLE_NAME,
  TRIM(rf.RDB$FIELD_NAME) AS COLUMN_NAME,
  TRIM(rf.RDB$FIELD_SOURCE) AS DOMAIN_NAME,
  COALESCE(rf.RDB$NULL_FLAG, 0) AS NOT_NULL_FLAG,
  rf.RDB$FIELD_POSITION
FROM RDB$RELATION_FIELDS rf
JOIN RDB$RELATIONS r ON r.RDB$RELATION_NAME = rf.RDB$RELATION_NAME
WHERE COALESCE(r.RDB$SYSTEM_FLAG, 0) = 0
  AND r.RDB$VIEW_BLR IS NULL
ORDER BY TABLE_NAME, rf.RDB$FIELD_POSITION
";

        var tables = new Dictionary<string, TableDto>(StringComparer.OrdinalIgnoreCase);

        using (var cmd = new FbCommand(tablesSql, conn))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var name = r.GetString(0);
                tables[name] = new TableDto { Name = name };
            }
        }

        using (var cmd = new FbCommand(colsSql, conn))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var tableName = r.GetString(0);
                if (!tables.TryGetValue(tableName, out var table))
                    continue;

                var notNull = !r.IsDBNull(3) && r.GetInt16(3) != 0;

                table.Columns.Add(new ColumnDto
                {
                    Name = r.GetString(1),
                    Domain = r.GetString(2), // np. DM_NAME / DM_AMOUNT
                    NotNull = notNull
                });
            }
        }

        return tables.Values.ToList();
    }

    private static List<ProcedureDto> ReadProcedures(FbConnection conn)
    {
        const string sql = @"
SELECT TRIM(p.RDB$PROCEDURE_NAME) AS NAME
FROM RDB$PROCEDURES p
WHERE COALESCE(p.RDB$SYSTEM_FLAG, 0) = 0
ORDER BY 1
";

        using var cmd = new FbCommand(sql, conn);
        using var r = cmd.ExecuteReader();

        var list = new List<ProcedureDto>();
        while (r.Read())
        {
            list.Add(new ProcedureDto { Name = r.GetString(0) });
        }

        return list;
    }

    private static string MapType(short? fieldType, short? subType)
    {
        if (fieldType is null) return "UNKNOWN";

        // Minimalny mapping wystarczy do JSON.
        return fieldType.Value switch
        {
            7 => "SMALLINT",
            8 => "INTEGER",
            10 => "FLOAT",
            12 => "DATE",
            13 => "TIME",
            14 => "CHAR",
            16 => subType switch
            {
                1 => "NUMERIC",
                2 => "DECIMAL",
                _ => "INT64"
            },
            27 => "DOUBLE",
            35 => "TIMESTAMP",
            37 => "VARCHAR",
            261 => "BLOB",
            _ => $"TYPE_{fieldType}"
        };
    }
}
