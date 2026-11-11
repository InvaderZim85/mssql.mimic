---
tags:
  - mssql
  - classgenerator
---
Ja. Dafür würde ich zusätzlich zu den Tabellen-/Spalten-Aliasdaten eine kleine **Connection-History** in deiner SQLite-Datenbank speichern.

Ziel:

- Der Benutzer gibt eine Verbindung ein.
- Nach erfolgreichem Verbinden wird sie in SQLite gespeichert.
- Es werden maximal die letzten **10** Verbindungen behalten.
- Beim Start der Anwendung werden diese 10 Verbindungen geladen und z. B. in einer ComboBox angezeigt.
- Wenn der Benutzer eine gespeicherte Verbindung auswählt, werden Server, Datenbank, Authentifizierung usw. wieder ins UI übernommen.

Wichtig: **Passwörter solltest du nicht im Klartext speichern.**  
Für ein lokales Windows-WPF-Tool bietet sich z. B. `ProtectedData` / DPAPI an. Alternativ speicherst du gar kein Passwort und lässt es bei SQL-Login erneut eingeben.

---

# 1. Neues Domain-Modell für Verbindung

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

    public string DisplayName
    {
        get
        {
            var auth = UseIntegratedSecurity
                ? "Windows"
                : UserName;

            return $"{ServerName} / {DatabaseName} ({auth})";
        }
    }
}
```


Das `Password` ist bewusst im Domain-Modell enthalten, weil es zur Laufzeit gebraucht wird. Ob du es dauerhaft speicherst, entscheidest du in der SQLite-Entity.

---

# 2. Neue SQLite-Entity für gespeicherte Verbindungen

```csharp
namespace MsSql.ClassGenerator.Infrastructure.Sqlite.Entities;

internal sealed class ConnectionHistoryEntity
{
    public int Id { get; set; }

    public required string ServerName { get; set; }

    public required string DatabaseName { get; set; }

    public bool UseIntegratedSecurity { get; set; }

    public string? UserName { get; set; }

    public string? ProtectedPassword { get; set; }

    public bool TrustServerCertificate { get; set; }

    public bool Encrypt { get; set; }

    public DateTime LastUsedAtUtc { get; set; }
}
```


Die Spalte `ProtectedPassword` enthält optional das verschlüsselte Passwort.

Wenn du Passwörter gar nicht speichern willst, lässt du diese Property einfach weg.

---

# 3. `AliasDbContext` erweitern

Dein vorhandener SQLite-Context sollte ein neues `DbSet` bekommen.

```csharp
using Microsoft.EntityFrameworkCore;
using MsSql.ClassGenerator.Infrastructure.Sqlite.Entities;

namespace MsSql.ClassGenerator.Infrastructure.Sqlite;

internal sealed class AliasDbContext : DbContext
{
    public DbSet<TableAliasEntity> TableAliases => Set<TableAliasEntity>();

    public DbSet<ColumnAliasEntity> ColumnAliases => Set<ColumnAliasEntity>();

    public DbSet<ConnectionHistoryEntity> ConnectionHistory => Set<ConnectionHistoryEntity>();

    public AliasDbContext(DbContextOptions<AliasDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConnectionHistoryEntity>(entity =>
        {
            entity.ToTable("ConnectionHistory");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ServerName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.DatabaseName)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(x => x.UserName)
                .HasMaxLength(256);

            entity.Property(x => x.ProtectedPassword)
                .HasMaxLength(4096);

            entity.Property(x => x.LastUsedAtUtc)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.ServerName,
                x.DatabaseName,
                x.UseIntegratedSecurity,
                x.UserName
            }).IsUnique();
        });

        // Deine vorhandenen TableAliasEntity- und ColumnAliasEntity-Konfigurationen bleiben hier ebenfalls.
    }
}
```


Wichtig ist der eindeutige Index. Dadurch wird dieselbe Verbindung nicht mehrfach gespeichert, sondern nur aktualisiert.

---

# 4. Passwortschutz per DPAPI

Für ein Windows-WPF-Tool kannst du unter Windows `ProtectedData` verwenden.

Dafür brauchst du ggf. das NuGet-Paket:

```xml
<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.0" />
```


Dann ein kleiner Service:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace MsSql.ClassGenerator.Application.Services;

internal sealed class PasswordProtector
{
    public string? Protect(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return null;

        var bytes = Encoding.UTF8.GetBytes(password);

        var protectedBytes = ProtectedData.Protect(
            bytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(protectedBytes);
    }

    public string? Unprotect(string? protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
            return null;

        var protectedBytes = Convert.FromBase64String(protectedPassword);

        var bytes = ProtectedData.Unprotect(
            protectedBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(bytes);
    }
}
```


`DataProtectionScope.CurrentUser` bedeutet:

- Nur derselbe Windows-Benutzer kann das Passwort wieder entschlüsseln.
- Auf einem anderen Benutzerkonto oder Rechner funktioniert das nicht.
- Für ein lokales Desktop-Tool ist das meistens genau richtig.

---

# 5. Service für die letzten 10 Verbindungen

```csharp
using Microsoft.EntityFrameworkCore;
using MsSql.ClassGenerator.Domain.Models;
using MsSql.ClassGenerator.Infrastructure.Sqlite;
using MsSql.ClassGenerator.Infrastructure.Sqlite.Entities;

namespace MsSql.ClassGenerator.Application.Services;

internal sealed class ConnectionHistoryService
{
    private const int MaxHistoryCount = 10;

    private readonly AliasDbContext _context;
    private readonly PasswordProtector _passwordProtector;

    public ConnectionHistoryService(
        AliasDbContext context,
        PasswordProtector passwordProtector)
    {
        _context = context;
        _passwordProtector = passwordProtector;
    }

    public async Task<IReadOnlyList<DatabaseConnectionProfile>> LoadLastConnectionsAsync(
        CancellationToken ct = default)
    {
        var entities = await _context.ConnectionHistory
            .AsNoTracking()
            .OrderByDescending(x => x.LastUsedAtUtc)
            .Take(MaxHistoryCount)
            .ToListAsync(ct);

        return entities
            .Select(ToProfile)
            .ToList();
    }

    public async Task SaveConnectionAsync(
        DatabaseConnectionProfile profile,
        CancellationToken ct = default)
    {
        var normalizedServerName = Normalize(profile.ServerName);
        var normalizedDatabaseName = Normalize(profile.DatabaseName);
        var normalizedUserName = NormalizeNullable(profile.UserName);

        var entity = await _context.ConnectionHistory
            .SingleOrDefaultAsync(
                x => x.ServerName == normalizedServerName &&
                     x.DatabaseName == normalizedDatabaseName &&
                     x.UseIntegratedSecurity == profile.UseIntegratedSecurity &&
                     x.UserName == normalizedUserName,
                ct);

        if (entity is null)
        {
            entity = new ConnectionHistoryEntity
            {
                ServerName = normalizedServerName,
                DatabaseName = normalizedDatabaseName,
                UseIntegratedSecurity = profile.UseIntegratedSecurity,
                UserName = normalizedUserName,
                ProtectedPassword = profile.UseIntegratedSecurity
                    ? null
                    : _passwordProtector.Protect(profile.Password),
                TrustServerCertificate = profile.TrustServerCertificate,
                Encrypt = profile.Encrypt,
                LastUsedAtUtc = DateTime.UtcNow
            };

            _context.ConnectionHistory.Add(entity);
        }
        else
        {
            entity.TrustServerCertificate = profile.TrustServerCertificate;
            entity.Encrypt = profile.Encrypt;
            entity.LastUsedAtUtc = DateTime.UtcNow;

            if (!profile.UseIntegratedSecurity)
            {
                entity.ProtectedPassword = _passwordProtector.Protect(profile.Password);
            }
            else
            {
                entity.ProtectedPassword = null;
            }
        }

        await _context.SaveChangesAsync(ct);

        await TrimHistoryAsync(ct);
    }

    public async Task DeleteConnectionAsync(
        DatabaseConnectionProfile profile,
        CancellationToken ct = default)
    {
        var normalizedServerName = Normalize(profile.ServerName);
        var normalizedDatabaseName = Normalize(profile.DatabaseName);
        var normalizedUserName = NormalizeNullable(profile.UserName);

        var entity = await _context.ConnectionHistory
            .SingleOrDefaultAsync(
                x => x.ServerName == normalizedServerName &&
                     x.DatabaseName == normalizedDatabaseName &&
                     x.UseIntegratedSecurity == profile.UseIntegratedSecurity &&
                     x.UserName == normalizedUserName,
                ct);

        if (entity is null)
            return;

        _context.ConnectionHistory.Remove(entity);

        await _context.SaveChangesAsync(ct);
    }

    private async Task TrimHistoryAsync(CancellationToken ct)
    {
        var oldEntries = await _context.ConnectionHistory
            .OrderByDescending(x => x.LastUsedAtUtc)
            .Skip(MaxHistoryCount)
            .ToListAsync(ct);

        if (oldEntries.Count == 0)
            return;

        _context.ConnectionHistory.RemoveRange(oldEntries);

        await _context.SaveChangesAsync(ct);
    }

    private DatabaseConnectionProfile ToProfile(ConnectionHistoryEntity entity)
    {
        return new DatabaseConnectionProfile
        {
            ServerName = entity.ServerName,
            DatabaseName = entity.DatabaseName,
            UseIntegratedSecurity = entity.UseIntegratedSecurity,
            UserName = entity.UserName,
            Password = entity.UseIntegratedSecurity
                ? null
                : _passwordProtector.Unprotect(entity.ProtectedPassword),
            TrustServerCertificate = entity.TrustServerCertificate,
            Encrypt = entity.Encrypt
        };
    }

    private static string Normalize(string value)
    {
        return value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
```


Damit ist die Logik klar gekapselt:

- `LoadLastConnectionsAsync()` lädt die letzten 10.
- `SaveConnectionAsync()` erstellt oder aktualisiert eine Verbindung.
- Danach werden automatisch alte Einträge gelöscht.
- `DeleteConnectionAsync()` erlaubt später das Entfernen einzelner Einträge.

---

# 6. Wann wird die Verbindung gespeichert?

Am besten speicherst du die Verbindung **nicht direkt beim Eingeben**, sondern erst nachdem klar ist, dass sie funktioniert.

Zum Beispiel nach:

- „Datenbanken laden“
- „Tabellen laden“
- erfolgreichem Verbindungstest

Beispiel:

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

    await _connectionHistoryService.SaveConnectionAsync(profile);

    await ReloadConnectionHistoryAsync();
}
```


Oder beim Datenbanken-Laden:

```csharp
[RelayCommand]
private async Task LoadDatabasesAsync()
{
    var profile = CreateConnectionProfile();

    var databaseNames = await _schemaReader.LoadDatabaseNamesAsync(profile);

    DatabaseNames.Clear();

    foreach (var databaseName in databaseNames)
    {
        DatabaseNames.Add(databaseName);
    }

    await _connectionHistoryService.SaveConnectionAsync(profile);

    await ReloadConnectionHistoryAsync();
}
```


Ich würde die Verbindung erst speichern, wenn mindestens ein erfolgreicher SQL-Zugriff stattgefunden hat.

---

# 7. ViewModel erweitern

Dein ViewModel braucht eine Collection für die gespeicherten Verbindungen.

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsSql.ClassGenerator.Application.Services;
using MsSql.ClassGenerator.Domain.Models;

namespace MsSql.ClassGenerator.ViewModels;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ConnectionHistoryService _connectionHistoryService;

    public ObservableCollection<DatabaseConnectionProfile> RecentConnections { get; } = [];

    [ObservableProperty]
    private DatabaseConnectionProfile? selectedRecentConnection;

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

    public MainWindowViewModel(
        ConnectionHistoryService connectionHistoryService)
    {
        _connectionHistoryService = connectionHistoryService;
    }

    public async Task InitializeAsync()
    {
        await ReloadConnectionHistoryAsync();
    }

    partial void OnSelectedRecentConnectionChanged(DatabaseConnectionProfile? value)
    {
        if (value is null)
            return;

        ServerName = value.ServerName;
        DatabaseName = value.DatabaseName;
        UseIntegratedSecurity = value.UseIntegratedSecurity;
        UserName = value.UserName ?? string.Empty;
        Password = value.Password ?? string.Empty;
        TrustServerCertificate = value.TrustServerCertificate;
        Encrypt = value.Encrypt;
    }

    private DatabaseConnectionProfile CreateConnectionProfile()
    {
        return new DatabaseConnectionProfile
        {
            ServerName = ServerName,
            DatabaseName = DatabaseName,
            UseIntegratedSecurity = UseIntegratedSecurity,
            UserName = string.IsNullOrWhiteSpace(UserName)
                ? null
                : UserName,
            Password = string.IsNullOrWhiteSpace(Password)
                ? null
                : Password,
            TrustServerCertificate = TrustServerCertificate,
            Encrypt = Encrypt
        };
    }

    private async Task ReloadConnectionHistoryAsync()
    {
        var connections = await _connectionHistoryService.LoadLastConnectionsAsync();

        RecentConnections.Clear();

        foreach (var connection in connections)
        {
            RecentConnections.Add(connection);
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedRecentConnectionAsync()
    {
        if (SelectedRecentConnection is null)
            return;

        await _connectionHistoryService.DeleteConnectionAsync(SelectedRecentConnection);

        SelectedRecentConnection = null;

        await ReloadConnectionHistoryAsync();
    }
}
```


Falls dein `MainWindowViewModel` bereits andere Services im Konstruktor bekommt, fügst du `ConnectionHistoryService` einfach zusätzlich hinzu.

---

# 8. `InitializeAsync` im Window aufrufen

Da Konstruktoren nicht `async` sein sollten, kannst du im Code-behind beim Laden initialisieren.

```csharp
using MsSql.ClassGenerator.ViewModels;

namespace MsSql.ClassGenerator;

public partial class MainWindow
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
}
```


---

# 9. WPF UI: ComboBox für letzte Verbindungen

Du kannst oben in deiner Maske eine ComboBox ergänzen.

```xml
<StackPanel Orientation="Horizontal"
            Margin="0,0,0,8">
    <TextBlock Text="Letzte Verbindungen:"
               VerticalAlignment="Center" />

    <ComboBox Width="320"
              Margin="8,0,0,0"
              ItemsSource="{Binding RecentConnections}"
              SelectedItem="{Binding SelectedRecentConnection}"
              DisplayMemberPath="DisplayName" />

    <Button Content="Entfernen"
            Margin="8,0,0,0"
            Command="{Binding DeleteSelectedRecentConnectionCommand}" />
</StackPanel>
```


Danach bleiben deine normalen Eingabefelder für Server, Datenbank usw.

```xml
<StackPanel Orientation="Horizontal"
            Margin="0,0,0,8">
    <TextBlock Text="Server:"
               VerticalAlignment="Center" />

    <TextBox Width="180"
             Margin="8,0,0,0"
             Text="{Binding ServerName, UpdateSourceTrigger=PropertyChanged}" />

    <TextBlock Text="Datenbank:"
               Margin="16,0,0,0"
               VerticalAlignment="Center" />

    <TextBox Width="180"
             Margin="8,0,0,0"
             Text="{Binding DatabaseName, UpdateSourceTrigger=PropertyChanged}" />

    <CheckBox Content="Windows Auth"
              Margin="16,0,0,0"
              VerticalAlignment="Center"
              IsChecked="{Binding UseIntegratedSecurity}" />

    <TextBlock Text="User:"
               Margin="16,0,0,0"
               VerticalAlignment="Center" />

    <TextBox Width="140"
             Margin="8,0,0,0"
             Text="{Binding UserName, UpdateSourceTrigger=PropertyChanged}" />
</StackPanel>
```


Für das Passwort solltest du entweder eine Attached Property verwenden oder im Code-behind setzen. Für eine einfache interne Anwendung reicht oft Code-behind.

---

# 10. Dependency Injection erweitern

Registriere die neuen Services:

```csharp
services.AddSingleton<PasswordProtector>();
services.AddTransient<ConnectionHistoryService>();
```


Beispiel:

```csharp
services.AddDbContext<AliasDbContext>(options =>
{
    options.UseSqlite("Data Source=aliases.db");
});

services.AddSingleton<PasswordProtector>();
services.AddTransient<ConnectionHistoryService>();

services.AddSingleton<SqlConnectionFactory>();
services.AddTransient<MsSqlSchemaReader>();
services.AddTransient<AliasService>();
services.AddTransient<MainWindowViewModel>();
services.AddTransient<MainWindow>();
```


---

# 11. Migration erstellen

Wenn du EF-Core-Migrations verwendest:

```shell script
dotnet ef migrations add AddConnectionHistory
dotnet ef database update
```


Die Migration würde ungefähr Folgendes erzeugen:

```csharp
migrationBuilder.CreateTable(
    name: "ConnectionHistory",
    columns: table => new
    {
        Id = table.Column<int>(type: "INTEGER", nullable: false)
            .Annotation("Sqlite:Autoincrement", true),
        ServerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
        DatabaseName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
        UseIntegratedSecurity = table.Column<bool>(type: "INTEGER", nullable: false),
        UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
        ProtectedPassword = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
        TrustServerCertificate = table.Column<bool>(type: "INTEGER", nullable: false),
        Encrypt = table.Column<bool>(type: "INTEGER", nullable: false),
        LastUsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_ConnectionHistory", x => x.Id);
    });

migrationBuilder.CreateIndex(
    name: "IX_ConnectionHistory_ServerName_DatabaseName_UseIntegratedSecurity_UserName",
    table: "ConnectionHistory",
    columns: new[]
    {
        "ServerName",
        "DatabaseName",
        "UseIntegratedSecurity",
        "UserName"
    },
    unique: true);
```


---

# 12. Anpassung im Ablauf

Der neue Ablauf wäre:

```plain text
1. Anwendung startet
2. Letzte 10 Verbindungen werden aus SQLite geladen
3. Benutzer wählt eine Verbindung aus oder gibt neue Daten ein
4. Benutzer klickt auf "Tabellen laden" oder "Datenbanken laden"
5. Verbindung wird getestet bzw. verwendet
6. Bei Erfolg:
   - Verbindung wird gespeichert oder aktualisiert
   - LastUsedAtUtc wird aktualisiert
   - Liste wird auf 10 Einträge reduziert
7. Benutzer arbeitet mit Tabellen, Aliasnamen und Generator weiter
```


---

# 13. Zusammenspiel mit `AliasService`

Dein `AliasService` sollte weiterhin mit dem vollständigen Verbindungsprofil arbeiten:

```csharp
await _aliasService.LoadTableAliasAsync(
    profile,
    selectedTable,
    ct);
```


und:

```csharp
await _aliasService.SaveAsync(
    profile,
    selectedTable,
    className,
    useEfCoreAttributes,
    properties,
    ct);
```


Dadurch werden die Aliasdaten passend zur ausgewählten Verbindung gespeichert.

Die Verbindungshistorie ist davon getrennt:

```csharp
await _connectionHistoryService.SaveConnectionAsync(profile, ct);
```


Du hast dann zwei getrennte Konzepte:

```plain text
ConnectionHistory
    Speichert die letzten 10 Verbindungen.

TableAliases / ColumnAliases
    Speichert Aliasdaten pro Server + Datenbank + Schema + Tabelle.
```


Das ist sauberer als alles in eine Tabelle zu packen.

---

# 14. Empfehlung zum Passwort

Ich würde eine Option anbieten:

```plain text
[x] Passwort speichern
```


Dann ergänzt du dein Profil:

```csharp
public bool SavePassword { get; init; }
```


und speicherst das Passwort nur, wenn der Benutzer es ausdrücklich möchte.

Entity:

```csharp
public bool SavePassword { get; set; }
```


Service:

```csharp
ProtectedPassword = profile.SavePassword
    ? _passwordProtector.Protect(profile.Password)
    : null;
```


Für viele interne Tools reicht aber auch:

- Server
- Datenbank
- Auth-Typ
- UserName

zu speichern und das Passwort immer neu einzugeben.

---

## Kurzfassung

Du ergänzt:

```plain text
ConnectionHistoryEntity
ConnectionHistoryService
PasswordProtector
RecentConnections im ViewModel
ComboBox im UI
```


Die SQLite-Tabelle speichert maximal die letzten 10 erfolgreichen Verbindungen:

```plain text
ServerName
DatabaseName
UseIntegratedSecurity
UserName
ProtectedPassword
TrustServerCertificate
Encrypt
LastUsedAtUtc
```


Beim erfolgreichen Laden von Datenbanken oder Tabellen rufst du auf:

```csharp
await _connectionHistoryService.SaveConnectionAsync(profile);
```


und beim Start:

```csharp
RecentConnections = await _connectionHistoryService.LoadLastConnectionsAsync();
```


Damit muss der Benutzer seine letzten Verbindungen nicht immer neu eingeben.