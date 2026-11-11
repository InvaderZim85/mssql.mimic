namespace MsSql.Mimic.Domain.Services;

public sealed class SqlTypeMapper
{
    public string MapToCSharpType(
        string sqlType,
        bool isNullable)
    {
        var typeName = sqlType.ToLowerInvariant() switch
        {
            "bigint" => "long",
            "int" => "int",
            "smallint" => "short",
            "tinyint" => "byte",
            "bit" => "bool",

            "decimal" => "decimal",
            "numeric" => "decimal",
            "money" => "decimal",
            "smallmoney" => "decimal",

            "float" => "double",
            "real" => "float",

            "date" => "DateOnly",
            "datetime" => "DateTime",
            "datetime2" => "DateTime",
            "smalldatetime" => "DateTime",
            "datetimeoffset" => "DateTimeOffset",
            "time" => "TimeOnly",

            "char" => "string",
            "varchar" => "string",
            "text" => "string",
            "nchar" => "string",
            "nvarchar" => "string",
            "ntext" => "string",

            "uniqueidentifier" => "Guid",

            "binary" => "byte[]",
            "varbinary" => "byte[]",
            "image" => "byte[]",

            _ => "object"
        };

        return isNullable ? $"{typeName}?" : typeName;
    }
}