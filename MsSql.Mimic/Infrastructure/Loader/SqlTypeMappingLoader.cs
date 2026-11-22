using Serilog;

using System.IO;
using System.Text.Json;

namespace MsSql.Mimic.Infrastructure.Loader;

/// <summary>
/// Provides loading of SQL Server to C# type mappings from the application configuration file.
/// </summary>
/// <remarks>
/// The loader reads mappings from a JSON file located in the application base directory and falls back to built-in default mappings when the file is missing, empty, or cannot be deserialized.
/// Mapping keys are normalized to support case-insensitive SQL type lookup semantics.
/// </remarks>
public sealed class SqlTypeMappingLoader
{
    /// <summary>
    /// Loads SQL Server to C# type mappings from the configured JSON file, falling back to default mappings when the file is missing, empty, or cannot be deserialized.
    /// </summary>
    /// <param name="ct">
    /// A cancellation token used to cancel the asynchronous load operation.
    /// </param>
    /// <return>
    /// A read-only dictionary containing SQL Server type names mapped to their corresponding C# type names.
    /// </return>
    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mssql-csharp-type-mappings.json");

        if (!File.Exists(path))
        {
            Log.Warning("MSSQL-CSharp-Type-Mapping file does not exist ({path}). Use fallback.", path);
            return CreateDefaultMappings();
        }

        await using var stream = File.OpenRead(path);

        var mappings = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: ct);

        if (mappings is not null && mappings.Count != 0) 
            return NormalizeKeys(mappings);
        
        Log.Warning("Can't deserialize MSSQL-CSharp-Type-Mapping file ({path}). Use fallback.", path);
        return CreateDefaultMappings();
    }

    /// <summary>
    /// Normalizes mapping keys by converting each key to lowercase using the invariant culture while preserving the associated values.
    /// </summary>
    /// <param name="mappings">
    /// The SQL type mappings to normalize.
    /// </param>
    /// <return>
    /// A read-only dictionary containing the normalized mapping keys and their original values.
    /// </return>
    private static IReadOnlyDictionary<string, string> NormalizeKeys(Dictionary<string, string> mappings)
    {
        return mappings.ToDictionary(entry => entry.Key.ToLowerInvariant(), entry => entry.Value);
    }

    /// <summary>
    /// Creates the built-in fallback mappings from SQL Server data type names to C# type names.
    /// </summary>
    /// <return>
    /// A read-only dictionary whose keys are SQL Server type names and whose values are the corresponding C# type names.
    /// </return>
    private static IReadOnlyDictionary<string, string> CreateDefaultMappings()
    {
        return new Dictionary<string, string>
        {
            ["bigint"] = "long",
            ["int"] = "int",
            ["smallint"] = "short",
            ["tinyint"] = "byte",
            ["bit"] = "bool",

            ["decimal"] = "decimal",
            ["numeric"] = "decimal",
            ["money"] = "decimal",
            ["smallmoney"] = "decimal",

            ["float"] = "double",
            ["real"] = "float",

            ["date"] = "DateOnly",
            ["datetime"] = "DateTime",
            ["datetime2"] = "DateTime",
            ["smalldatetime"] = "DateTime",
            ["datetimeoffset"] = "DateTimeOffset",
            ["time"] = "TimeOnly",

            ["char"] = "string",
            ["varchar"] = "string",
            ["text"] = "string",
            ["nchar"] = "string",
            ["nvarchar"] = "string",
            ["ntext"] = "string",

            ["uniqueidentifier"] = "Guid",

            ["binary"] = "byte[]",
            ["varbinary"] = "byte[]",
            ["image"] = "byte[]",

            ["xml"] = "string",
            ["json"] = "string",

            ["sql_variant"] = "object",
            ["hierarchyid"] = "string",
            ["geometry"] = "object",
            ["geography"] = "object",

            ["rowversion"] = "byte[]",
            ["timestamp"] = "byte[]"
        };
    }
}