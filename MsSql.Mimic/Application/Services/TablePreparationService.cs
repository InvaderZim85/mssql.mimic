using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;

namespace MsSql.Mimic.Application.Services;

public sealed class TablePreparationService(
    MsSqlSchemaReader schemaReader,
    AliasService aliasService,
    PropertyDefinitionFactory propertyDefinitionFactory,
    CSharpNameService nameService)
{
    public async Task<IReadOnlyList<TableEntry>> LoadTablesAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        return await schemaReader.LoadTableNamesAsync(profile, ct);
    }

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
}