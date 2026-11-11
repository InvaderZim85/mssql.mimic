using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MsSql.Mimic.Application.Services;
using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;
using MsSql.Mimic.Infrastructure.MsSql;
using MsSql.Mimic.Infrastructure.Sqlite;
using MsSql.Mimic.Ui;
using MsSql.Mimic.Ui.Services;
using MsSql.Mimic.Ui.ViewModel;

using System.Windows;

namespace MsSql.Mimic;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider _serviceProvider = null!;

    /// <inheritdoc />
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        
        var services = new ServiceCollection();

        services.AddDbContext<AliasDbContext>(options => options.UseSqlite("Data Source=aliases.db"));

        services.AddSingleton<PasswordProtector>();
        services.AddSingleton<ConnectionHistoryService>();
        services.AddSingleton<SqlConnectionFactory>();
        
        services.AddSingleton<CSharpNameService>();
        services.AddSingleton<SqlTypeMapper>();
        services.AddTransient<MsSqlSchemaReader>();
        services.AddTransient<AliasService>();
        services.AddTransient<PropertyDefinitionFactory>();
        services.AddTransient<TablePreparationService>();
        services.AddTransient<CSharpClassGenerator>();
        
        // TODO: MainWindowViewModel
        services.AddTransient<ConnectionWindowViewModel>();
        services.AddTransient<ConnectionWindow>();
        services.AddSingleton<IConnectionDialogService, ConnectionDialogService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // AliasDB Preparation
        await using var scope = _serviceProvider.CreateAsyncScope();
        var aliasDbContext = scope.ServiceProvider.GetRequiredService<AliasDbContext>();
        await aliasDbContext.Database.EnsureCreatedAsync(); // Create the DB if it's not exist
        
        // Get the main window and show it
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Closed += (_, _) => Shutdown(); // Close the application.
        mainWindow.Show();
    }
}