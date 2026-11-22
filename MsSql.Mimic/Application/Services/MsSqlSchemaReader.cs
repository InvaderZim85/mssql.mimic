using Dapper;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Infrastructure.MsSql;

namespace MsSql.Mimic.Application.Services;

/// <summary>
/// Reads schema metadata from a Microsoft SQL Server database for use by application services.
/// </summary>
/// <remarks>
/// Provides operations for loading available table names and detailed table definitions from a database connection profile.
/// </remarks>
/// <param name="connectionFactory">
/// Factory used to create SQL Server connections for the supplied connection profiles.
/// </param>
public sealed class MsSqlSchemaReader(SqlConnectionFactory connectionFactory)
{
    /// <summary>
    /// Loads the available SQL Server table names and schemas for the specified connection profile.
    /// </summary>
    /// <param name="profile">The connection profile used to create the database connection.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of table entries ordered by table name.</returns>
    public async Task<IReadOnlyList<TableEntry>> LoadTableNamesAsync(
        ConnectionProfile profile,
        CancellationToken ct = default)
    {
        const string query =
            """
            SELECT
                TABLE_SCHEMA AS [Schema],
                TABLE_NAME AS [Name]
            FROM
                INFORMATION_SCHEMA.TABLES
            ORDER BY
                TABLE_NAME;
            """;

        var connection = connectionFactory.CreateConnection(profile);
        
        var command = new CommandDefinition(query, cancellationToken: ct);

        var tables = await connection.QueryAsync<TableEntry>(command);

        return [.. tables];
    }

    /// <summary>
    /// Loads the column metadata and primary key information for the specified SQL Server table.
    /// </summary>
    /// <param name="profile">The connection profile used to create the database connection.</param>
    /// <param name="table">The table entry that identifies the schema and table name to load.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the table definition, including schema, name, and column definitions.</returns>
    public async Task<TableDefinition> LoadTableDefinitionAsync(
        ConnectionProfile profile,
        TableEntry table,
        CancellationToken ct = default)
    {
        const string query =
            """
            SELECT
                c.COLUMN_NAME AS [Name],
                c.DATA_TYPE AS SqlDataType,
                CAST(CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS bit) AS IsNullable,
                CAST(CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsPrimaryKey,
                c.CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                c.NUMERIC_PRECISION AS Precision,
                c.NUMERIC_SCALE AS Scale
            FROM 
                INFORMATION_SCHEMA.COLUMNS AS c

                LEFT JOIN 
                (
                    SELECT 
                        ku.TABLE_SCHEMA, 
                        ku.TABLE_NAME, 
                        ku.COLUMN_NAME
                    FROM 
                        INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
                        
                        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS ku
                        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                        AND tc.TABLE_NAME = ku.TABLE_NAME
                    WHERE 
                        tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) AS pk
                ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA
                AND c.TABLE_NAME = pk.TABLE_NAME
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE 
                c.TABLE_SCHEMA = @Schema
                AND c.TABLE_NAME = @TableName
            ORDER BY 
                c.ORDINAL_POSITION;
            """;

        var connection = connectionFactory.CreateConnection(profile);
        
        var command = new CommandDefinition(query, new
            {
                table.Schema,
                TableName = table.Name
            },
            cancellationToken: ct);
        
        var columns = await connection.QueryAsync<ColumnDefinition>(command);

        return new TableDefinition
        {
            Schema = table.Schema,
            Name = table.Name,
            Columns = [.. columns]
        };
    }
}