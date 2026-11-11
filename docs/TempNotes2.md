Ja. Die `Action`/`Func`-Lösung funktioniert, ist aber etwas umständlich, weil dein ViewModel dadurch indirekt das View kennt. Besser ist eine kleine **Attached Property** oder ein **Behavior**, mit dem du `PasswordBox.Password` bindbar machst.

WPF macht `PasswordBox.Password` absichtlich nicht als DependencyProperty verfügbar, damit Passwörter nicht versehentlich überall im Binding-System landen. In Desktop-Tools ist eine Attached-Property-Lösung aber oft der pragmatischste Weg.

## Empfehlung: Attached Property für `PasswordBox`

Lege z. B. eine Klasse `PasswordBoxAssistant` an.

```csharp
using System.Windows;
using System.Windows.Controls;

namespace MsSql.ClassGenerator.Ui.Behaviors;

public static class PasswordBoxAssistant
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxAssistant),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    public static readonly DependencyProperty BindPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BindPassword",
            typeof(bool),
            typeof(PasswordBoxAssistant),
            new PropertyMetadata(false, OnBindPasswordChanged));

    private static readonly DependencyProperty UpdatingPasswordProperty =
        DependencyProperty.RegisterAttached(
            "UpdatingPassword",
            typeof(bool),
            typeof(PasswordBoxAssistant),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(BoundPasswordProperty);
    }

    public static void SetBoundPassword(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(BoundPasswordProperty, value);
    }

    public static bool GetBindPassword(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(BindPasswordProperty);
    }

    public static void SetBindPassword(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(BindPasswordProperty, value);
    }

    private static bool GetUpdatingPassword(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(UpdatingPasswordProperty);
    }

    private static void SetUpdatingPassword(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(UpdatingPasswordProperty, value);
    }

    private static void OnBindPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox passwordBox)
            return;

        if ((bool)e.OldValue)
            passwordBox.PasswordChanged -= OnPasswordChanged;

        if ((bool)e.NewValue)
            passwordBox.PasswordChanged += OnPasswordChanged;
    }

    private static void OnBoundPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox passwordBox)
            return;

        if (GetUpdatingPassword(passwordBox))
            return;

        var newPassword = e.NewValue as string ?? string.Empty;

        if (passwordBox.Password == newPassword)
            return;

        passwordBox.Password = newPassword;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
            return;

        SetUpdatingPassword(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetUpdatingPassword(passwordBox, false);
    }
}
```


Danach kannst du dein XAML direkt an dein ViewModel binden.

```xml
<mah:MetroWindow
    x:Class="MsSql.ClassGenerator.Ui.ConnectionWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:behaviors="clr-namespace:MsSql.ClassGenerator.Ui.Behaviors"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mah="http://metro.mahapps.com/winfx/xaml/controls"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    Title="{Binding Title}"
    Width="400"
    Height="450"
    mah:DialogParticipation.Register="{Binding}"
    WindowStartupLocation="CenterScreen"
    mc:Ignorable="d">

    <!-- ... existing code ... -->

    <PasswordBox
        Name="PasswordBox"
        Grid.Row="4"
        Grid.Column="1"
        Margin="3"
        behaviors:PasswordBoxAssistant.BindPassword="True"
        behaviors:PasswordBoxAssistant.BoundPassword="{Binding Connection.Password, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

    <!-- ... existing code ... -->
</mah:MetroWindow>
```


Damit brauchst du im Code-Behind keine Passwort-Callback-Logik mehr.

```csharp
using MahApps.Metro.Controls;

using MsSql.ClassGenerator.Ui.ViewModel;

namespace MsSql.ClassGenerator.Ui;

public partial class ConnectionWindow : MetroWindow
{
    public ConnectionWindow(ConnectionWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        Loaded += (_, _) => viewModel.InitViewModel();

        viewModel.CloseRequested += (_, result) => DialogResult = result;
    }
}
```


Und dein ViewModel braucht keine `Action<string>` / `Func<string>` mehr.

```csharp
// ... existing code ...
public sealed partial class ConnectionWindowViewModel : BaseViewModel
{
    public event EventHandler<bool?>? CloseRequested;

    private readonly SqlConnectionFactory? _connectionFactory;

    private readonly ConnectionHistoryService? _connectionHistoryService;

    public ConnectionWindowViewModel()
    {

    }

    public ConnectionWindowViewModel(SqlConnectionFactory connectionFactory, ConnectionHistoryService connectionHistoryService)
    {
        _connectionFactory = connectionFactory;
        _connectionHistoryService = connectionHistoryService;
    }

    public ConnectionViewModel Connection { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<IdValueDto> ApplicationIntents { get; private set; } = [];

    public void InitViewModel()
    {
        ApplicationIntents.Clear();

        foreach (var intent in Enum.GetValues<ConnectionIntent>())
            ApplicationIntents.Add(new IdValueDto((int)intent, intent.ToString()));

        var selectedIntent = ApplicationIntents.FirstOrDefault(intent => intent.Id == Connection.ApplicationIntent.Id);
        Connection.ApplicationIntent = selectedIntent ?? ApplicationIntents[0];
    }

    [RelayCommand]
    private async Task TryConnect()
    {
        if (_connectionFactory is null)
            return;

        if (Connection.UseIntegratedSecurity)
            Connection.Password = string.Empty;

        var connectionProfile = new ConnectionProfile(Connection);
        var result = await _connectionFactory.CanConnect(connectionProfile);

        if (result.Success)
        {
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
}
```


## Noch besser: Domain nicht vom UI-ViewModel abhängig machen

In deinem `ConnectionProfile` ist aktuell ein Konstruktor drin, der ein UI-ViewModel annimmt. Das ist architektonisch ungünstig, weil damit dein Domain-Modell die UI-Schicht kennt.

Besser wäre z. B.:

```csharp
public ConnectionProfile(
    string server,
    string database,
    bool useIntegratedSecurity,
    string? userName,
    string? password,
    bool trustServerCertificate,
    bool encrypt,
    ConnectionIntent intent)
{
    Server = server;
    Database = database;
    UseIntegratedSecurity = useIntegratedSecurity;
    UserName = userName;
    Password = password;
    TrustServerCertificate = trustServerCertificate;
    Encrypt = encrypt;
    Intent = intent;
}
```


Oder du machst eine Mapping-Methode im ViewModel:

```csharp
public ConnectionProfile ToProfile()
{
    return new ConnectionProfile
    {
        Server = Server,
        Database = Database,
        UseIntegratedSecurity = UseIntegratedSecurity,
        UserName = UserName,
        Password = Password,
        TrustServerCertificate = TrustServerCertificates,
        Encrypt = Encrypt,
        Intent = (ConnectionIntent)ApplicationIntent.Id
    };
}
```


Dann verwendest du im `ConnectionWindowViewModel`:

```csharp
var connectionProfile = Connection.ToProfile();
```


Das hält die Abhängigkeiten sauberer:

```plain text
UI/ViewModel -> Domain
Domain -> nicht zurück zur UI
```


## Kurzfazit

Ich würde es so machen:

1. `PasswordBoxAssistant` als Attached Property hinzufügen.
2. `PasswordBox.Password` per Attached Property an `Connection.Password` binden.
3. `Action<string>` und `Func<string>` aus `ConnectionWindowViewModel` entfernen.
4. `ConnectionProfile` nicht direkt von `ConnectionViewModel` abhängig machen.

Das ist deutlich MVVM-freundlicher und macht dein Fenster einfacher wiederverwendbar für „Neu“ und „Bearbeiten“.