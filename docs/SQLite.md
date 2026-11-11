---
tags:
  - mssql
  - classgenerator
---
Ja, das ist eine sehr sinnvolle Erweiterung. Ich würde den Alias **nicht direkt in `DatabaseColumn` speichern**, sondern als eigene Persistenz-/Konfigurationsinformation behandeln.

Der Grund: `DatabaseColumn` beschreibt die echte Datenbankstruktur. Der Alias ist eine **Benutzerentscheidung** deines Tools.

---

## Empfohlene Erweiterung der Architektur

Ergänze deine Architektur um einen kleinen Alias-Bereich:

```plain text
Domain
├─ Models
│  ├─ DatabaseTable.cs
│  ├─ DatabaseColumn.cs
│  ├─ ColumnAlias.cs
│  └─ PropertyDefinition.cs
│
Application
├─ Services
│  ├─ ITableMetadataService.cs
│  ├─ IColumnAliasService.cs
│  └─ IClassGenerationService.cs
│
Infrastructure
├─ SqlServer
│  └─ SqlServerTableMetadataService.cs
│
└─ SQLite
   ├─ SqliteColumnAliasService.cs
   └─ SqliteDatabaseInitializer.cs
```


---

# 1. Alias als eigenes Domain-Modell

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

/// <summary>
/// Stores a user-defined alias for a database column.
/// </summary>
internal sealed class ColumnAlias
{
    public required string ServerName { get; init; }

    public required string DatabaseName { get; init; }

    public required string SchemaName { get; init; }

    public required string TableName { get; init; }

    public required string ColumnName { get; init; }

    public required string Alias { get; init; }
}
```


Damit kannst du Aliase eindeutig speichern für:

```plain text
Server + Datenbank + Schema + Tabelle + Spalte
```


Das ist wichtig, weil es theoretisch mehrere Tabellen mit gleichnamigen Spalten geben kann, z. B.:

```plain text
dbo.Customer.Id
dbo.Order.Id
```


---

# 2. Interface für Alias-Zugriff

In der Application-Schicht:

```csharp
using MsSql.ClassGenerator.Domain.Models;

namespace MsSql.ClassGenerator.Application.Services;

internal interface IColumnAliasService
{
    Task<IReadOnlyDictionary<string, string>> GetAliasesAsync(
        string serverName,
        string databaseName,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default);

    Task SaveAliasAsync(
        ColumnAlias alias,
        CancellationToken cancellationToken = default);

    Task SaveAliasesAsync(
        IReadOnlyList<ColumnAlias> aliases,
        CancellationToken cancellationToken = default);

    Task DeleteAliasAsync(
        string serverName,
        string databaseName,
        string schemaName,
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default);
}
```


`GetAliasesAsync` kann ein Dictionary zurückgeben:

```csharp
ColumnName -> Alias
```


Beispiel:

```plain text
customer_id -> CustomerId
created_at  -> CreatedAt
```


---

# 3. SQLite Tabellenstruktur

Für SQLite würde ich eine Tabelle `ColumnAliases` verwenden:

```sql
CREATE TABLE IF NOT EXISTS ColumnAliases
(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ServerName TEXT NOT NULL,
    DatabaseName TEXT NOT NULL,
    SchemaName TEXT NOT NULL,
    TableName TEXT NOT NULL,
    ColumnName TEXT NOT NULL,
    Alias TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,

    UNIQUE
    (
        ServerName,
        DatabaseName,
        SchemaName,
        TableName,
        ColumnName
    )
);
```


Der `UNIQUE`-Index ist wichtig, damit pro Spalte nur ein Alias gespeichert wird.

---

# 4. SQLite Initializer

```csharp
using Microsoft.Data.Sqlite;

namespace MsSql.ClassGenerator.Infrastructure.SQLite;

internal sealed class SqliteDatabaseInitializer
{
    private readonly string _connectionString;

    public SqliteDatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS ColumnAliases
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ServerName TEXT NOT NULL,
                DatabaseName TEXT NOT NULL,
                SchemaName TEXT NOT NULL,
                TableName TEXT NOT NULL,
                ColumnName TEXT NOT NULL,
                Alias TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,

                UNIQUE
                (
                    ServerName,
                    DatabaseName,
                    SchemaName,
                    TableName,
                    ColumnName
                )
            );
            """;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```


---

# 5. SQLite Alias-Service

```csharp
using Microsoft.Data.Sqlite;
using MsSql.ClassGenerator.Application.Services;
using MsSql.ClassGenerator.Domain.Models;

namespace MsSql.ClassGenerator.Infrastructure.SQLite;

internal sealed class SqliteColumnAliasService : IColumnAliasService
{
    private readonly string _connectionString;

    public SqliteColumnAliasService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAliasesAsync(
        string serverName,
        string databaseName,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ColumnName, Alias
            FROM ColumnAliases
            WHERE ServerName = $ServerName
              AND DatabaseName = $DatabaseName
              AND SchemaName = $SchemaName
              AND TableName = $TableName;
            """;

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$ServerName", serverName);
        command.Parameters.AddWithValue("$DatabaseName", databaseName);
        command.Parameters.AddWithValue("$SchemaName", schemaName);
        command.Parameters.AddWithValue("$TableName", tableName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            aliases[reader.GetString(0)] = reader.GetString(1);
        }

        return aliases;
    }

    public async Task SaveAliasAsync(
        ColumnAlias alias,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ColumnAliases
            (
                ServerName,
                DatabaseName,
                SchemaName,
                TableName,
                ColumnName,
                Alias,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                $ServerName,
                $DatabaseName,
                $SchemaName,
                $TableName,
                $ColumnName,
                $Alias,
                $Now,
                $Now
            )
            ON CONFLICT
            (
                ServerName,
                DatabaseName,
                SchemaName,
                TableName,
                ColumnName
            )
            DO UPDATE SET
                Alias = excluded.Alias,
                UpdatedAt = excluded.UpdatedAt;
            """;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var now = DateTimeOffset.UtcNow.ToString("O");

        command.Parameters.AddWithValue("$ServerName", alias.ServerName);
        command.Parameters.AddWithValue("$DatabaseName", alias.DatabaseName);
        command.Parameters.AddWithValue("$SchemaName", alias.SchemaName);
        command.Parameters.AddWithValue("$TableName", alias.TableName);
        command.Parameters.AddWithValue("$ColumnName", alias.ColumnName);
        command.Parameters.AddWithValue("$Alias", alias.Alias);
        command.Parameters.AddWithValue("$Now", now);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveAliasesAsync(
        IReadOnlyList<ColumnAlias> aliases,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var alias in aliases)
        {
            await SaveAliasCoreAsync(connection, transaction, alias, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAliasAsync(
        string serverName,
        string databaseName,
        string schemaName,
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM ColumnAliases
            WHERE ServerName = $ServerName
              AND DatabaseName = $DatabaseName
              AND SchemaName = $SchemaName
              AND TableName = $TableName
              AND ColumnName = $ColumnName;
            """;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$ServerName", serverName);
        command.Parameters.AddWithValue("$DatabaseName", databaseName);
        command.Parameters.AddWithValue("$SchemaName", schemaName);
        command.Parameters.AddWithValue("$TableName", tableName);
        command.Parameters.AddWithValue("$ColumnName", columnName);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SaveAliasCoreAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ColumnAlias alias,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO ColumnAliases
            (
                ServerName,
                DatabaseName,
                SchemaName,
                TableName,
                ColumnName,
                Alias,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                $ServerName,
                $DatabaseName,
                $SchemaName,
                $TableName,
                $ColumnName,
                $Alias,
                $Now,
                $Now
            )
            ON CONFLICT
            (
                ServerName,
                DatabaseName,
                SchemaName,
                TableName,
                ColumnName
            )
            DO UPDATE SET
                Alias = excluded.Alias,
                UpdatedAt = excluded.UpdatedAt;
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        var now = DateTimeOffset.UtcNow.ToString("O");

        command.Parameters.AddWithValue("$ServerName", alias.ServerName);
        command.Parameters.AddWithValue("$DatabaseName", alias.DatabaseName);
        command.Parameters.AddWithValue("$SchemaName", alias.SchemaName);
        command.Parameters.AddWithValue("$TableName", alias.TableName);
        command.Parameters.AddWithValue("$ColumnName", alias.ColumnName);
        command.Parameters.AddWithValue("$Alias", alias.Alias);
        command.Parameters.AddWithValue("$Now", now);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```


---

# 6. ViewModel: Alias laden und anzeigen

In deinem `ColumnMappingViewModel` solltest du zwischen Originalname und Alias unterscheiden.

```csharp
internal sealed class ColumnMappingViewModel : ObservableObject
{
    private string _alias;
    private bool _include = true;
    private PropertyAccessorKind _accessorKind = PropertyAccessorKind.GetSet;

    public ColumnMappingViewModel(
        DatabaseColumn column,
        string defaultPropertyName,
        string? storedAlias)
    {
        Column = column;
        _alias = string.IsNullOrWhiteSpace(storedAlias)
            ? defaultPropertyName
            : storedAlias;
    }

    public DatabaseColumn Column { get; }

    public string ColumnName => Column.Name;

    public string Alias
    {
        get => _alias;
        set => SetProperty(ref _alias, value);
    }

    public bool Include
    {
        get => _include;
        set => SetProperty(ref _include, value);
    }

    public PropertyAccessorKind AccessorKind
    {
        get => _accessorKind;
        set => SetProperty(ref _accessorKind, value);
    }
}
```


Beim Generieren verwendest du dann:

```csharp
PropertyName = column.Alias
```


---

# 7. Ablauf beim Auswählen einer Tabelle

Wenn der Benutzer eine Tabelle auswählt:

```plain text
1. Spalten aus MS SQL laden
2. Aliase aus SQLite laden
3. Für jede Spalte:
   - Wenn Alias vorhanden: Alias verwenden
   - Sonst: Standardname generieren
4. Spalten im DataGrid anzeigen
```


Pseudo-Code:

```csharp
private async Task LoadColumnsAsync(DatabaseTable table)
{
    var columns = await _tableMetadataService.GetColumnsAsync(
        ConnectionString,
        table);

    var aliases = await _columnAliasService.GetAliasesAsync(
        ServerName,
        DatabaseName,
        table.Schema,
        table.Name);

    Columns.Clear();

    foreach (var column in columns)
    {
        aliases.TryGetValue(column.Name, out var storedAlias);

        var defaultPropertyName = _nameService.ToPropertyName(column.Name);
        var csharpType = _typeMapper.Map(column.SqlType, column.IsNullable);

        Columns.Add(new ColumnMappingViewModel(
            column,
            defaultPropertyName,
            storedAlias));
    }
}
```


---

# 8. Alias speichern

Ich würde die Aliase entweder speichern:

## Variante A: über Button „Aliase speichern“

Das ist am transparentesten.

```plain text
[Aliase speichern]
```


Beim Klick speicherst du alle Aliaswerte.

```csharp
private async Task SaveAliasesAsync()
{
    if (SelectedTable is null)
    {
        return;
    }

    var aliases = Columns
        .Where(column => !string.IsNullOrWhiteSpace(column.Alias))
        .Select(column => new ColumnAlias
        {
            ServerName = ServerName,
            DatabaseName = DatabaseName,
            SchemaName = SelectedTable.Schema,
            TableName = SelectedTable.Name,
            ColumnName = column.ColumnName,
            Alias = column.Alias.Trim()
        })
        .ToList();

    await _columnAliasService.SaveAliasesAsync(aliases);
}
```


## Variante B: automatisch beim Ändern

Das geht auch, aber ich würde es für die erste Version nicht machen.

Warum?

- weniger Kontrolle
- mehr Schreibzugriffe
- Fehlerbehandlung schwieriger
- bei Tippfehlern wird sofort gespeichert

Meine Empfehlung: **Button „Aliase speichern“** plus optional später Auto-Save.

---

# 9. UI im DataGrid

Im DataGrid solltest du eine Spalte `Alias` haben:

```xml
<DataGridTextColumn Header="SQL-Spalte"
                    Binding="{Binding ColumnName}"
                    IsReadOnly="True" />

<DataGridTextColumn Header="Alias / Property-Name"
                    Binding="{Binding Alias, UpdateSourceTrigger=PropertyChanged}" />
```


Optional kannst du zusätzlich anzeigen:

```xml
<DataGridTextColumn Header="Standardname"
                    Binding="{Binding DefaultPropertyName}"
                    IsReadOnly="True" />
```


Dann sieht der Benutzer, was automatisch generiert worden wäre.

---

# 10. Wichtiger Punkt: Alias validieren

Da der Alias später ein C#-Property-Name wird, solltest du prüfen:

- nicht leer
- beginnt mit Buchstabe oder `_`
- enthält nur Buchstaben, Zahlen oder `_`
- kein C#-Keyword
- innerhalb der Klasse eindeutig

Beispiel-Service:

```csharp
internal sealed class CSharpIdentifierValidator
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "class",
        "namespace",
        "public",
        "private",
        "protected",
        "internal",
        "void",
        "string",
        "int",
        "decimal",
        "object",
        "event",
        "base",
        "new",
        "return",
        "switch",
        "case",
        "default",
        "init",
        "set",
        "get"
    };

    public bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        if (Keywords.Contains(value))
        {
            return false;
        }

        return value.All(character =>
            char.IsLetterOrDigit(character) || character == '_');
    }
}
```


---

# 11. SQLite-Dateiablage

Für eine Desktop-App bietet sich z. B. an:

```plain text
%AppData%\MsSql.ClassGenerator\aliases.db
```


In C#:

```csharp
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var directory = Path.Combine(appData, "MsSql.ClassGenerator");

Directory.CreateDirectory(directory);

var databasePath = Path.Combine(directory, "aliases.db");
var connectionString = $"Data Source={databasePath}";
```


---

# 12. NuGet-Paket

Für SQLite brauchst du:

```textmate
Install-Package Microsoft.Data.Sqlite
```


Falls du Dependency Injection verwendest zusätzlich:

```textmate
Install-Package Microsoft.Extensions.DependencyInjection
```


---

# Empfehlung

Ich würde es so einbauen:

1. `ColumnAlias` als eigenes Domain-Modell
2. `IColumnAliasService` in Application
3. `SqliteColumnAliasService` in Infrastructure
4. SQLite-Tabelle `ColumnAliases`
5. Beim Laden einer Tabelle:
   - Spalten aus MS SQL laden
   - Aliase aus SQLite laden
   - UI mit Alias vorbelegen
6. Button **„Aliase speichern“**
7. Beim Generieren wird `Alias` als Property-Name verwendet

Damit bleibt die Architektur sauber:

- SQL Server liefert nur echte Metadaten
- SQLite speichert Benutzereinstellungen
- ViewModel kombiniert beides für die UI
- Generator kennt nur fertige `PropertyDefinition`-Objekte