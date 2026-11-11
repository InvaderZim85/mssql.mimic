Du kannst das bestehende Fenster gut wiederverwenden, wenn du es in zwei Modi betreibst:

- **Neu anlegen**: leeres `ConnectionDto`
- **Bearbeiten**: `ConnectionDto` wird aus einem vorhandenen `ConnectionProfile` befüllt
- Beim Speichern einer bearbeiteten Verbindung muss die alte History ggf. gelöscht werden, falls sich Schlüssel-Felder wie `Server`, `Database`, `UseIntegratedSecurity` oder `UserName` ändern.

Wichtig: Dein `ConnectionProfile` ist ein `record` mit `init`-Properties. Deshalb solltest du bestehende Einträge nicht direkt ändern, sondern nach dem Dialog den Eintrag in der ObservableCollection ersetzen.

---

## 1. `ConnectionDto` um Mapping-Methode erweitern

Damit du ein bestehendes Profil sauber ins Fenster laden kannst:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

using MsSql.ClassGenerator.Domain.Enums;
using MsSql.ClassGenerator.Domain.Models;

namespace MsSql.ClassGenerator.Domain.Dtos;

public sealed partial class ConnectionDto : ObservableObject
{
    [ObservableProperty]
    public partial string Server { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Database { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool UseIntegratedSecurity { get; set; } = true;

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TrustServerCertificates { get; set; } = true;

    [ObservableProperty]
    public partial bool Encrypt { get; set; } = true;

    [ObservableProperty]
    public partial IdValueDto ApplicationIntent { get; set; } = IdValueDto.Empty();

    public static ConnectionDto FromProfile(ConnectionProfile profile)
    {
        return new ConnectionDto
        {
            Server = profile.Server,
            Database = profile.Database,
            UseIntegratedSecurity = profile.UseIntegratedSecurity,
            UserName = profile.UserName ?? string.Empty,
            Password = profile.Password ?? string.Empty,
            TrustServerCertificates = profile.TrustServerCertificate,
            Encrypt = profile.Encrypt,
            ApplicationIntent = new IdValueDto((int)profile.Intent, profile.Intent.ToString())
        };
    }
}
```


---

## 2. `ConnectionWindowViewModel` auf Neu/Bearbeiten erweitern

Du brauchst:

- `Title`
- `AcceptButtonText`
- optionales `OriginalConnection`
- `LoadConnection(...)`
- `CancelCommand`
- beim Speichern ggf. alte Verbindung löschen

```csharp
// ... existing code ...
public sealed partial class ConnectionWindowViewModel : BaseViewModel
{
    public event EventHandler<bool?>? CloseRequested;

    private readonly SqlConnectionFactory? _connectionFactory;

    private readonly ConnectionHistoryService? _connectionHistoryService;

    private Action<string>? _setPassword;
    private Func<string>? _getPassword;

    private ConnectionProfile? _originalConnection;

    [ObservableProperty]
    public partial string Title { get; private set; } = "New connection";

    [ObservableProperty]
    public partial string AcceptButtonText { get; private set; } = "Connect";

    public ConnectionWindowViewModel()
    {

    }

    public ConnectionWindowViewModel(SqlConnectionFactory connectionFactory, ConnectionHistoryService connectionHistoryService)
    {
        _connectionFactory = connectionFactory;
        _connectionHistoryService = connectionHistoryService;
    }

    public ConnectionDto Connection { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<IdValueDto> ApplicationIntents { get; private set; } = [];

    public void LoadConnection(ConnectionProfile connection)
    {
        _originalConnection = connection;

        Title = "Edit connection";
        AcceptButtonText = "Save";

        Connection = ConnectionDto.FromProfile(connection);
        OnPropertyChanged(nameof(Connection));

        _setPassword?.Invoke(Connection.Password);
    }

    public void InitViewModel(Action<string> setPassword, Func<string> getPassword)
    {
        _setPassword = setPassword;
        _getPassword = getPassword;

        ApplicationIntents.Clear();

        foreach (var intent in Enum.GetValues<ConnectionIntent>())
            ApplicationIntents.Add(new IdValueDto((int)intent, intent.ToString()));

        var selectedIntent = ApplicationIntents.FirstOrDefault(intent => intent.Id == Connection.ApplicationIntent.Id);
        Connection.ApplicationIntent = selectedIntent ?? ApplicationIntents[0];

        _setPassword.Invoke(Connection.Password);
    }

    [RelayCommand]
    private async Task TryConnect()
    {
        if (_connectionFactory is null)
            return;

        if (!Connection.UseIntegratedSecurity)
            Connection.Password = _getPassword?.Invoke() ?? string.Empty;
        else
            Connection.Password = string.Empty;

        var connectionProfile = new ConnectionProfile(Connection);
        var result = await _connectionFactory.CanConnect(connectionProfile);

        if (result.Success)
        {
            if (_originalConnection is not null && IsConnectionKeyChanged(_originalConnection, connectionProfile))
                await _connectionHistoryService?.DeleteConnectionAsync(_originalConnection)!;

            await _connectionHistoryService?.SaveConnectionAsync(connectionProfile)!;

            CloseRequested?.Invoke(this, true);
        }
        else
            await ShowMessageAsync("Connection failed.",
                $"""
                 Can't establish connection to server '{Connection.Server}' / database '{Connection.Database}'.

                 Message:
                 {result.ErrorMessage}
                 """);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }

    private static bool IsConnectionKeyChanged(ConnectionProfile oldConnection, ConnectionProfile newConnection)
    {
        return !string.Equals(oldConnection.Server, newConnection.Server, StringComparison.OrdinalIgnoreCase)
               || !string.Equals(oldConnection.Database, newConnection.Database, StringComparison.OrdinalIgnoreCase)
               || oldConnection.UseIntegratedSecurity != newConnection.UseIntegratedSecurity
               || !string.Equals(oldConnection.UserName, newConnection.UserName, StringComparison.OrdinalIgnoreCase);
    }
}
```


Hinweis: Wenn `_connectionHistoryService` optional bleiben soll, ist die `await _connectionHistoryService?...!`-Schreibweise nicht ideal. Sauberer wäre ein früher Guard:

```csharp
if (_connectionFactory is null || _connectionHistoryService is null)
    return;
```


Dann kannst du die Null-forgiving-Operatoren vermeiden.

---

## 3. `ConnectionWindow.xaml` an ViewModel-Metadaten binden

Wichtig ist hier:

- `Title="{Binding Title}"`
- Button-Text über `AcceptButtonText`
- Cancel-Button mit Command
- die lokale `<Window.DataContext>`-Deklaration solltest du entfernen, weil du den ViewModel bereits per DI im Code-Behind setzt.

Außerdem sollten TextBox-Bindings mit `UpdateSourceTrigger=PropertyChanged` arbeiten, damit die DTO-Werte sicher aktuell sind.

```xml
<mah:MetroWindow
    x:Class="MsSql.ClassGenerator.Ui.ConnectionWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mah="http://metro.mahapps.com/winfx/xaml/controls"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    Title="{Binding Title}"
    Width="400"
    Height="450"
    mah:DialogParticipation.Register="{Binding}"
    WindowStartupLocation="CenterScreen"
    mc:Ignorable="d">

    <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto,32,Auto,Auto,32,32,Auto,*,Auto">
        <Label
            Grid.Row="0"
            Grid.Column="0"
            Content="Server:" />
        <TextBox
            Grid.Row="0"
            Grid.Column="1"
            Text="{Binding Connection.Server, UpdateSourceTrigger=PropertyChanged}" />

        <Label
            Grid.Row="1"
            Grid.Column="0"
            Content="Database:" />
        <TextBox
            Grid.Row="1"
            Grid.Column="1"
            Text="{Binding Connection.Database, UpdateSourceTrigger=PropertyChanged}" />

        <Label
            Grid.Row="2"
            Grid.Column="0"
            Content="Integrated security:" />
        <CheckBox
            Grid.Row="2"
            Grid.Column="1"
            IsChecked="{Binding Connection.UseIntegratedSecurity}" />

        <Label
            Grid.Row="3"
            Grid.Column="0"
            Content="Username:" />
        <TextBox
            Grid.Row="3"
            Grid.Column="1"
            Text="{Binding Connection.UserName, UpdateSourceTrigger=PropertyChanged}" />

        <Label
            Grid.Row="4"
            Grid.Column="0"
            Content="Password:" />
        <PasswordBox
            Name="PasswordBox"
            Grid.Row="4"
            Grid.Column="1"
            Margin="3" />

        <!-- ... existing code ... -->

        <StackPanel
            Grid.Row="9"
            Grid.Column="0"
            Grid.ColumnSpan="2"
            HorizontalAlignment="Right"
            Orientation="Horizontal">
            <Button
                Width="75"
                Command="{Binding TryConnectCommand}"
                Content="{Binding AcceptButtonText}" />
            <Button
                Width="75"
                Command="{Binding CancelCommand}"
                Content="Cancel" />
        </StackPanel>
    </Grid>
</mah:MetroWindow>
```


---

## 4. Dialog-Service um Edit-Methode erweitern

Das Interface sollte neben „New“ auch „Edit“ anbieten.

```csharp
namespace MsSql.ClassGenerator.Domain.Models;

public interface IConnectionDialogService
{
    Task<ConnectionProfile?> ShowNewConnectionDialogAsync(CancellationToken ct = default);

    Task<ConnectionProfile?> ShowEditConnectionDialogAsync(ConnectionProfile connection, CancellationToken ct = default);
}
```


Die Implementierung lädt für den Edit-Fall das bestehende Profil ins ViewModel:

```csharp
using Microsoft.Extensions.DependencyInjection;

using MsSql.ClassGenerator.Domain.Models;
using MsSql.ClassGenerator.Ui.ViewModel;

namespace MsSql.ClassGenerator.Ui.Services;

public sealed class ConnectionDialogService(IServiceProvider serviceProvider) : IConnectionDialogService
{
    /// <inheritdoc />
    public Task<ConnectionProfile?> ShowNewConnectionDialogAsync(CancellationToken ct = default)
    {
        var window = serviceProvider.GetRequiredService<ConnectionWindow>();

        var result = window.ShowDialog();

        if (result != true || window.DataContext is not ConnectionWindowViewModel viewModel)
            return Task.FromResult<ConnectionProfile?>(null);

        var connectionProfile = new ConnectionProfile(viewModel.Connection);
        return Task.FromResult<ConnectionProfile?>(connectionProfile);
    }

    /// <inheritdoc />
    public Task<ConnectionProfile?> ShowEditConnectionDialogAsync(ConnectionProfile connection, CancellationToken ct = default)
    {
        var window = serviceProvider.GetRequiredService<ConnectionWindow>();

        if (window.DataContext is ConnectionWindowViewModel viewModel)
            viewModel.LoadConnection(connection);

        var result = window.ShowDialog();

        if (result != true || window.DataContext is not ConnectionWindowViewModel updatedViewModel)
            return Task.FromResult<ConnectionProfile?>(null);

        var connectionProfile = new ConnectionProfile(updatedViewModel.Connection);
        return Task.FromResult<ConnectionProfile?>(connectionProfile);
    }
}
```


---

## 5. MainWindow um Bearbeiten-Button erweitern

Neben `+` kannst du z. B. einen `Edit`-Button hinzufügen.

```xml
<!--  Connection  -->
<Border Grid.Row="0" Style="{StaticResource BorderStyle}">
    <Grid ColumnDefinitions="Auto,400,Auto,Auto,Auto,*" RowDefinitions="Auto,Auto,Auto">
        <Label
            Grid.Row="0"
            Grid.Column="0"
            Grid.ColumnSpan="6"
            Content="Connection"
            Style="{StaticResource Headline}" />

        <Separator
            Grid.Row="1"
            Grid.Column="0"
            Grid.ColumnSpan="6" />

        <Label
            Grid.Row="2"
            Grid.Column="0"
            Content="Connection:" />

        <ComboBox
            Grid.Row="2"
            Grid.Column="1"
            ItemsSource="{Binding Connections, Mode=OneWay}"
            SelectedItem="{Binding SelectedConnection}" />

        <Button
            Grid.Row="2"
            Grid.Column="2"
            Width="30"
            Command="{Binding NewConnectionCommand}"
            Content="+"
            ToolTip="Adds a new connection" />

        <Button
            Grid.Row="2"
            Grid.Column="3"
            Width="45"
            Command="{Binding EditConnectionCommand}"
            Content="Edit"
            ToolTip="Edits the selected connection" />

        <Button
            Grid.Row="2"
            Grid.Column="4"
            Command="{Binding ConnectCommand}"
            Content="Connect" />
    </Grid>
</Border>
```


---

## 6. `MainWindowViewModel` um Edit-Command erweitern

Der Dialog gibt ein neues `ConnectionProfile` zurück. Danach ersetzt du den alten Eintrag in der Collection.

```csharp
// ... existing code ...
[RelayCommand]
private async Task NewConnectionAsync()
{
    if (_connectionDialogService is null)
        return;

    var connection = await _connectionDialogService.ShowNewConnectionDialogAsync(_cts.Token);

    if (connection is null)
        return;

    Connections.Add(connection);
    SelectedConnection = connection;
}

[RelayCommand]
private async Task EditConnectionAsync()
{
    if (_connectionDialogService is null || SelectedConnection is null)
        return;

    var oldConnection = SelectedConnection;
    var updatedConnection = await _connectionDialogService.ShowEditConnectionDialogAsync(oldConnection, _cts.Token);

    if (updatedConnection is null)
        return;

    var index = Connections.IndexOf(oldConnection);

    if (index >= 0)
        Connections[index] = updatedConnection;
    else
        Connections.Add(updatedConnection);

    SelectedConnection = updatedConnection;

    Tables.Clear();
    Columns.Clear();
    PreparedTable = null;
    GeneratedCode = string.Empty;
    GeneratedCodeDocument = new TextDocument();

    ShowInfoText("Connection updated.", InfoTextType.Success);
}

/// <summary>
/// Connects to the selected connection profile, loads the available tables, resets the current table and generated output state, and reports the connection result.
/// </summary>
/// <return>
/// A task that represents the asynchronous operation.
/// </return>
[RelayCommand]
private async Task ConnectAsync()
{
    if (SelectedConnection is null || _tablePreparationService is null)
        return;
// ... existing code ...
```


Optional kannst du den Edit-Command deaktivieren, wenn keine Verbindung gewählt ist. Dafür kannst du beim CommunityToolkit z. B. eine `CanExecute`-Methode verwenden. Für den Anfang reicht der Guard im Command aber völlig.

---

## 7. Wichtig: Reihenfolge bei `LoadConnection`

Da dein `PasswordBox` erst in `InitViewModel(...)` erreichbar ist, ist es gut, dass `LoadConnection(...)` auch ohne gesetzte `_setPassword` funktioniert und in `InitViewModel(...)` nochmal `_setPassword.Invoke(Connection.Password)` aufgerufen wird.

Damit ist egal, ob `LoadConnection(...)` vor oder nach `Loaded` ausgeführt wird.

---

## Ergebnis

Danach hast du:

- `+` öffnet leeres Fenster
- `Edit` öffnet dasselbe Fenster mit vorhandenen Werten
- Passwort wird ins `PasswordBox` übernommen
- Speichern testet die Verbindung
- bei geändertem Schlüssel wird der alte History-Eintrag entfernt
- die ComboBox bekommt das aktualisierte `ConnectionProfile`
- Tabellen/Spalten/Code werden zurückgesetzt, weil die Verbindung nicht mehr garantiert dieselbe ist.