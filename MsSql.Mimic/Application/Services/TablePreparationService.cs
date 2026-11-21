using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;

namespace MsSql.Mimic.Application.Services;

/// <summary>
/// Coordinates the retrieval and preparation of SQL Server table metadata for use by the application.
/// </summary>
/// <remarks>
/// This service loads table information from a SQL Server connection profile and prepares table definitions by applying naming rules, aliases, and property metadata conversion.
/// </remarks>
/// <param name="schemaReader">Reads table and column metadata from SQL Server.</param>
/// <param name="aliasService">Provides alias information used to customize generated names.</param>
/// <param name="propertyDefinitionFactory">Creates property definitions from database column metadata.</param>
/// <param name="nameService">Provides C# naming normalization and validation for generated identifiers.</param>
public sealed class TablePreparationService(
    MsSqlSchemaReader schemaReader,
    AliasService aliasService,
    PropertyDefinitionFactory propertyDefinitionFactory,
    CSharpNameService nameService)
{
    /// <summary>
    /// Loads the available table entries for the specified connection profile.
    /// </summary>
    /// <param name="profile">The connection profile used to read table names from the database.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded table entries.</returns>
    public async Task<IReadOnlyList<TableEntry>> LoadTablesAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        return await schemaReader.LoadTableNamesAsync(profile, ct);
    }

    /// <summary>
    /// Loads the schema definition and alias configuration for the specified table, then creates a prepared table definition for code generation.
    /// </summary>
    /// <param name="profile">The connection profile used to read the table schema and alias information.</param>
    /// <param name="table">The table entry to prepare.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the prepared table definition.</returns>
    public async Task<PreparedTableDefinition> LoadPreparedTableAsync(
        ConnectionProfile profile,
        TableEntry table,
        CancellationToken ct = default)
    {
        var tableDefinition = await schemaReader.LoadTableDefinitionAsync(profile, table, ct);
        var alias = await aliasService.LoadTableAliasAsync(profile, table, ct);

        var className = alias?.ClassName ?? nameService.ToClassName(table.Name);
        var useEfCoreAttributes = alias?.UseEfCoreAttributes ?? true;

        var properties = propertyDefinitionFactory.CreateProperties(tableDefinition, alias);

        return new PreparedTableDefinition
        {
            Table = table,
            ClassName = className,
            UseEfCoreAttributes = useEfCoreAttributes,
            Properties = properties
        };
    }

    /// <summary>
    /// Resets the prepared definition for the specified table by reloading its current schema and mapping information.
    /// </summary>
    /// <param name="profile">The connection profile used to read the table definition from the database.</param>
    /// <param name="table">The table entry whose prepared definition is reset.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the reset prepared table definition.</returns>
    public async Task<PreparedTableDefinition> ResetPreparedTableAsync(
        ConnectionProfile profile,
        TableEntry table,
        CancellationToken ct = default)
    {
        return await LoadPreparedTableAsync(profile, table, ct);
    }
}