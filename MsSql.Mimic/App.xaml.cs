using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MsSql.Mimic.Application.Services;
using MsSql.Mimic.Domain.Services;
using MsSql.Mimic.Infrastructure.MsSql;
using MsSql.Mimic.Infrastructure.Sqlite;
using MsSql.Mimic.Ui.Services;
using MsSql.Mimic.Ui.View;
using MsSql.Mimic.Ui.ViewModel;

using Serilog;
using Serilog.Events;

using System.IO;
using System.Windows;

namespace MsSql.Mimic;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    /// <summary>
    /// Holds the application's root dependency injection service provider built during startup for resolving registered services, creating scopes, initializing application resources, and obtaining windows and services.
    /// </summary>
    private ServiceProvider _serviceProvider = null!;

    /// <inheritdoc />
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Init the log
            InitLog(LogEventLevel.Information);

            // Create and show the splash screen
            var splashScreen = new SplashScreen("Assets/SplashScreen.png");
            splashScreen.Show(autoClose: false);

            base.OnStartup(e);

            // Set the shutdown mode to explicit
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Prepare the DI
            var services = new ServiceCollection();

            // Database
            services.AddDbContext<AliasDbContext>(options => options.UseSqlite("Data Source=aliases.db"));
            services.AddSingleton<PasswordProtector>();
            services.AddSingleton<ConnectionProfileService>();
            services.AddSingleton<SqlConnectionFactory>();

            // Services
            services.AddSingleton<GenerationStatisticsService>();
            services.AddSingleton<ContextualMessageService>();
            services.AddSingleton<CSharpNameService>();
            services.AddSingleton<SqlTypeMapper>();
            services.AddTransient<MsSqlSchemaReader>();
            services.AddTransient<AliasService>();
            services.AddTransient<PropertyDefinitionFactory>();
            services.AddTransient<TablePreparationService>();
            services.AddTransient<CSharpClassGenerator>();

            // Windows
            services.AddTransient<ConnectionManagerWindowViewModel>();
            services.AddTransient<ConnectionManagerWindow>();
            services.AddSingleton<IConnectionDialogService, ConnectionWindowService>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // AliasDB Preparation
            await using var scope = _serviceProvider.CreateAsyncScope();
            var aliasDbContext = scope.ServiceProvider.GetRequiredService<AliasDbContext>();
            await aliasDbContext.Database.EnsureCreatedAsync(); // Create the DB if it's not exist

            // Get the main window and show it
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            if (MonitorHelper.ShouldStartFullscreen(mainWindow))
                mainWindow.WindowState = WindowState.Maximized;
            else
            {
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Width = 1_920;
                mainWindow.Height = 1_080;
            }
            
            mainWindow.Closed += (_, _) => Shutdown(); // Close the application.

            // Close the splash screen
            splashScreen.Close(TimeSpan.FromMilliseconds(500));

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "The application terminated unexpectedly.");
        }
    }

    /// <summary>
    /// Init the logger.
    /// </summary>
    /// <param name="logEventLevel">The desired log level.</param>
    private static void InitLog(LogEventLevel logEventLevel)
    {
        // Template
        const string template = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        // Init the logger
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logEventLevel) // Set the desired min. log level
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "log", "log_.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: template) // Add the file sink
            .CreateLogger();
    }
}