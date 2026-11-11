---
tags:
  - mssql
  - classgenerator
---
Eine mögliche Umsetzung kannst du in **3 Bereiche** aufteilen:

1. **MS SQL auslesen**  
   - Tabellen, Spalten, Datentypen, Nullable, Primary Keys etc.
   - per **SQL + Dapper**

2. **Alias-/Mapping-Daten lokal speichern**  
   - Tabellenalias, Spaltenalias, Accessor-Auswahl, EF-Core-Attribut-Optionen
   - per **SQLite + EF Core**

3. **C#-Code generieren**  
   - aus den SQL-Metadaten + gespeicherten Alias-Daten
   - optional mit EF-Core-Attributen
   - `get; set;` oder `get; init;`

---

## 1. Sinnvolle Projektstruktur

Beispiel:

```plain text
MsSql.ClassGenerator
│
├── Domain
│   ├── Models
│   │   ├── TableDefinition.cs
│   │   ├── ColumnDefinition.cs
│   │   ├── PropertyDefinition.cs
│   │   └── AccessorKind.cs
│   │
│   └── Services
│       ├── CSharpNameService.cs
│       └── SqlTypeMapper.cs
│
├── Application
│   ├── Options
│   │   └── ClassGenerationOptions.cs
│   │
│   └── Services
│       ├── MsSqlSchemaReader.cs
│       ├── CSharpClassGenerator.cs
│       └── AliasService.cs
│
├── Infrastructure
│   ├── Sqlite
│   │   ├── AliasDbContext.cs
│   │   ├── TableAliasEntity.cs
│   │   └── ColumnAliasEntity.cs
│   │
│   └── MsSql
│       └── SqlConnectionFactory.cs
│
└── UI
    └── WPF ViewModels
```


---

## 2. Domain-Modelle

### `AccessorKind`

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

internal enum AccessorKind
{
    GetSet,
    GetInit
}
```


### `ColumnDefinition`

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

internal sealed class ColumnDefinition
{
    public required string ColumnName { get; init; }

    public required string SqlDataType { get; init; }

    public required bool IsNullable { get; init; }

    public required bool IsPrimaryKey { get; init; }

    public required int? MaxLength { get; init; }

    public required int? Precision { get; init; }

    public required int? Scale { get; init; }
}
```


### `TableDefinition`

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

internal sealed class TableDefinition
{
    public required string SchemaName { get; init; }

    public required string TableName { get; init; }

    public required IReadOnlyList<ColumnDefinition> Columns { get; init; }
}
```


### `PropertyDefinition`

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

internal sealed class PropertyDefinition
{
    public required string ColumnName { get; init; }

    public required string PropertyName { get; init; }

    public required string CSharpTypeName { get; init; }

    public required bool IsNullable { get; init; }

    public required bool IsPrimaryKey { get; init; }

    public required int? MaxLength { get; init; }

    public required AccessorKind AccessorKind { get; init; }
}
```


---

## 3. SQL Server Metadaten mit Dapper auslesen

Du kannst `INFORMATION_SCHEMA` verwenden, für Primary Keys ist aber `sys.*` oft zuverlässiger.

### `MsSqlSchemaReader`

```csharp
using System.Data;
using Dapper;
using MsSql.ClassGenerator.Domain.Models;

namespace MsSql.ClassGenerator.Application.Services;

internal sealed class MsSqlSchemaReader
{
    private readonly IDbConnection _connection;

    public MsSqlSchemaReader(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IReadOnlyList<string>> GetTableNamesAsync(
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @SchemaName
              AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME;
            """;

        var command = new CommandDefinition(
            sql,
            new { SchemaName = schemaName },
            cancellationToken: cancellationToken);

        var result = await _connection.QueryAsync<string>(command);

        return result.AsList();
    }

    public async Task<TableDefinition> GetTableDefinitionAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.COLUMN_NAME AS ColumnName,
                c.DATA_TYPE AS SqlDataType,
                CAST(CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS bit) AS IsNullable,
                CAST(CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsPrimaryKey,
                c.CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                c.NUMERIC_PRECISION AS Precision,
                c.NUMERIC_SCALE AS Scale
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                   AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                   AND tc.TABLE_NAME = ku.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk
                ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA
               AND c.TABLE_NAME = pk.TABLE_NAME
               AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @SchemaName
              AND c.TABLE_NAME = @TableName
            ORDER BY c.ORDINAL_POSITION;
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                SchemaName = schemaName,
                TableName = tableName
            },
            cancellationToken: cancellationToken);

        var columns = await _connection.QueryAsync<ColumnDefinition>(command);

        return new TableDefinition
        {
            SchemaName = schemaName,
            TableName = tableName,
            Columns = columns.AsList()
        };
    }
}
```


---

## 4. SQL-Typen auf C#-Typen mappen

### `SqlTypeMapper`

```csharp
namespace MsSql.ClassGenerator.Domain.Services;

internal sealed class SqlTypeMapper
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

        if (typeName is "string" or "byte[]" or "object")
            return isNullable ? $"{typeName}?" : typeName;

        return isNullable ? $"{typeName}?" : typeName;
    }
}
```


---

## 5. Alias-Daten mit EF Core + SQLite speichern

Du willst speichern:

- Tabelle: Originalname, Schema, Klassenname
- Spalte: Originalname, Propertyname, Accessor
- ob EF-Core-Attribute verwendet werden sollen

### Entities

```csharp
namespace MsSql.ClassGenerator.Infrastructure.Sqlite;

internal sealed class TableAliasEntity
{
    public int Id { get; set; }

    public required string SchemaName { get; set; }

    public required string TableName { get; set; }

    public required string ClassName { get; set; }

    public bool UseEfCoreAttributes { get; set; }

    public List<ColumnAliasEntity> Columns { get; set; } = [];
}
```


```csharp
namespace MsSql.ClassGenerator.Infrastructure.Sqlite;

internal sealed class ColumnAliasEntity
{
    public int Id { get; set; }

    public int TableAliasId { get; set; }

    public TableAliasEntity? TableAlias { get; set; }

    public required string ColumnName { get; set; }

    public required string PropertyName { get; set; }

    public required string AccessorKind { get; set; }
}
```


### DbContext

```csharp
using Microsoft.EntityFrameworkCore;

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

## 6. Alias-Service

Dieser Service lädt bereits gespeicherte Aliasnamen und speichert Änderungen.

```csharp
using Microsoft.EntityFrameworkCore;
using MsSql.ClassGenerator.Domain.Models;

namespace MsSql.ClassGenerator.Application.Services;

internal sealed class AliasService
{
    private readonly AliasDbContext _dbContext;

    public AliasService(AliasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TableAliasEntity?> GetTableAliasAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TableAliases
            .Include(x => x.Columns)
            .SingleOrDefaultAsync(
                x => x.SchemaName == schemaName &&
                     x.TableName == tableName,
                cancellationToken);
    }

    public async Task SaveAsync(
        string schemaName,
        string tableName,
        string className,
        bool useEfCoreAttributes,
        IReadOnlyList<PropertyDefinition> properties,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TableAliases
            .Include(x => x.Columns)
            .SingleOrDefaultAsync(
                x => x.SchemaName == schemaName &&
                     x.TableName == tableName,
                cancellationToken);

        if (entity is null)
        {
            entity = new TableAliasEntity
            {
                SchemaName = schemaName,
                TableName = tableName,
                ClassName = className,
                UseEfCoreAttributes = useEfCoreAttributes
            };

            _dbContext.TableAliases.Add(entity);
        }

        entity.ClassName = className;
        entity.UseEfCoreAttributes = useEfCoreAttributes;

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

            columnEntity.PropertyName = property.PropertyName;
            columnEntity.AccessorKind = property.AccessorKind.ToString();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```


> Hinweis: `AliasDbContext`, `TableAliasEntity` und `ColumnAliasEntity` liegen bei diesem Beispiel im Infrastructure-Projekt. Je nach Architektur solltest du entweder Interfaces verwenden oder die Entities in einem gemeinsamen Persistence-Bereich platzieren.

---

## 7. Aus SQL-Metadaten PropertyDefinition erzeugen

Du kannst die Spalten aus SQL Server mit gespeicherten Aliases kombinieren.

```csharp
using MsSql.ClassGenerator.Domain.Models;
using MsSql.ClassGenerator.Domain.Services;
using MsSql.ClassGenerator.Infrastructure.Sqlite;

namespace MsSql.ClassGenerator.Application.Services;

internal sealed class PropertyDefinitionFactory
{
    private readonly CSharpNameService _nameService;
    private readonly SqlTypeMapper _typeMapper;

    public PropertyDefinitionFactory(
        CSharpNameService nameService,
        SqlTypeMapper typeMapper)
    {
        _nameService = nameService;
        _typeMapper = typeMapper;
    }

    public IReadOnlyList<PropertyDefinition> CreateProperties(
        TableDefinition tableDefinition,
        TableAliasEntity? aliasEntity)
    {
        return tableDefinition.Columns
            .Select(column =>
            {
                var columnAlias = aliasEntity?.Columns
                    .SingleOrDefault(x => x.ColumnName == column.ColumnName);

                var propertyName = columnAlias?.PropertyName
                    ?? _nameService.ToPropertyName(column.ColumnName);

                var accessorKind = Enum.TryParse<AccessorKind>(
                    columnAlias?.AccessorKind,
                    out var parsedAccessorKind)
                        ? parsedAccessorKind
                        : AccessorKind.GetSet;

                return new PropertyDefinition
                {
                    ColumnName = column.ColumnName,
                    PropertyName = propertyName,
                    CSharpTypeName = _typeMapper.MapToCSharpType(
                        column.SqlDataType,
                        column.IsNullable),
                    IsNullable = column.IsNullable,
                    IsPrimaryKey = column.IsPrimaryKey,
                    MaxLength = column.MaxLength,
                    AccessorKind = accessorKind
                };
            })
            .ToList();
    }
}
```


---

## 8. C# Klasse generieren

Du hast bereits eine gute Options-Klasse für die Generierung. Eine mögliche Erweiterung wäre, dass `PropertyDefinition` alle Infos enthält, die der Generator braucht.

### `CSharpClassGenerator`

```csharp
using System.Text;
using MsSql.ClassGenerator.Application.Options;
using MsSql.ClassGenerator.Domain.Models;

namespace MsSql.ClassGenerator.Application.Services;

internal sealed class CSharpClassGenerator
{
    public string Generate(ClassGenerationOptions options)
    {
        var builder = new StringBuilder();

        if (options.UseEfCoreAttributes)
        {
            builder.AppendLine("using System.ComponentModel.DataAnnotations;");
            builder.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            builder.AppendLine();
        }

        builder.AppendLine($"namespace {options.NamespaceName};");
        builder.AppendLine();

        if (options.UseEfCoreAttributes)
        {
            builder.AppendLine($"""[Table("{options.TableName}", Schema = "{options.SchemaName}")]""");
        }

        builder.AppendLine($"public sealed class {options.ClassName}");
        builder.AppendLine("{");

        foreach (var property in options.Properties)
        {
            AppendProperty(builder, property, options.UseEfCoreAttributes);
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendProperty(
        StringBuilder builder,
        PropertyDefinition property,
        bool useEfCoreAttributes)
    {
        builder.AppendLine();

        if (useEfCoreAttributes)
        {
            if (property.IsPrimaryKey)
            {
                builder.AppendLine("    [Key]");
            }

            builder.AppendLine($"""    [Column("{property.ColumnName}")]""");

            if (property.MaxLength is > 0 &&
                property.CSharpTypeName is "string" or "string?")
            {
                builder.AppendLine($"    [MaxLength({property.MaxLength.Value})]");
            }

            if (!property.IsNullable &&
                property.CSharpTypeName is "string" or "byte[]")
            {
                builder.AppendLine("    [Required]");
            }
        }

        var accessorText = property.AccessorKind switch
        {
            AccessorKind.GetSet => "get; set;",
            AccessorKind.GetInit => "get; init;",
            _ => "get; set;"
        };

        builder.AppendLine(
            $"    public {property.CSharpTypeName} {property.PropertyName} {{ {accessorText} }}");
    }
}
```


Beispielausgabe:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApp.Entities;

[Table("Customers", Schema = "dbo")]
public sealed class Customer
{
    [Key]
    [Column("CustomerId")]
    public int CustomerId { get; init; }

    [Column("First_Name")]
    [MaxLength(100)]
    [Required]
    public string FirstName { get; init; }

    [Column("BirthDate")]
    public DateOnly? BirthDate { get; init; }
}
```


---

## 9. Ablauf im Programm

Der Workflow könnte so aussehen:

```plain text
1. Nutzer gibt MS-SQL-ConnectionString ein
2. Anwendung lädt Tabellen aus SQL Server
3. Nutzer wählt Tabelle aus
4. Anwendung lädt Spaltenmetadaten per Dapper
5. Anwendung lädt gespeicherte Aliasdaten aus SQLite
6. Anwendung erzeugt Vorschläge:
   - Klassenname aus Tabellenname
   - Propertyname aus Spaltenname
   - C# Typ aus SQL Typ
   - Accessor Default: get; set; oder get; init;
7. Nutzer bearbeitet:
   - Klassenname
   - Propertynamen
   - Accessors
   - EF-Core-Attribute ja/nein
8. Anwendung speichert Aliasdaten in SQLite
9. Anwendung generiert C# Code
10. Nutzer kopiert Code oder speichert Datei
```


---

## 10. WPF ViewModel-Beispiel

Vereinfacht könnte ein ViewModel so aussehen:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsSql.ClassGenerator.Application.Options;
using MsSql.ClassGenerator.Application.Services;
using MsSql.ClassGenerator.Domain.Models;
using MsSql.ClassGenerator.Domain.Services;

namespace MsSql.ClassGenerator.ViewModels;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly MsSqlSchemaReader _schemaReader;
    private readonly AliasService _aliasService;
    private readonly PropertyDefinitionFactory _propertyFactory;
    private readonly CSharpNameService _nameService;
    private readonly CSharpClassGenerator _classGenerator;

    [ObservableProperty]
    private string schemaName = "dbo";

    [ObservableProperty]
    private string? selectedTableName;

    [ObservableProperty]
    private string className = string.Empty;

    [ObservableProperty]
    private string namespaceName = "MyApp.Entities";

    [ObservableProperty]
    private bool useEfCoreAttributes = true;

    [ObservableProperty]
    private string generatedCode = string.Empty;

    public ObservableCollection<string> TableNames { get; } = [];

    public ObservableCollection<PropertyEditorViewModel> Properties { get; } = [];

    public MainWindowViewModel(
        MsSqlSchemaReader schemaReader,
        AliasService aliasService,
        PropertyDefinitionFactory propertyFactory,
        CSharpNameService nameService,
        CSharpClassGenerator classGenerator)
    {
        _schemaReader = schemaReader;
        _aliasService = aliasService;
        _propertyFactory = propertyFactory;
        _nameService = nameService;
        _classGenerator = classGenerator;
    }

    [RelayCommand]
    private async Task LoadTablesAsync()
    {
        TableNames.Clear();

        var tables = await _schemaReader.GetTableNamesAsync(SchemaName);

        foreach (var table in tables)
        {
            TableNames.Add(table);
        }
    }

    [RelayCommand]
    private async Task LoadSelectedTableAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTableName))
            return;

        var tableDefinition = await _schemaReader.GetTableDefinitionAsync(
            SchemaName,
            SelectedTableName);

        var alias = await _aliasService.GetTableAliasAsync(
            SchemaName,
            SelectedTableName);

        ClassName = alias?.ClassName
            ?? _nameService.ToClassName(SelectedTableName);

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

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTableName))
            return;

        var properties = Properties
            .Select(x => x.ToPropertyDefinition())
            .ToList();

        await _aliasService.SaveAsync(
            SchemaName,
            SelectedTableName,
            ClassName,
            UseEfCoreAttributes,
            properties);

        var options = new ClassGenerationOptions
        {
            NamespaceName = NamespaceName,
            SchemaName = SchemaName,
            TableName = SelectedTableName,
            ClassName = ClassName,
            UseEfCoreAttributes = UseEfCoreAttributes,
            Properties = properties
        };

        GeneratedCode = _classGenerator.Generate(options);
    }
}
```


---

## 11. Property-Editor-ViewModel

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using MsSql.ClassGenerator.Domain.Models;

namespace MsSql.ClassGenerator.ViewModels;

internal sealed partial class PropertyEditorViewModel : ObservableObject
{
    public string ColumnName { get; }

    public string CSharpTypeName { get; }

    public bool IsNullable { get; }

    public bool IsPrimaryKey { get; }

    public int? MaxLength { get; }

    [ObservableProperty]
    private string propertyName;

    [ObservableProperty]
    private AccessorKind accessorKind;

    public PropertyEditorViewModel(PropertyDefinition property)
    {
        ColumnName = property.ColumnName;
        CSharpTypeName = property.CSharpTypeName;
        IsNullable = property.IsNullable;
        IsPrimaryKey = property.IsPrimaryKey;
        MaxLength = property.MaxLength;
        propertyName = property.PropertyName;
        accessorKind = property.AccessorKind;
    }

    public PropertyDefinition ToPropertyDefinition()
    {
        return new PropertyDefinition
        {
            ColumnName = ColumnName,
            PropertyName = PropertyName,
            CSharpTypeName = CSharpTypeName,
            IsNullable = IsNullable,
            IsPrimaryKey = IsPrimaryKey,
            MaxLength = MaxLength,
            AccessorKind = AccessorKind
        };
    }
}
```


---

## 12. WPF UI grob

Eine einfache Maske könnte so aufgebaut sein:

```xml
<Window x:Class="MsSql.ClassGenerator.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MS SQL Class Generator"
        Height="700"
        Width="1100">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="2*" />
        </Grid.RowDefinitions>

        <StackPanel Orientation="Horizontal"
                    Grid.Row="0"
                    Margin="0,0,0,12">
            <TextBlock Text="Schema:"
                       VerticalAlignment="Center" />

            <TextBox Width="100"
                     Margin="8,0,16,0"
                     Text="{Binding SchemaName, UpdateSourceTrigger=PropertyChanged}" />

            <Button Content="Tabellen laden"
                    Command="{Binding LoadTablesCommand}" />

            <ComboBox Width="220"
                      Margin="16,0,0,0"
                      ItemsSource="{Binding TableNames}"
                      SelectedItem="{Binding SelectedTableName}" />

            <Button Content="Tabelle laden"
                    Margin="8,0,0,0"
                    Command="{Binding LoadSelectedTableCommand}" />
        </StackPanel>

        <Grid Grid.Row="1">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <StackPanel Orientation="Horizontal"
                        Grid.Row="0"
                        Margin="0,0,0,8">
                <TextBlock Text="Namespace:"
                           VerticalAlignment="Center" />

                <TextBox Width="260"
                         Margin="8,0,16,0"
                         Text="{Binding NamespaceName, UpdateSourceTrigger=PropertyChanged}" />

                <TextBlock Text="Klasse:"
                           VerticalAlignment="Center" />

                <TextBox Width="220"
                         Margin="8,0,16,0"
                         Text="{Binding ClassName, UpdateSourceTrigger=PropertyChanged}" />

                <CheckBox Content="EF-Core Attribute"
                          IsChecked="{Binding UseEfCoreAttributes}"
                          VerticalAlignment="Center" />

                <Button Content="Generieren"
                        Margin="16,0,0,0"
                        Command="{Binding GenerateCommand}" />
            </StackPanel>

            <DataGrid Grid.Row="1"
                      ItemsSource="{Binding Properties}"
                      AutoGenerateColumns="False"
                      CanUserAddRows="False">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="SQL Spalte"
                                        Binding="{Binding ColumnName}"
                                        IsReadOnly="True" />

                    <DataGridTextColumn Header="C# Property"
                                        Binding="{Binding PropertyName, UpdateSourceTrigger=PropertyChanged}" />

                    <DataGridTextColumn Header="Typ"
                                        Binding="{Binding CSharpTypeName}"
                                        IsReadOnly="True" />

                    <DataGridCheckBoxColumn Header="Nullable"
                                            Binding="{Binding IsNullable}"
                                            IsReadOnly="True" />

                    <DataGridCheckBoxColumn Header="PK"
                                            Binding="{Binding IsPrimaryKey}"
                                            IsReadOnly="True" />

                    <DataGridComboBoxColumn Header="Accessor"
                                            SelectedItemBinding="{Binding AccessorKind}"
                                            ItemsSource="{Binding Source={StaticResource AccessorKinds}}" />
                </DataGrid.Columns>
            </DataGrid>
        </Grid>

        <TextBox Grid.Row="2"
                 Margin="0,12,0,0"
                 Text="{Binding GeneratedCode}"
                 FontFamily="Consolas"
                 FontSize="13"
                 AcceptsReturn="True"
                 AcceptsTab="True"
                 VerticalScrollBarVisibility="Auto"
                 HorizontalScrollBarVisibility="Auto" />
    </Grid>
</Window>
```


Für die ComboBox brauchst du z. B. eine Resource für die Enum-Werte oder bindest diese aus dem ViewModel.

---

## 13. Dependency Injection in WPF

In `App.xaml.cs` kannst du Services registrieren.

```csharp
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MsSql.ClassGenerator.Application.Services;
using MsSql.ClassGenerator.Domain.Services;
using MsSql.ClassGenerator.Infrastructure.Sqlite;
using MsSql.ClassGenerator.ViewModels;

namespace MsSql.ClassGenerator;

public partial class App
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        services.AddDbContext<AliasDbContext>(options =>
        {
            options.UseSqlite("Data Source=aliases.db");
        });

        services.AddTransient<IDbConnection>(_ =>
        {
            return new SqlConnection(
                "Server=.;Database=MyDatabase;Trusted_Connection=True;TrustServerCertificate=True;");
        });

        services.AddSingleton<CSharpNameService>();
        services.AddSingleton<SqlTypeMapper>();
        services.AddTransient<MsSqlSchemaReader>();
        services.AddTransient<AliasService>();
        services.AddTransient<PropertyDefinitionFactory>();
        services.AddTransient<CSharpClassGenerator>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AliasDbContext>();
            dbContext.Database.Migrate();
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
```


Und im `MainWindow`:

```csharp
using MsSql.ClassGenerator.ViewModels;

namespace MsSql.ClassGenerator;

public partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```


---

## 14. EF-Core-Migration für SQLite

Pakete:

```xml
<ItemGroup>
  <PackageReference Include="Dapper" Version="2.1.66" />
  <PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.2" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
</ItemGroup>
```


Migration erzeugen:

```shell script
dotnet ef migrations add InitialAliasDatabase
dotnet ef database update
```


---

## 15. Wichtige Erweiterungen

Später kannst du noch ergänzen:

### Safer C# Namen

Aktuell solltest du prüfen:

- beginnt Name mit Zahl?
- ist Name ein C# Keyword?
- enthält Name Sonderzeichen?
- doppelte Property-Namen nach Alias?

Beispiel:

```csharp
private static readonly HashSet<string> Keywords =
[
    "class",
    "namespace",
    "public",
    "private",
    "internal",
    "string",
    "int",
    "object",
    "event",
    "record",
    "init"
];

public string EscapeIdentifier(string value)
{
    if (Keywords.Contains(value))
        return "@" + value;

    if (char.IsDigit(value[0]))
        return "_" + value;

    return value;
}
```


### Weitere EF-Core-Attribute

Du kannst später zusätzlich generieren:

```csharp
[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
```


für Identity-Spalten.

Oder:

```csharp
[Precision(18, 2)]
```


für `decimal`.

Dafür musst du beim SQL-Auslesen noch Identity-Informationen ergänzen.

---

## 16. Besserer SQL für Identity und Reihenfolge

Wenn du Identity-Spalten erkennen willst, kannst du statt `INFORMATION_SCHEMA` direkt `sys.columns` verwenden:

```sql
SELECT
    c.name AS ColumnName,
    t.name AS SqlDataType,
    c.is_nullable AS IsNullable,
    c.max_length AS MaxLength,
    c.precision AS Precision,
    c.scale AS Scale,
    c.is_identity AS IsIdentity,
    CAST(CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsPrimaryKey
FROM sys.tables tbl
INNER JOIN sys.schemas s
    ON tbl.schema_id = s.schema_id
INNER JOIN sys.columns c
    ON tbl.object_id = c.object_id
INNER JOIN sys.types t
    ON c.user_type_id = t.user_type_id
LEFT JOIN (
    SELECT
        ic.object_id,
        ic.column_id
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic
        ON i.object_id = ic.object_id
       AND i.index_id = ic.index_id
    WHERE i.is_primary_key = 1
) pk
    ON c.object_id = pk.object_id
   AND c.column_id = pk.column_id
WHERE s.name = @SchemaName
  AND tbl.name = @TableName
ORDER BY c.column_id;
```


Dann könntest du `ColumnDefinition` erweitern:

```csharp
public required bool IsIdentity { get; init; }
```


und im Generator:

```csharp
if (property.IsIdentity)
{
    builder.AppendLine("    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
}
```


---

## Kurzfazit

Eine gute Implementierung wäre:

- **Dapper** liest nur die MS-SQL-Metadaten.
- **EF Core + SQLite** speichert Benutzerentscheidungen wie Klassenname, Propertynamen, Accessors und EF-Optionen.
- Ein separater **Generator-Service** erzeugt daraus C#-Code.
- Die WPF-Oberfläche zeigt Tabellen, Spalten und Aliasfelder an.
- Beim Generieren werden Aliasdaten automatisch gespeichert.

Damit bleibt dein Programm sauber trennbar:

```plain text
MS SQL lesen      => MsSqlSchemaReader
Alias speichern  => AliasService + SQLite EF Core
C# erzeugen      => CSharpClassGenerator
UI Logik         => MainWindowViewModel
```


Das ist wartbar, testbar und später leicht erweiterbar, z. B. für Records, Partial Classes, Fluent API oder Nullable Reference Types.