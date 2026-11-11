---
tags:
  - mssql
  - classgenerator
---
Für dein Tool würde ich die Klassen sauber nach **Verantwortlichkeiten** trennen. Auch wenn es „nur“ ein WPF-Tool ist, lohnt sich eine kleine Architektur mit klaren Schichten:

```plain text
MsSql.ClassGenerator
│
├─ Presentation
│  ├─ MainWindow.xaml
│  ├─ MainWindow.xaml.cs
│  ├─ ViewModels
│  │  ├─ MainWindowViewModel.cs
│  │  ├─ ColumnMappingViewModel.cs
│  │  └─ TableItemViewModel.cs
│  └─ Commands
│     └─ RelayCommand.cs
│
├─ Application
│  ├─ Services
│  │  ├─ ITableMetadataService.cs
│  │  ├─ IClassGenerationService.cs
│  │  └─ IFileExportService.cs
│  └─ Options
│     └─ ClassGenerationOptions.cs
│
├─ Infrastructure
│  ├─ SqlServer
│  │  ├─ SqlServerTableMetadataService.cs
│  │  └─ SqlServerTypeMapper.cs
│  └─ FileSystem
│     └─ FileExportService.cs
│
├─ Domain
│  ├─ Models
│  │  ├─ DatabaseTable.cs
│  │  ├─ DatabaseColumn.cs
│  │  ├─ GeneratedClass.cs
│  │  └─ PropertyDefinition.cs
│  ├─ Enums
│  │  └─ PropertyAccessorKind.cs
│  └─ Services
│     ├─ CSharpClassGenerator.cs
│     ├─ CSharpNameService.cs
│     └─ SqlToCSharpTypeMapper.cs
│
└─ Shared
   └─ ObservableObject.cs
```


Du musst nicht zwingend exakt diese Ordner verwenden, aber die **Trennung der Verantwortlichkeiten** ist wichtig.

---

# Zielbild der Architektur

## 1. Presentation

Hier liegt alles, was direkt mit WPF zu tun hat:

- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- ViewModels
- Commands
- UI-spezifische Hilfsklassen

Die Presentation-Schicht sollte **keine SQL-Abfragen** enthalten und auch nicht direkt C#-Code zusammenbauen.

Sie ruft nur Services auf.

---

## 2. Application

Diese Schicht koordiniert Anwendungsfälle:

- Tabellen laden
- Spalten laden
- Klasse generieren
- Datei speichern

Hier liegen Interfaces, die von der UI verwendet werden.

Beispiele:

```csharp
public interface ITableMetadataService
{
    Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatabaseColumn>> GetColumnsAsync(
        string connectionString,
        DatabaseTable table,
        CancellationToken cancellationToken = default);
}
```


```csharp
public interface IClassGenerationService
{
    string GenerateClass(ClassGenerationOptions options);
}
```


```csharp
public interface IFileExportService
{
    Task SaveAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default);
}
```


Die UI kennt dann nur diese Interfaces, nicht die konkrete SQL-Server-Implementierung.

---

## 3. Infrastructure

Hier liegen technische Details:

- SQL Server Zugriff
- `Microsoft.Data.SqlClient`
- Dateisystem
- eventuell App-Konfiguration

Beispiel:

```csharp
public sealed class SqlServerTableMetadataService : ITableMetadataService
{
    public Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        // INFORMATION_SCHEMA.TABLES lesen
    }

    public Task<IReadOnlyList<DatabaseColumn>> GetColumnsAsync(
        string connectionString,
        DatabaseTable table,
        CancellationToken cancellationToken = default)
    {
        // INFORMATION_SCHEMA.COLUMNS lesen
    }
}
```


Diese Klasse darf SQL kennen.

Die ViewModels sollten diese Klasse aber nicht direkt erzeugen, sondern nur über `ITableMetadataService` arbeiten.

---

## 4. Domain

Hier liegt deine eigentliche Fachlogik:

- Tabellenmodell
- Spaltenmodell
- Codegenerator
- Namenskonvertierung
- Typmapping
- Generierungsregeln

Diese Schicht sollte möglichst unabhängig sein von:

- WPF
- SQL Client
- Dateisystem
- konkreten UI-Details

---

# Empfohlene Kernklassen

## `DatabaseTable`

```csharp
public sealed class DatabaseTable
{
    public required string Schema { get; init; }

    public required string Name { get; init; }

    public string FullName => $"{Schema}.{Name}";
}
```


---

## `DatabaseColumn`

```csharp
public sealed class DatabaseColumn
{
    public required string Name { get; init; }

    public required string SqlType { get; init; }

    public bool IsNullable { get; init; }

    public bool IsPrimaryKey { get; init; }

    public int? MaxLength { get; init; }

    public int OrdinalPosition { get; init; }
}
```


Diese Klasse beschreibt die Datenbankspalte so, wie sie aus SQL Server kommt.

Wichtig: Hier würde ich noch **keinen alternativen Property-Namen** speichern. Das ist eher eine Benutzerentscheidung.

---

## `PropertyDefinition`

```csharp
public sealed class PropertyDefinition
{
    public required string ColumnName { get; init; }

    public required string PropertyName { get; init; }

    public required string CSharpType { get; init; }

    public bool IsNullable { get; init; }

    public bool IsPrimaryKey { get; init; }

    public int? MaxLength { get; init; }

    public bool Include { get; init; } = true;

    public PropertyAccessorKind AccessorKind { get; init; } = PropertyAccessorKind.GetSet;
}
```


Diese Klasse beschreibt dann die spätere C#-Property.

---

## `PropertyAccessorKind`

```csharp
public enum PropertyAccessorKind
{
    GetSet,
    GetInit
}
```


---

## `ClassGenerationOptions`

```csharp
public sealed class ClassGenerationOptions
{
    public required string NamespaceName { get; init; }

    public required string ClassName { get; init; }

    public required string TableName { get; init; }

    public required string SchemaName { get; init; }

    public bool UseEfCoreAttributes { get; init; }

    public required IReadOnlyList<PropertyDefinition> Properties { get; init; }
}
```


Diese Klasse enthält alle Entscheidungen, die für die Codegenerierung nötig sind.

---

# Warum `DatabaseColumn` und `PropertyDefinition` trennen?

Das ist wichtig.

Eine Datenbankspalte ist nicht dasselbe wie eine generierte C#-Property.

Beispiel:

SQL:

```plain text
customer_id int not null
```


Daraus kann der Benutzer machen:

```csharp
[Column("customer_id")]
public int Id { get; init; }
```


Also:

| Datenbank | C# |
|---|---|
| `customer_id` | `Id` |
| `int` | `int` |
| nullable laut DB | nullable laut C# |
| Spaltenname fix | Property-Name editierbar |
| keine Accessor-Info | `get; set;` oder `get; init;` |

Deshalb ist diese Trennung sauberer.

---

# ViewModel-Struktur

## `MainWindowViewModel`

Dieses ViewModel koordiniert die UI:

```csharp
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ITableMetadataService _tableMetadataService;
    private readonly IClassGenerationService _classGenerationService;

    public MainWindowViewModel(
        ITableMetadataService tableMetadataService,
        IClassGenerationService classGenerationService)
    {
        _tableMetadataService = tableMetadataService;
        _classGenerationService = classGenerationService;
    }

    public string ConnectionString { get; set; } = string.Empty;

    public string NamespaceName { get; set; } = "GeneratedModels";

    public string ClassName { get; set; } = string.Empty;

    public bool UseEfCoreAttributes { get; set; }

    public ObservableCollection<TableItemViewModel> Tables { get; } = [];

    public ObservableCollection<ColumnMappingViewModel> Columns { get; } = [];

    public string GeneratedCode { get; set; } = string.Empty;

    public ICommand LoadTablesCommand { get; }

    public ICommand GenerateCodeCommand { get; }
}
```


---

## `TableItemViewModel`

```csharp
public sealed class TableItemViewModel
{
    public TableItemViewModel(DatabaseTable table)
    {
        Table = table;
    }

    public DatabaseTable Table { get; }

    public string Schema => Table.Schema;

    public string Name => Table.Name;

    public string FullName => Table.FullName;
}
```


---

## `ColumnMappingViewModel`

Das ist die UI-Version einer Property.

```csharp
public sealed class ColumnMappingViewModel : ObservableObject
{
    private string _propertyName;
    private string _cSharpType;
    private bool _include = true;
    private PropertyAccessorKind _accessorKind = PropertyAccessorKind.GetSet;

    public ColumnMappingViewModel(DatabaseColumn column, string cSharpType, string propertyName)
    {
        Column = column;
        _cSharpType = cSharpType;
        _propertyName = propertyName;
    }

    public DatabaseColumn Column { get; }

    public string ColumnName => Column.Name;

    public string SqlType => Column.SqlType;

    public bool IsNullable => Column.IsNullable;

    public bool IsPrimaryKey => Column.IsPrimaryKey;

    public int? MaxLength => Column.MaxLength;

    public string PropertyName
    {
        get => _propertyName;
        set => SetProperty(ref _propertyName, value);
    }

    public string CSharpType
    {
        get => _cSharpType;
        set => SetProperty(ref _cSharpType, value);
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

    public PropertyDefinition ToPropertyDefinition()
    {
        return new PropertyDefinition
        {
            ColumnName = Column.Name,
            PropertyName = PropertyName,
            CSharpType = CSharpType,
            IsNullable = Column.IsNullable,
            IsPrimaryKey = Column.IsPrimaryKey,
            MaxLength = Column.MaxLength,
            Include = Include,
            AccessorKind = AccessorKind
        };
    }
}
```


---

# Services

## `ITableMetadataService`

```csharp
public interface ITableMetadataService
{
    Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatabaseColumn>> GetColumnsAsync(
        string connectionString,
        DatabaseTable table,
        CancellationToken cancellationToken = default);
}
```


---

## `IClassGenerationService`

```csharp
public interface IClassGenerationService
{
    string GenerateClass(ClassGenerationOptions options);
}
```


---

## `ClassGenerationService`

```csharp
public sealed class ClassGenerationService : IClassGenerationService
{
    private readonly CSharpClassGenerator _generator;

    public ClassGenerationService(CSharpClassGenerator generator)
    {
        _generator = generator;
    }

    public string GenerateClass(ClassGenerationOptions options)
    {
        return _generator.Generate(options);
    }
}
```


---

# Generator-Klassen

Ich würde den Generator weiter aufteilen:

```plain text
Domain/Services
├─ CSharpClassGenerator.cs
├─ CSharpPropertyGenerator.cs
├─ CSharpNameService.cs
└─ SqlToCSharpTypeMapper.cs
```


## `CSharpClassGenerator`

Verantwortlich für die komplette Klasse.

```csharp
public sealed class CSharpClassGenerator
{
    private readonly CSharpPropertyGenerator _propertyGenerator;

    public CSharpClassGenerator(CSharpPropertyGenerator propertyGenerator)
    {
        _propertyGenerator = propertyGenerator;
    }

    public string Generate(ClassGenerationOptions options)
    {
        var builder = new StringBuilder();

        AppendUsings(builder, options);
        AppendNamespace(builder, options);
        AppendClassDeclaration(builder, options);
        AppendProperties(builder, options);
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendUsings(StringBuilder builder, ClassGenerationOptions options)
    {
        builder.AppendLine("using System;");

        if (options.UseEfCoreAttributes)
        {
            builder.AppendLine("using System.ComponentModel.DataAnnotations;");
            builder.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
        }

        builder.AppendLine();
    }

    private static void AppendNamespace(StringBuilder builder, ClassGenerationOptions options)
    {
        builder.AppendLine($"namespace {options.NamespaceName};");
        builder.AppendLine();
    }

    private static void AppendClassDeclaration(StringBuilder builder, ClassGenerationOptions options)
    {
        if (options.UseEfCoreAttributes)
        {
            builder.AppendLine($"[Table(\"{options.TableName}\", Schema = \"{options.SchemaName}\")]");
        }

        builder.AppendLine($"public sealed class {options.ClassName}");
        builder.AppendLine("{");
    }

    private void AppendProperties(StringBuilder builder, ClassGenerationOptions options)
    {
        foreach (var property in options.Properties.Where(x => x.Include))
        {
            _propertyGenerator.AppendProperty(builder, property, options.UseEfCoreAttributes);
        }
    }
}
```


---

## `CSharpPropertyGenerator`

```csharp
public sealed class CSharpPropertyGenerator
{
    public void AppendProperty(
        StringBuilder builder,
        PropertyDefinition property,
        bool useEfCoreAttributes)
    {
        builder.AppendLine();

        if (useEfCoreAttributes)
        {
            AppendEfCoreAttributes(builder, property);
        }

        var accessor = property.AccessorKind switch
        {
            PropertyAccessorKind.GetInit => "get; init;",
            _ => "get; set;"
        };

        var defaultValue = GetDefaultValue(property.CSharpType);

        builder.AppendLine(
            $"    public {property.CSharpType} {property.PropertyName} {{ {accessor} }}{defaultValue}");
    }

    private static void AppendEfCoreAttributes(StringBuilder builder, PropertyDefinition property)
    {
        if (property.IsPrimaryKey)
        {
            builder.AppendLine("    [Key]");
        }

        builder.AppendLine($"    [Column(\"{property.ColumnName}\")]");

        if (property.MaxLength is > 0 && IsStringType(property.CSharpType))
        {
            builder.AppendLine($"    [MaxLength({property.MaxLength})]");
        }

        if (!property.IsNullable && IsStringType(property.CSharpType))
        {
            builder.AppendLine("    [Required]");
        }
    }

    private static bool IsStringType(string cSharpType)
    {
        return cSharpType is "string" or "string?";
    }

    private static string GetDefaultValue(string cSharpType)
    {
        return cSharpType switch
        {
            "string" => " = string.Empty;",
            "byte[]" => " = [];",
            _ => string.Empty
        };
    }
}
```


---

## `SqlToCSharpTypeMapper`

```csharp
public sealed class SqlToCSharpTypeMapper
{
    public string Map(string sqlType, bool isNullable)
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
}
```


---

## `CSharpNameService`

```csharp
public sealed class CSharpNameService
{
    public string ToClassName(string tableName)
    {
        var name = ToPascalCase(tableName);

        if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^1];
        }

        return name;
    }

    public string ToPropertyName(string columnName)
    {
        return ToPascalCase(columnName);
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = value.Split(
            ['_', ' ', '-'],
            StringSplitOptions.RemoveEmptyEntries);

        return string.Concat(parts.Select(part =>
            char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
```


---

# SQL-Service

```csharp
public sealed class SqlServerTableMetadataService : ITableMetadataService
{
    public async Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                TABLE_SCHEMA,
                TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME;
            """;

        var tables = new List<DatabaseTable>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new DatabaseTable
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1)
            });
        }

        return tables;
    }

    public async Task<IReadOnlyList<DatabaseColumn>> GetColumnsAsync(
        string connectionString,
        DatabaseTable table,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.ORDINAL_POSITION,
                CASE 
                    WHEN tc.CONSTRAINT_TYPE = 'PRIMARY KEY' THEN 1 
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

        var columns = new List<DatabaseColumn>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Schema", table.Schema);
        command.Parameters.AddWithValue("@TableName", table.Name);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new DatabaseColumn
            {
                Name = reader.GetString(0),
                SqlType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                OrdinalPosition = reader.GetInt32(4),
                IsPrimaryKey = reader.GetInt32(5) == 1
            });
        }

        return columns;
    }
}
```


---

# Abhängigkeiten

Die Abhängigkeiten sollten ungefähr so laufen:

```plain text
Presentation
    ↓
Application
    ↓
Domain

Infrastructure
    ↓
Application + Domain
```


Oder praktisch:

```plain text
MainWindowViewModel
    nutzt ITableMetadataService
    nutzt IClassGenerationService

SqlServerTableMetadataService
    implementiert ITableMetadataService

ClassGenerationService
    nutzt CSharpClassGenerator

CSharpClassGenerator
    nutzt PropertyDefinition, ClassGenerationOptions
```


Wichtig:

- UI kennt keine SQL-Queries
- Generator kennt keine WPF-Klassen
- SQL-Service kennt keine ViewModels
- Domain kennt kein `SqlConnection`
- ViewModels enthalten keine StringBuilder-Codegenerierung

---

# Dependency Injection

In WPF kannst du einfach `Microsoft.Extensions.DependencyInjection` verwenden.

In `App.xaml.cs`:

```csharp
public partial class App : Application
{
    private ServiceProvider _serviceProvider = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ITableMetadataService, SqlServerTableMetadataService>();
        services.AddSingleton<IClassGenerationService, ClassGenerationService>();

        services.AddSingleton<CSharpClassGenerator>();
        services.AddSingleton<CSharpPropertyGenerator>();
        services.AddSingleton<SqlToCSharpTypeMapper>();
        services.AddSingleton<CSharpNameService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
```


Und in `MainWindow.xaml.cs`:

```csharp
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```


---

# Konkreter Vorschlag für eine erste Version

Für den Anfang würde ich nicht zu viele Klassen bauen. Eine gute Startstruktur wäre:

```plain text
Domain
├─ DatabaseTable.cs
├─ DatabaseColumn.cs
├─ PropertyDefinition.cs
├─ PropertyAccessorKind.cs
├─ ClassGenerationOptions.cs
├─ CSharpClassGenerator.cs
├─ CSharpPropertyGenerator.cs
├─ SqlToCSharpTypeMapper.cs
└─ CSharpNameService.cs

Application
├─ ITableMetadataService.cs
└─ IClassGenerationService.cs

Infrastructure
└─ SqlServerTableMetadataService.cs

Presentation
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
├─ MainWindowViewModel.cs
├─ ColumnMappingViewModel.cs
├─ TableItemViewModel.cs
├─ ObservableObject.cs
└─ RelayCommand.cs
```


Das ist für dein Projekt übersichtlich und trotzdem erweiterbar.

---

# Spätere Erweiterungen

Wenn dein Tool später größer wird, kannst du einfach weitere Services ergänzen:

```plain text
Application
├─ IClassGenerationService.cs
├─ ITableMetadataService.cs
├─ IProjectSettingsService.cs
├─ ITemplateService.cs
└─ IFileExportService.cs
```


```plain text
Infrastructure
├─ SqlServer
│  ├─ SqlServerTableMetadataService.cs
│  └─ SqlServerConnectionTester.cs
├─ FileSystem
│  └─ FileExportService.cs
└─ Settings
   └─ JsonProjectSettingsService.cs
```


---

# Kurzempfehlung

Ich würde es so machen:

- **Domain** enthält Modelle und Generatorlogik
- **Application** enthält Interfaces und Use-Case-nahe Services
- **Infrastructure** enthält SQL Server und Dateisystem
- **Presentation** enthält WPF, ViewModels und Commands

Die wichtigste Regel:

> Eine Klasse sollte genau eine Verantwortung haben.

Also:

- `SqlServerTableMetadataService` lädt Datenbank-Metadaten
- `SqlToCSharpTypeMapper` mappt SQL-Typen auf C#-Typen
- `CSharpNameService` erzeugt Klassennamen und Property-Namen
- `CSharpClassGenerator` erzeugt den Code
- `MainWindowViewModel` koordiniert die UI
- `MainWindow.xaml` zeigt nur die Oberfläche an