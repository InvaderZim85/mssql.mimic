---
tags:
  - mssql
  - classgenerator
---
Ja — dafür solltest du das Programm konzeptionell von **„eine feste Datenbank“** auf **„konfigurierbare Datenbankverbindung pro Arbeitskontext“** umstellen.

Wichtig ist dabei:

1. Der Benutzer wählt oder erfasst:
   - Server
   - Datenbank
   - Authentifizierung
   - optional Benutzername / Passwort
   - TrustServerCertificate etc.

2. Alle MS-SQL-Abfragen laufen über eine Connection, die aus dieser Benutzereingabe erstellt wird.

3. Die Alias-Daten in SQLite dürfen nicht nur nach `Database + TableName` gespeichert werden, sondern müssen mindestens auch den **Server** und das **Schema** berücksichtigen.

Sonst kollidieren Tabellen wie:

```plain text
ServerA / DatabaseA / dbo.Customer
ServerB / DatabaseA / dbo.Customer
ServerA / DatabaseB / dbo.Customer
```


---

## 1. Neuer Verbindungs-Kontext

Erstelle ein Modell, das die aktuell ausgewählte MS-SQL-Datenbank beschreibt.

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

internal sealed class DatabaseConnectionProfile
{
    public required string ServerName { get; init; }

    public required string DatabaseName { get; init; }

    public bool UseIntegratedSecurity { get; init; } = true;

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public bool TrustServerCertificate { get; init; } = true;

    public bool Encrypt { get; init; } = true;
}
```


Wenn du keine Profile speichern willst, kann das auch nur ein temporäres ViewModel sein. Für den `AliasService` ist aber wichtig, dass du daraus einen eindeutigen Schlüssel bilden kannst.

---

## 2. Connection Factory anpassen

Statt eine feste Connection zu injizieren, sollte die Factory eine Connection aus einem Profil erzeugen.

```csharp
using Microsoft.Data.SqlClient;
using MsSql.ClassGenerator.Domain.Models;
using System.Data;

namespace MsSql.ClassGenerator.Infrastructure.MsSql;

internal sealed class SqlConnectionFactory
{
    public IDbConnection CreateConnection(DatabaseConnectionProfile profile)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = profile.ServerName,
            InitialCatalog = profile.DatabaseName,
            Encrypt = profile.Encrypt,
            TrustServerCertificate = profile.TrustServerCertificate,
            MultipleActiveResultSets = false
        };

        if (profile.UseIntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = profile.UserName;
            builder.Password = profile.Password;
            builder.IntegratedSecurity = false;
        }

        return new SqlConnection(builder.ConnectionString);
    }
}
```


Dann sollte dein `MsSqlSchemaReader` nicht mehr dauerhaft eine feste `IDbConnection` im Konstruktor bekommen, sondern entweder:

- pro Methode ein `IDbConnection`
- oder ein `DatabaseConnectionProfile`

Ich würde die zweite Variante bevorzugen.

---

## 3. `MsSqlSchemaReader` auf beliebige Verbindung erweitern

Beispiel:

```csharp
using Dapper;
using MsSql.ClassGenerator.Domain.Models;
using MsSql.ClassGenerator.Infrastructure.MsSql;
using System.Data;

namespace MsSql.ClassGenerator.Application.Services;

internal sealed class MsSqlSchemaReader
{
    private readonly SqlConnectionFactory _connectionFactory;

    public MsSqlSchemaReader(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<TableEntry>> LoadTableNamesAsync(
        DatabaseConnectionProfile profile,
        CancellationToken ct = default)
    {
        const string query =
            """
            SELECT
                TABLE_SCHEMA AS [Schema],
                TABLE_NAME AS [Name]
            FROM
                INFORMATION_SCHEMA.TABLES
            WHERE
                TABLE_TYPE = 'BASE TABLE'
            ORDER BY
                TABLE_SCHEMA,
                TABLE_NAME;
            """;

        using var connection = _connectionFactory.CreateConnection(profile);

        var command = new CommandDefinition(
            query,
            cancellationToken: ct);

        var tables = await connection.QueryAsync<TableEntry>(command);

        return [.. tables];
    }

    public async Task<TableDefinition> LoadTableDefinitionAsync(
        DatabaseConnectionProfile profile,
        TableEntry table,
        CancellationToken ct = default)
    {
        const string query =
            """
            SELECT
                c.COLUMN_NAME AS ColumnName,
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

        using var connection = _connectionFactory.CreateConnection(profile);

        var command = new CommandDefinition(
            query,
            new
            {
                Schema = table.Schema,
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

    public async Task<IReadOnlyList<string>> LoadDatabaseNamesAsync(
        DatabaseConnectionProfile serverProfile,
        CancellationToken ct = default)
    {
        var profile = serverProfile withDatabase("master");

        const string query =
            """
            SELECT
                name
            FROM
                sys.databases
            WHERE
                database_id > 4
            ORDER BY
                name;
            """;

        using var connection = _connectionFactory.CreateConnection(profile);

        var command = new CommandDefinition(
            query,
            cancellationToken: ct);

        var databases = await connection.QueryAsync<string>(command);

        return [.. databases];
    }
}
```


Da `DatabaseConnectionProfile` oben als `class` definiert ist, gibt es kein `with`. Entweder machst du daraus ein `record` oder du erzeugst eine Kopie manuell.

Ich würde hier tatsächlich ein `record` verwenden.

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

internal sealed record DatabaseConnectionProfile
{
    public required string ServerName { get; init; }

    public required string DatabaseName { get; init; }

    public bool UseIntegratedSecurity { get; init; } = true;

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public bool TrustServerCertificate { get; init; } = true;

    public bool Encrypt { get; init; } = true;
}
```


Dann funktioniert:

```csharp
var profile = serverProfile with
{
    DatabaseName = "master"
};
```


Korrigierte Methode:

```csharp
public async Task<IReadOnlyList<string>> LoadDatabaseNamesAsync(
    DatabaseConnectionProfile serverProfile,
    CancellationToken ct = default)
{
    var profile = serverProfile with
    {
        DatabaseName = "master"
    };

    const string query =
        """
        SELECT
            name
        FROM
            sys.databases
        WHERE
            database_id > 4
        ORDER BY
            name;
        """;

    using var connection = _connectionFactory.CreateConnection(profile);

    var command = new CommandDefinition(
        query,
        cancellationToken: ct);

    var databases = await connection.QueryAsync<string>(command);

    return [.. databases];
}
```


---

## 4. Alias-Daten eindeutig speichern

Dein aktuelles `TableAliasEntity` enthält bereits `Database`, aber das reicht nicht aus.

Du brauchst mindestens:

```plain text
ServerName
DatabaseName
SchemaName
TableName
```


Optional kannst du zusätzlich einen normalisierten Schlüssel speichern, z. B.:

```plain text
ServerKey = ServerName.ToLowerInvariant()
DatabaseKey = DatabaseName.ToLowerInvariant()
```


Für den Anfang reicht aber eine eindeutige Kombination aus Server, Datenbank, Schema und Tabelle.

---

## 5. `TableAliasEntity` erweitern

Aktuell hast du nur `Database` und `TableName`. Ändere das besser so:

```csharp
namespace MsSql.ClassGenerator.Infrastructure.Sqlite.Entities;

internal sealed class TableAliasEntity
{
    public int Id { get; set; }

    public required string ServerName { get; set; }

    public required string DatabaseName { get; set; }

    public required string SchemaName { get; set; }

    public required string TableName { get; set; }

    public required string ClassName { get; set; }

    public bool UseEfCoreAttributes { get; set; }

    public List<ColumnAliasEntity> Columns { get; set; } = [];
}
```


Damit unterscheiden sich Aliasdaten sauber nach Herkunft:

```plain text
localhost / Northwind / dbo / Customers
prod-sql-01 / Northwind / dbo / Customers
localhost / Crm / dbo / Customers
localhost / Northwind / audit / Customers
```


---

## 6. `AliasDbContext` Index anpassen

In deinem SQLite-Modell sollte ein eindeutiger Index über diese vier Felder existieren.

```csharp
using Microsoft.EntityFrameworkCore;
using MsSql.ClassGenerator.Infrastructure.Sqlite.Entities;

namespace MsSql.ClassGenerator.Infrastructure.Sqlite;

internal sealed class AliasDbContext : DbContext
{
    public DbSet<TableAliasEntity> TableAliases => Set<TableAliasEntity>();

    public DbSet<ColumnAliasEntity> ColumnAliases => Set<ColumnAliasEntity>();

    public AliasDbContext(DbContextOptions<AliasDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TableAliasEntity>(entity =>
        {
            entity.ToTable("TableAliases");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ServerName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.DatabaseName)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(x => x.SchemaName)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(x => x.TableName)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(x => x.ClassName)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(x => new
            {
                x.ServerName,
                x.DatabaseName,
                x.SchemaName,
                x.TableName
            }).IsUnique();

            entity.HasMany(x => x.Columns)
                .WithOne(x => x.TableAlias)
                .HasForeignKey(x => x.TableAliasId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ColumnAliasEntity>(entity =>
        {
            entity.ToTable("ColumnAliases");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ColumnName)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(x => x.PropertyName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.AccessorKind)
                .IsRequired()
                .HasMaxLength(32);

            entity.HasIndex(x => new
            {
                x.TableAliasId,
                x.ColumnName
            }).IsUnique();
        });
    }
}
```


---

## 7. `AliasService` anpassen

Der `AliasService` sollte nicht mehr nur `database` und `tableName` verwenden.

Besser:

- `DatabaseConnectionProfile profile`
- `TableEntry table`

Dann muss der Aufrufer nicht selbst viele Strings korrekt weiterreichen.

```csharp
using Microsoft.EntityFrameworkCore;
using MsSql.ClassGenerator.Domain.Models;
using MsSql.ClassGenerator.Infrastructure.Sqlite;
using MsSql.ClassGenerator.Infrastructure.Sqlite.Entities;

namespace MsSql.ClassGenerator.Application.Services;

internal sealed class AliasService
{
    private readonly AliasDbContext _context;

    public AliasService(AliasDbContext context)
    {
        _context = context;
    }

    public async Task<TableAliasEntity?> LoadTableAliasAsync(
        DatabaseConnectionProfile profile,
        TableEntry table,
        CancellationToken ct = default)
    {
        return await _context.TableAliases
            .Include(x => x.Columns)
            .SingleOrDefaultAsync(
                x => x.ServerName == profile.ServerName &&
                     x.DatabaseName == profile.DatabaseName &&
                     x.SchemaName == table.Schema &&
                     x.TableName == table.Name,
                ct);
    }

    public async Task SaveAsync(
        DatabaseConnectionProfile profile,
        TableEntry table,
        string className,
        bool useEfCoreAttributes,
        IReadOnlyList<PropertyDefinition> properties,
        CancellationToken ct = default)
    {
        var entity = await _context.TableAliases
            .Include(x => x.Columns)
            .SingleOrDefaultAsync(
                x => x.ServerName == profile.ServerName &&
                     x.DatabaseName == profile.DatabaseName &&
                     x.SchemaName == table.Schema &&
                     x.TableName == table.Name,
                ct);

        if (entity is null)
        {
            entity = new TableAliasEntity
            {
                ServerName = profile.ServerName,
                DatabaseName = profile.DatabaseName,
                SchemaName = table.Schema,
                TableName = table.Name,
                ClassName = className,
                UseEfCoreAttributes = useEfCoreAttributes
            };

            _context.TableAliases.Add(entity);
        }
        else
        {
            entity.ClassName = className;
            entity.UseEfCoreAttributes = useEfCoreAttributes;
        }

        foreach (var property in properties)
        {
            var columnEntity = entity.Columns
                .SingleOrDefault(x => x.ColumnName == property.ColumnName);

            if (columnEntity is null)
            {
                columnEntity = new ColumnAliasEntity
                {
                    ColumnName = property.ColumnName,
                    PropertyName = property.PropertyName,
                    AccessorKind = property.AccessorKind.ToString()
                };

                entity.Columns.Add(columnEntity);
            }
            else
            {
                columnEntity.PropertyName = property.PropertyName;
                columnEntity.AccessorKind = property.AccessorKind.ToString();
            }
        }

        RemoveDeletedColumns(entity, properties);

        await _context.SaveChangesAsync(ct);
    }

    private static void RemoveDeletedColumns(
        TableAliasEntity entity,
        IReadOnlyList<PropertyDefinition> properties)
    {
        var currentColumnNames = properties
            .Select(x => x.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var deletedColumns = entity.Columns
            .Where(x => !currentColumnNames.Contains(x.ColumnName))
            .ToList();

        foreach (var deletedColumn in deletedColumns)
        {
            entity.Columns.Remove(deletedColumn);
        }
    }
}
```


Der zusätzliche `RemoveDeletedColumns`-Teil ist optional, aber sinnvoll. Wenn eine Spalte in SQL Server gelöscht wurde, bleibt sonst ein alter Alias in SQLite liegen.

---

## 8. `TableEntry` sollte Schema und Name enthalten

Falls noch nicht vorhanden:

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

internal sealed class TableEntry
{
    public required string Schema { get; init; }

    public required string Name { get; init; }

    public string DisplayName => $"{Schema}.{Name}";
}
```


Für WPF ist `DisplayName` praktisch, damit Tabellen nicht doppelt oder unklar angezeigt werden.

---

## 9. Bug in deiner aktuellen `LoadTableDefinitionAsync`-Methode

In deinem aktuellen Code ist dieser Teil problematisch:

```csharp
new
{
    TableName = tableName
}
```


`tableName` ist ein `TableEntry`, aber SQL erwartet einen Tabellenname-String.

Außerdem filterst du aktuell nur nach `TABLE_NAME`, nicht nach `TABLE_SCHEMA`.

Das sollte so sein:

```csharp
new
{
    Schema = tableName.Schema,
    TableName = tableName.Name
}
```


und im SQL:

```sql
WHERE 
    c.TABLE_SCHEMA = @Schema
    AND c.TABLE_NAME = @TableName
```


Sonst bekommst du falsche Ergebnisse, wenn mehrere Schemas dieselbe Tabelle enthalten.

---

## 10. Aktualisierte `LoadTableDefinitionAsync`

```csharp
public async Task<TableDefinition> LoadTableDefinitionAsync(
    DatabaseConnectionProfile profile,
    TableEntry table,
    CancellationToken ct = default)
{
    const string query =
        """
        SELECT
            c.COLUMN_NAME AS ColumnName,
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

    using var connection = _connectionFactory.CreateConnection(profile);

    var command = new CommandDefinition(
        query,
        new
        {
            Schema = table.Schema,
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
```


---

## 11. ViewModel-Erweiterung

Im ViewModel brauchst du zusätzliche Eingaben:

```csharp
[ObservableProperty]
private string serverName = ".";

[ObservableProperty]
private string databaseName = string.Empty;

[ObservableProperty]
private bool useIntegratedSecurity = true;

[ObservableProperty]
private string userName = string.Empty;

[ObservableProperty]
private string password = string.Empty;

[ObservableProperty]
private bool trustServerCertificate = true;

[ObservableProperty]
private bool encrypt = true;
```


Dann baust du daraus ein Profil:

```csharp
private DatabaseConnectionProfile CreateConnectionProfile()
{
    return new DatabaseConnectionProfile
    {
        ServerName = ServerName,
        DatabaseName = DatabaseName,
        UseIntegratedSecurity = UseIntegratedSecurity,
        UserName = string.IsNullOrWhiteSpace(UserName) ? null : UserName,
        Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
        TrustServerCertificate = TrustServerCertificate,
        Encrypt = Encrypt
    };
}
```


Verwendung beim Laden:

```csharp
[RelayCommand]
private async Task LoadTablesAsync()
{
    var profile = CreateConnectionProfile();

    var tables = await _schemaReader.LoadTableNamesAsync(profile);

    Tables.Clear();

    foreach (var table in tables)
    {
        Tables.Add(table);
    }
}
```


Beim Laden der Tabelle:

```csharp
[RelayCommand]
private async Task LoadSelectedTableAsync()
{
    if (SelectedTable is null)
        return;

    var profile = CreateConnectionProfile();

    var tableDefinition = await _schemaReader.LoadTableDefinitionAsync(
        profile,
        SelectedTable);

    var alias = await _aliasService.LoadTableAliasAsync(
        profile,
        SelectedTable);

    ClassName = alias?.ClassName
        ?? _nameService.ToClassName(SelectedTable.Name);

    UseEfCoreAttributes = alias?.UseEfCoreAttributes ?? true;

    var properties = _propertyFactory.CreateProperties(
        tableDefinition,
        alias);

    Properties.Clear();

    foreach (var property in properties)
    {
        Properties.Add(new PropertyEditorViewModel(property));
    }
}
```


Beim Speichern/Generieren:

```csharp
[RelayCommand]
private async Task GenerateAsync()
{
    if (SelectedTable is null)
        return;

    var profile = CreateConnectionProfile();

    var properties = Properties
        .Select(x => x.ToPropertyDefinition())
        .ToList();

    await _aliasService.SaveAsync(
        profile,
        SelectedTable,
        ClassName,
        UseEfCoreAttributes,
        properties);

    var options = new ClassGenerationOptions
    {
        NamespaceName = NamespaceName,
        SchemaName = SelectedTable.Schema,
        TableName = SelectedTable.Name,
        ClassName = ClassName,
        UseEfCoreAttributes = UseEfCoreAttributes,
        Properties = properties
    };

    GeneratedCode = _classGenerator.Generate(options);
}
```


---

## 12. Datenbanken eines Servers laden

Wenn der Benutzer zuerst nur Server + Auth eingibt, kannst du die verfügbaren Datenbanken laden.

Dafür verwendest du eine Verbindung auf `master`.

```csharp
public async Task<IReadOnlyList<string>> LoadDatabaseNamesAsync(
    DatabaseConnectionProfile serverProfile,
    CancellationToken ct = default)
{
    var profile = serverProfile with
    {
        DatabaseName = "master"
    };

    const string query =
        """
        SELECT
            name
        FROM
            sys.databases
        WHERE
            state_desc = 'ONLINE'
            AND HAS_DBACCESS(name) = 1
        ORDER BY
            name;
        """;

    using var connection = _connectionFactory.CreateConnection(profile);

    var command = new CommandDefinition(
        query,
        cancellationToken: ct);

    var databaseNames = await connection.QueryAsync<string>(command);

    return [.. databaseNames];
}
```


Wichtig: `HAS_DBACCESS(name) = 1` verhindert, dass du Datenbanken anzeigst, auf die der Benutzer keinen Zugriff hat.

---

## 13. WPF-Maske erweitern

Grob:

```xml
<StackPanel Orientation="Horizontal">
    <TextBlock Text="Server:" />
    <TextBox Width="180"
             Text="{Binding ServerName, UpdateSourceTrigger=PropertyChanged}" />

    <CheckBox Content="Windows Auth"
              IsChecked="{Binding UseIntegratedSecurity}" />

    <TextBlock Text="User:" />
    <TextBox Width="120"
             Text="{Binding UserName, UpdateSourceTrigger=PropertyChanged}" />

    <TextBlock Text="Passwort:" />
    <PasswordBox Width="120" />

    <Button Content="Datenbanken laden"
            Command="{Binding LoadDatabasesCommand}" />

    <ComboBox Width="180"
              ItemsSource="{Binding DatabaseNames}"
              SelectedItem="{Binding DatabaseName}" />

    <Button Content="Tabellen laden"
            Command="{Binding LoadTablesCommand}" />
</StackPanel>
```


Hinweis: `PasswordBox` bindet nicht direkt sauber an `Password`. Dafür brauchst du entweder:

- Attached Property
- Code-behind forwarding
- oder du übergibst das Passwort explizit beim Command

Für ein internes Tool ist Code-behind hier völlig okay.

---

## 14. SQLite Migration

Weil du `TableAliasEntity` geändert hast, brauchst du eine Migration.

Beispiel-Migration konzeptionell:

```csharp
migrationBuilder.AddColumn<string>(
    name: "ServerName",
    table: "TableAliases",
    type: "TEXT",
    maxLength: 256,
    nullable: false,
    defaultValue: "");

migrationBuilder.RenameColumn(
    name: "Database",
    table: "TableAliases",
    newName: "DatabaseName");

migrationBuilder.AddColumn<string>(
    name: "SchemaName",
    table: "TableAliases",
    type: "TEXT",
    maxLength: 128,
    nullable: false,
    defaultValue: "dbo");

migrationBuilder.CreateIndex(
    name: "IX_TableAliases_ServerName_DatabaseName_SchemaName_TableName",
    table: "TableAliases",
    columns: new[] { "ServerName", "DatabaseName", "SchemaName", "TableName" },
    unique: true);
```


Falls du noch keine produktiven Aliasdaten hast, ist es einfacher:

```shell script
dotnet ef database drop
dotnet ef migrations remove
dotnet ef migrations add InitialAliasDatabase
dotnet ef database update
```


Wenn schon Daten existieren, solltest du migrieren.

---

## 15. Optional: Server normalisieren

Servernamen können verschieden geschrieben sein:

```plain text
.
localhost
(local)
MACHINE\SQLEXPRESS
machine\sqlexpress
```


Wenn du Aliase möglichst stabil speichern willst, kannst du einen separaten `ServerKey` speichern.

Beispiel:

```csharp
public static string NormalizeServerName(string serverName)
{
    return serverName.Trim().ToUpperInvariant();
}
```


Dann `TableAliasEntity`:

```csharp
public required string ServerKey { get; set; }
public required string ServerName { get; set; }
```


Und im Aliasvergleich verwendest du `ServerKey`.

Für den Anfang reicht aber meistens `ServerName.Trim()`.

---

## 16. Empfohlener finaler Schlüssel für Aliase

Für deine Anforderung würde ich diesen Alias-Schlüssel verwenden:

```plain text
ServerName + DatabaseName + SchemaName + TableName + ColumnName
```


Auf Tabellenebene:

```plain text
ServerName + DatabaseName + SchemaName + TableName
```


Auf Spaltenebene reicht dann:

```plain text
TableAliasId + ColumnName
```


Das ist genau die Struktur:

```plain text
TableAliases
    Id
    ServerName
    DatabaseName
    SchemaName
    TableName
    ClassName
    UseEfCoreAttributes

ColumnAliases
    Id
    TableAliasId
    ColumnName
    PropertyName
    AccessorKind
```


---

## Kurzantwort

Du musst dein Programm so erweitern:

1. **Connection-Daten als Benutzereingabe modellieren**
   - Server
   - Datenbank
   - Authentifizierung

2. **`SqlConnectionFactory` dynamisch machen**
   - ConnectionString aus Benutzerangaben bauen

3. **`MsSqlSchemaReader` pro Aufruf mit einem `DatabaseConnectionProfile` arbeiten lassen**

4. **SQLite-Aliasdaten um Server, Datenbank und Schema erweitern**

5. **`AliasService` nicht mehr nach `Database + TableName`, sondern nach `Server + Database + Schema + Table` suchen und speichern**

Dadurch kann der Benutzer beliebige Server und Datenbanken verwenden, und deine gespeicherten Aliasnamen bleiben eindeutig und wiederverwendbar.