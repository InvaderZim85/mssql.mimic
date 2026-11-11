---
tags:
  - mssql
  - classgenerator
---
Ja, das lässt sich gut als **WPF-Tool** aufbauen. Für dein Programm würde ich die Logik grob in vier Bereiche aufteilen:

1. **Datenbank-Metadaten laden**
   - Tabellen aus `INFORMATION_SCHEMA.TABLES`
   - Spalten aus `INFORMATION_SCHEMA.COLUMNS`
   - Primärschlüssel aus `INFORMATION_SCHEMA.TABLE_CONSTRAINTS` / `KEY_COLUMN_USAGE`

2. **UI**
   - Connection String eingeben
   - Tabellen laden
   - Tabelle auswählen
   - Spalten anzeigen
   - Pro Spalte:
     - Originalname
     - Alternativer Property-Name
     - C#-Datentyp
     - `get; set;` oder `get; init;`
     - optional ignorieren
   - Checkbox: EF-Core-Attribute erzeugen

3. **Generator**
   - SQL-Typen nach C# mappen
   - Property-Namen generieren
   - EF-Core-Attribute hinzufügen
   - Klasse als Text erzeugen

4. **Export**
   - generierten Code anzeigen
   - optional als `.cs`-Datei speichern

---

## Beispiel für die spätere Bedienung

Du wählst z. B. die Tabelle `Customers`.

Die Spalten könnten so aussehen:

| SQL-Spalte | Property-Name | Typ | Accessor |
|---|---|---|---|
| `CustomerId` | `Id` | `int` | `get; set;` |
| `FirstName` | `FirstName` | `string` | `get; init;` |
| `CreatedAt` | `CreatedAt` | `DateTime` | `get; set;` |

Mit EF-Core-Attributen könnte daraus werden:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeneratedModels;

[Table("Customers")]
public class Customer
{
    [Key]
    [Column("CustomerId")]
    public int Id { get; set; }

    [Column("FirstName")]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
```


Ohne EF-Core-Attribute:

```csharp
using System;

namespace GeneratedModels;

public class Customer
{
    public int Id { get; set; }

    public string FirstName { get; init; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
```


---

## Datenmodell für Tabellen und Spalten

Du könntest intern mit diesen Klassen arbeiten:

```csharp
public sealed class DatabaseTable
{
    public string Schema { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string FullName => $"{Schema}.{Name}";
}
```


```csharp
public sealed class DatabaseColumn
{
    public string ColumnName { get; set; } = string.Empty;

    public string PropertyName { get; set; } = string.Empty;

    public string SqlType { get; set; } = string.Empty;

    public string CSharpType { get; set; } = string.Empty;

    public bool IsNullable { get; set; }

    public bool IsPrimaryKey { get; set; }

    public int? MaxLength { get; set; }

    public bool Include { get; set; } = true;

    public PropertyAccessorKind AccessorKind { get; set; } = PropertyAccessorKind.GetSet;
}
```


```csharp
public enum PropertyAccessorKind
{
    GetSet,
    GetInit
}
```


---

## Tabellen über `INFORMATION_SCHEMA` laden

```csharp
public async Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(string connectionString)
{
    const string sql = """
        SELECT 
            TABLE_SCHEMA,
            TABLE_NAME
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_SCHEMA, TABLE_NAME;
        """;

    var result = new List<DatabaseTable>();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        result.Add(new DatabaseTable
        {
            Schema = reader.GetString(0),
            Name = reader.GetString(1)
        });
    }

    return result;
}
```


Dafür brauchst du z. B.:

```csharp
using Microsoft.Data.SqlClient;
```


---

## Spalten einer Tabelle laden

```csharp
public async Task<IReadOnlyList<DatabaseColumn>> GetColumnsAsync(
    string connectionString,
    string schema,
    string tableName)
{
    const string sql = """
        SELECT
            c.COLUMN_NAME,
            c.DATA_TYPE,
            c.IS_NULLABLE,
            c.CHARACTER_MAXIMUM_LENGTH,
            CASE 
                WHEN ku.COLUMN_NAME IS NOT NULL THEN 1 
                ELSE 0 
            END AS IS_PRIMARY_KEY
        FROM INFORMATION_SCHEMA.COLUMNS c
        LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
            ON ku.TABLE_SCHEMA = c.TABLE_SCHEMA
            AND ku.TABLE_NAME = c.TABLE_NAME
            AND ku.COLUMN_NAME = c.COLUMN_NAME
        LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            ON tc.CONSTRAINT_SCHEMA = ku.CONSTRAINT_SCHEMA
            AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
            AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
        WHERE c.TABLE_SCHEMA = @Schema
          AND c.TABLE_NAME = @TableName
        ORDER BY c.ORDINAL_POSITION;
        """;

    var result = new List<DatabaseColumn>();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new SqlCommand(sql, connection);
    command.Parameters.AddWithValue("@Schema", schema);
    command.Parameters.AddWithValue("@TableName", tableName);

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var columnName = reader.GetString(0);
        var sqlType = reader.GetString(1);
        var isNullable = reader.GetString(2) == "YES";
        var maxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3);
        var isPrimaryKey = reader.GetInt32(4) == 1;

        result.Add(new DatabaseColumn
        {
            ColumnName = columnName,
            PropertyName = ToPascalCase(columnName),
            SqlType = sqlType,
            CSharpType = MapSqlTypeToCSharp(sqlType, isNullable),
            IsNullable = isNullable,
            MaxLength = maxLength is > 0 ? maxLength : null,
            IsPrimaryKey = isPrimaryKey
        });
    }

    return result;
}
```


---

## SQL-Typen nach C# mappen

```csharp
private static string MapSqlTypeToCSharp(string sqlType, bool isNullable)
{
    var type = sqlType.ToLowerInvariant() switch
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
        "time" => "TimeOnly",
        "datetimeoffset" => "DateTimeOffset",
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

    if (type is "string" or "byte[]")
    {
        return isNullable ? $"{type}?" : type;
    }

    return isNullable ? $"{type}?" : type;
}
```


---

## Generator für die C#-Klasse

```csharp
public sealed class ClassGenerator
{
    public string Generate(
        string namespaceName,
        string className,
        string tableName,
        IReadOnlyList<DatabaseColumn> columns,
        bool useEfCoreAttributes)
    {
        var builder = new StringBuilder();

        builder.AppendLine("using System;");

        if (useEfCoreAttributes)
        {
            builder.AppendLine("using System.ComponentModel.DataAnnotations;");
            builder.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
        }

        builder.AppendLine();
        builder.AppendLine($"namespace {namespaceName};");
        builder.AppendLine();

        if (useEfCoreAttributes)
        {
            builder.AppendLine($"[Table(\"{tableName}\")]");
        }

        builder.AppendLine($"public class {className}");
        builder.AppendLine("{");

        foreach (var column in columns.Where(x => x.Include))
        {
            AppendProperty(builder, column, useEfCoreAttributes);
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendProperty(
        StringBuilder builder,
        DatabaseColumn column,
        bool useEfCoreAttributes)
    {
        builder.AppendLine();

        if (useEfCoreAttributes)
        {
            if (column.IsPrimaryKey)
            {
                builder.AppendLine("    [Key]");
            }

            builder.AppendLine($"    [Column(\"{column.ColumnName}\")]");

            if (column.MaxLength is > 0 && IsStringType(column.CSharpType))
            {
                builder.AppendLine($"    [MaxLength({column.MaxLength})]");
            }
        }

        var accessor = column.AccessorKind switch
        {
            PropertyAccessorKind.GetInit => "get; init;",
            _ => "get; set;"
        };

        var defaultValue = GetDefaultValue(column.CSharpType);

        builder.AppendLine($"    public {column.CSharpType} {column.PropertyName} {{ {accessor} }}{defaultValue}");
    }

    private static bool IsStringType(string csharpType)
    {
        return csharpType is "string" or "string?";
    }

    private static string GetDefaultValue(string csharpType)
    {
        return csharpType switch
        {
            "string" => " = string.Empty;",
            "byte[]" => " = [];",
            _ => string.Empty
        };
    }
}
```


---

## Hilfsmethode für Property-Namen

```csharp
private static string ToPascalCase(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    var parts = value
        .Split(['_', ' ', '-'], StringSplitOptions.RemoveEmptyEntries);

    return string.Concat(parts.Select(part =>
        char.ToUpperInvariant(part[0]) + part[1..]));
}
```


Beispiele:

| SQL-Spalte | Property |
|---|---|
| `customer_id` | `CustomerId` |
| `first_name` | `FirstName` |
| `CreatedAt` | `CreatedAt` |

---

## Sinnvolle WPF-Oberfläche

Die Oberfläche könnte ungefähr so aufgebaut sein:

- oben: Connection String
- Button: Tabellen laden
- links: Tabelle auswählen
- Mitte: Spalten bearbeiten
- rechts/unten: generierter Code

Beispiel-Struktur:

```xaml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
        <RowDefinition Height="2*" />
    </Grid.RowDefinitions>

    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="10">
        <TextBox Width="500"
                 Text="{Binding ConnectionString, UpdateSourceTrigger=PropertyChanged}" />

        <Button Content="Tabellen laden"
                Command="{Binding LoadTablesCommand}"
                Margin="10,0,0,0" />

        <CheckBox Content="EF-Core Attribute"
                  IsChecked="{Binding UseEfCoreAttributes}"
                  Margin="10,0,0,0" />
    </StackPanel>

    <Grid Grid.Row="1" Margin="10">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="250" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>

        <ListBox Grid.Column="0"
                 ItemsSource="{Binding Tables}"
                 SelectedItem="{Binding SelectedTable}"
                 DisplayMemberPath="FullName" />

        <DataGrid Grid.Column="1"
                  ItemsSource="{Binding Columns}"
                  AutoGenerateColumns="False"
                  Margin="10,0,0,0">
            <DataGrid.Columns>
                <DataGridCheckBoxColumn Header="Nutzen"
                                        Binding="{Binding Include}" />

                <DataGridTextColumn Header="SQL-Spalte"
                                    Binding="{Binding ColumnName}"
                                    IsReadOnly="True" />

                <DataGridTextColumn Header="Property-Name"
                                    Binding="{Binding PropertyName}" />

                <DataGridTextColumn Header="C# Typ"
                                    Binding="{Binding CSharpType}" />

                <DataGridComboBoxColumn Header="Accessor"
                                        SelectedItemBinding="{Binding AccessorKind}"
                                        ItemsSource="{Binding Source={StaticResource AccessorKinds}}" />
            </DataGrid.Columns>
        </DataGrid>
    </Grid>

    <TextBox Grid.Row="2"
             Text="{Binding GeneratedCode}"
             FontFamily="Consolas"
             AcceptsReturn="True"
             AcceptsTab="True"
             VerticalScrollBarVisibility="Auto"
             HorizontalScrollBarVisibility="Auto"
             Margin="10" />
</Grid>
```


---

## Empfohlene NuGet-Pakete

Für die SQL-Verbindung:

```textmate
Install-Package Microsoft.Data.SqlClient
```


Für EF-Core-Attribute brauchst du im Generator selbst nicht zwingend EF-Core, sondern nur:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
```


Diese Attribute kommen aus .NET und sind für viele EF-Core-Szenarien ausreichend.

---

## Wichtige EF-Core-Attribute

Für dein Tool wären diese besonders sinnvoll:

| Attribut | Zweck |
|---|---|
| `[Table("TableName")]` | Tabellenname |
| `[Column("ColumnName")]` | Spaltenname |
| `[Key]` | Primärschlüssel |
| `[MaxLength(100)]` | Länge bei `nvarchar`, `varchar`, usw. |
| `[Required]` | Pflichtfeld |
| `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]` | Identity-Spalte |

Optional könntest du später auch Identity-Spalten auslesen über:

```sql
COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity')
```


Das geht allerdings nicht rein über `INFORMATION_SCHEMA`, sondern über SQL-Server-spezifische Funktionen.

---

## Empfehlung

Ich würde die erste Version so bauen:

1. Connection String eingeben
2. Tabellen laden
3. Tabelle auswählen
4. Spalten laden
5. Property-Namen bearbeiten
6. Accessor pro Property auswählen
7. EF-Core-Checkbox
8. Code generieren

Danach kannst du erweitern um:

- Datei speichern
- Namespace frei eingeben
- Klassenname frei eingeben
- Nullable Reference Types berücksichtigen
- Identity-Spalten erkennen
- `[Required]` generieren
- mehrere Tabellen auf einmal generieren
- Navigation Properties erzeugen
- Fluent-API statt Attributes erzeugen