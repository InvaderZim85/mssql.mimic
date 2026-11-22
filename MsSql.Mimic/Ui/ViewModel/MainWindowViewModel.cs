using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using ICSharpCode.AvalonEdit.Document;

using MahApps.Metro.Controls.Dialogs;

using Microsoft.Win32;

using MsSql.Mimic.Application.Options;
using MsSql.Mimic.Application.Services;
using MsSql.Mimic.Common;
using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Ui.Enums;
using MsSql.Mimic.Ui.Services;

using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;

namespace MsSql.Mimic.Ui.ViewModel;

/// <summary>
/// Provides the main window presentation model for managing SQL Server connection profiles,
/// browsing database tables, preparing table metadata, and generating C# class source code.
/// </summary>
/// <remarks>
/// Coordinates connection history, connection dialogs, table preparation, alias resolution,
/// and source generation services for the user interface. Maintains the observable state used
/// by the main window, including selected connection and table information, generation options,
/// prepared table metadata, and the generated code document.
/// </remarks>
/// <param name="connectionProfileService">Provides access to saved database connection profiles.</param>
/// <param name="connectionDialogService">Displays and manages database connection dialogs.</param>
/// <param name="tablePreparationService">Prepares database table metadata for processing and generation.</param>
/// <param name="messageService">Provides contextual messages for user-facing operations.</param>
/// <param name="classGenerator">Generates C# class output from prepared metadata.</param>
/// <param name="aliasService">Manages aliases used during metadata preparation or code generation.</param>
/// <param name="generationStatisticsService">The services for collecting and managing statistics related to code generation.</param>
/// <param name="nuGetPackageReader">The service for collecting NuGet package data like the name and version of each used package.</param>
public sealed partial class MainWindowViewModel(
    ConnectionProfileService connectionProfileService,
    IConnectionDialogService connectionDialogService,
    TablePreparationService tablePreparationService,
    ContextualMessageService messageService,
    CSharpClassGenerator classGenerator,
    AliasService aliasService,
    GenerationStatisticsService generationStatisticsService,
    NuGetPackageReader nuGetPackageReader) : BaseViewModel
{
    /// <summary>
    /// Provides cancellation support for asynchronous operations in the main window view model.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Tracks consecutive info command clicks used to trigger the informational message.
    /// </summary>
    private int _clickCount;

    /// <summary>
    /// Stores the timestamp of the first click in the current click sequence used to detect repeated clicks within a time window.
    /// </summary>
    private DateTime _firstClick;
    
    /// <summary>
    /// Stores the prepared table definition currently loaded for code generation.
    /// </summary>
    private PreparedTableDefinition? _preparedTable;

    /// <summary>
    /// Gets the title of the main window.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "MsSql.Mimic";

    /// <summary>
    /// Gets the collection of available connection profiles displayed by the main window view model.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<ConnectionProfile> Connections { get; private set; } = [];

    /// <summary>
    /// Gets or sets the currently selected connection profile.
    /// </summary>
    [ObservableProperty]
    public partial ConnectionProfile? SelectedConnection { get; set; }

    /// <summary>
    /// Contains the list with the tables.
    /// </summary>
    private List<TableEntry> _tables = [];

    /// <summary>
    /// Gets the collection of database tables available for selection in the main window.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<TableEntry> Tables { get; private set; } = [];

    /// <summary>
    /// Gets or sets the currently selected database table whose metadata and generated class output are being prepared.
    /// </summary>
    [ObservableProperty]
    public partial TableEntry? SelectedTable { get; set; }

    /// <summary>
    /// Gets or sets the text used to filter the displayed table list.
    /// </summary>
    [ObservableProperty]
    public partial string TableFilter { get; set; } = string.Empty;

    // ReSharper disable once PropertyCanBeMadeInitOnly.Local
    // ReSharper disable once UnusedMember.Local
    /// <summary>
    /// Gets the collection of column view models for the currently prepared table.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<ColumnViewModel> Columns { get; private set; } = [];

    /// <summary>
    /// Gets or sets the namespace to use for the generated C# class.
    /// </summary>
    [ObservableProperty]
    public partial string NamespaceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated C# class name for the selected database table.
    /// </summary>
    [ObservableProperty]
    public partial string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether generated classes should include Entity Framework Core attributes.
    /// </summary>
    [ObservableProperty]
    public partial bool UseEfCoreAttributes { get; set; } = true;

    /// <summary>
    /// Indicates whether XML summary documentation should be generated for the produced class and its members.
    /// </summary>
    [ObservableProperty]
    public partial bool GenerateSummary { get; set; } = true;

    /// <summary>
    /// Gets or sets the available accessor options that can be selected for generated members.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<AccessorOption> Accessors { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected accessor option used when generating property accessors for the output class.
    /// </summary>
    [ObservableProperty]
    public partial AccessorOption? SelectedAccessor { get; set; }

    /// <summary>
    /// Holds the text document containing the generated C# code displayed in the editor.
    /// </summary>
    [ObservableProperty]
    public partial TextDocument GeneratedCodeDocument { get; private set; } = new();

    // ReSharper disable once PropertyCanBeMadeInitOnly.Local
    // ReSharper disable once UnusedMember.Local
    /// <summary>
    /// Gets the statistics view model associated with the main window.
    /// </summary>
    [ObservableProperty]
    public partial StatisticViewModel Statistics { get; private set; } = new();

    /// <summary>
    /// Indicates whether the view model is currently connected to a database.
    /// </summary>
    [ObservableProperty]
    public partial bool Connected { get; private set; }

    /// <summary>
    /// Gets connection status information displayed by the main window view model.
    /// </summary>
    [ObservableProperty]
    public partial string ConnectionInfo { get; private set; } = "Not connected";

    /// <summary>
    /// Gets the application version number displayed by the main window view model.
    /// </summary>
    [ObservableProperty]
    public partial string VersionNumber { get; private set; } = "v1.0.0.0";

    /// <summary>
    /// Gets the URL of the project's GitHub repository.
    /// </summary>
    public string GitHubUrl => "https://github.com/InvaderZim85/mssql.mimic";

    /// <summary>
    /// Gets the collection of NuGet package information available to the main window view model.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<NuGetPackageInfo> NuGetPackages { get; private set; } = [];

    /// <summary>
    /// Handles changes to the selected table and starts loading its details when a table is selected.
    /// </summary>
    /// <param name="value">The newly selected table entry, or null when the selection is cleared.</param>
    partial void OnSelectedTableChanged(TableEntry? value)
    {
        if (value is not null)
            _ = LoadSelectedTableAsync();
    }

    /// <summary>
    /// Updates the displayed table collection to include only tables whose names match the current filter text, or restores all tables when the filter is empty.
    /// </summary>
    /// <param name="value">The table name filter text used to match available tables.</param>
    partial void OnTableFilterChanged(string value)
    {
        var tables = string.IsNullOrWhiteSpace(value)
            ? _tables
            : _tables.Where(table => table.Name.Contains(value, StringComparison.OrdinalIgnoreCase));

        Tables = tables.ToObservableCollection();
    }

    /// <summary>
    /// Initializes the main window view model by loading available accessor options and previously saved connection profiles while displaying progress and handling load errors.
    /// </summary>
    public async Task InitViewModelAsync()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
        VersionNumber = $"v{version}";
        Title = $"MsSql.Mimic | v{version}";
        
        var controller = await ShowProgressAsync("Please wait", "Please wait while loading the connections...", _cts);

        try
        {
            // Load and set the statistics
            await generationStatisticsService.LoadAsync();
            UpdateStatistics();
            
            // Load the nuget packages
            NuGetPackages = nuGetPackageReader.GetNuGetPackages().ToObservableCollection();
            
            // Set the available accessors
            Accessors = AccessorOption.All.ToObservableCollection();
            SelectedAccessor = Accessors.FirstOrDefault();
            
            // Load the connections
            await LoadConnectionsAsync(cancellationToken: _cts.Token);

            var startupMessage = messageService.GetStartupMessage();
            if (startupMessage is not null)
                ShowInfoText(startupMessage);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Load);
        }
        finally
        {
            await controller.CloseAsync();
        }
    }

    /// <summary>
    /// Loads available connection profiles, updates the connection collection, and selects the specified, current, or first available connection.
    /// </summary>
    /// <param name="connection">The connection profile to select after loading, or null to preserve the current selection when possible.</param>
    /// <param name="cancellationToken">The cancellation token used to cancel the asynchronous load operation.</param>
    /// <returns>A task that represents the asynchronous load operation.</returns>
    private async Task LoadConnectionsAsync(ConnectionProfile? connection = null, CancellationToken cancellationToken = default)
    {
        var selectedConnection = connection ?? SelectedConnection;

        var connections = await connectionProfileService.LoadConnectionsAsync(cancellationToken);
        Connections = connections.ToObservableCollection();

        if (Connections.Count == 0)
            return;
        
        if (selectedConnection is null)
        {
            SelectedConnection = Connections[0];
            return;
        }

        SetSelectedConnection(selectedConnection);
    }

    /// <summary>
    /// Opens the connection manager, refreshes available connection profiles, applies the selected connection when connection is requested, and starts the connection process.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous connection management operation.
    /// </returns>
    [RelayCommand]
    private async Task ManageConnectionsAsync()
    {
        var result = await connectionDialogService.ShowConnectionManagerAsync(_cts.Token);

        await LoadConnectionsAsync(connection: result.SelectedConnection, cancellationToken: _cts.Token);

        if (!result.ConnectRequested || result.SelectedConnection is null)
            return;

        await ConnectAsync();
    }

    /// <summary>
    /// Selects the connection profile from the loaded connections that matches the specified connection details.
    /// </summary>
    /// <param name="connection">The connection profile whose server, database, username, and authentication mode are used to find the matching loaded connection.</param>
    private void SetSelectedConnection(ConnectionProfile connection)
    {
        if (Connections.Count == 0)
            return;

        var tmpConnection = Connections.FirstOrDefault(con =>
            con.Server == connection.Server &&
            con.Database == connection.Database &&
            con.UserName == connection.UserName &&
            con.UseIntegratedSecurity == connection.UseIntegratedSecurity);

        SelectedConnection = tmpConnection;
    }

    /// <summary>
    /// Clears the current surface state by removing loaded tables and columns, resetting the prepared table, and creating a new empty generated code document.
    /// </summary>
    private void ResetSurface()
    {
        Tables.Clear();
        Columns.Clear();
        _preparedTable = null;
        GeneratedCodeDocument = new TextDocument();
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
        if (SelectedConnection is null)
            return;

        var controller = await ShowProgressAsync("Please wait", "Please wait while loading tables...", _cts);

        try
        {
            ResetSurface();
            
            var tables = await tablePreparationService.LoadTablesAsync(SelectedConnection, _cts.Token);
            _tables = [.. tables];
            
            foreach (var table in tables)
                Tables.Add(table);
            
            ShowInfoText("Connection established.", InfoTextType.Success);

            Connected = true;
            ConnectionInfo = $"Connected - {SelectedConnection}";
            
            // Update the last used date
            await connectionProfileService.UpdateLastUsedDateAsync(SelectedConnection);

            var message = messageService.GetSchemaLoadedMessage(Tables.Count);
            if (message is not null)
                ShowInfoText(message);
        }
        catch (Exception ex)
        {
            Connected = false;
            ConnectionInfo = "Not connected";
            
            await ShowErrorAsync(ex, ErrorMessageType.Load);
        }
        finally
        {
            await controller.CloseAsync();
        }
    }

    /// <summary>
    /// Loads metadata for the selected table, updates the prepared table state, and refreshes the displayed column definitions.
    /// </summary>
    /// <return>
    /// A task that represents the asynchronous operation.
    /// </return>
    private async Task LoadSelectedTableAsync()
    {
        if (SelectedConnection is null || SelectedTable is null)
            return;

        var controller = await ShowProgressAsync("Please wait", "Please wait while loading table metadata...", _cts);

        try
        {
            Columns.Clear();

            _preparedTable = await tablePreparationService.LoadPreparedTableAsync(
                SelectedConnection,
                SelectedTable,
                _cts.Token);

            ClassName = _preparedTable.ClassName;
            UseEfCoreAttributes = _preparedTable.UseEfCoreAttributes;

            foreach (var property in _preparedTable.Properties)
                Columns.Add(ColumnViewModel.FromPropertyDefinition(property));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Load);
        }
        finally
        {
            await controller.CloseAsync();
        }
    }

    /// <summary>
    /// Generates C# code for the selected table using the included columns and current generation options, then saves aliases.
    /// </summary>
    /// <return>
    /// A task that represents the asynchronous operation.
    /// </return>
    [RelayCommand]
    private async Task GenerateCodeAsync()
    {
        if (SelectedConnection is null || SelectedTable is null)
            return;

        var selectedProperties = Columns
            .Where(column => column.IsIncluded)
            .Select(column => column.ToPropertyDefinition())
            .ToList();

        if (selectedProperties.Count == 0)
        {
            await ShowMessageAsync("Generate", "You must select at least one column.");
            return;
        }

        if (UseEfCoreAttributes)
        {
            var actualKeyColumns = Columns.Count(column => column.IsPrimaryKey);
            var selectedKeyColumns = selectedProperties.Count(column => column.IsPrimaryKey);
        
            // Compare the amount of key columns to check if there is a mismatch
            if (actualKeyColumns != selectedKeyColumns)
            {
                var result = await ShowQuestionAsync("Incomplete primary key",
                    $"""
                     Incomplete primary key detected.

                     Table: '{SelectedTable.Name}'

                     Primary key columns in the table: {actualKeyColumns}
                     Selected primary key columns: {selectedKeyColumns}

                     The generated class does not include all columns of the primary key.
                     This may lead to configuration and runtime errors when used with EF Core.

                     Do you sill want to proceed with the generation?
                     """,
                    "Yes", "No");

                if (result != MessageDialogResult.Affirmative)
                    return;
            }
        }
        
        var options = new ClassGenerationOptions
        {
            NamespaceName = NamespaceName,
            SchemaName = SelectedTable.Schema,
            TableName = SelectedTable.Name,
            ClassName = ClassName,
            UseEfCoreAttributes = UseEfCoreAttributes,
            GenerateSummary = GenerateSummary,
            TableHasPrimaryKey = Columns.Any(column => column.IsPrimaryKey),
            AccessorKind = SelectedAccessor?.Kind ?? AccessorKind.GetSet,
            Properties = selectedProperties
        };

        GeneratedCodeDocument = new TextDocument(classGenerator.Generate(options));

        await SaveColumnAliasesAsync(true);

        generationStatisticsService.TrackGeneration(SelectedConnection, SelectedTable);
        generationStatisticsService.ScheduleSave();
        UpdateStatistics();
        
        var message = messageService.GetGenerationMessage(SelectedTable.Schema, SelectedTable.Name);
        if (message is not null)
            ShowInfoText(message);
    }

    /// <summary>
    /// Refreshes the displayed generation statistics using the current statistics from the statistics service.
    /// </summary>
    private void UpdateStatistics()
    {
        Statistics.UpdateStatistics(generationStatisticsService.Statistics);
        Statistics.InfoMessage = messageService.GetStatisticsInfoMessage(Statistics.TotalGeneratedTableCount, Statistics.TotalGeneratedClassCount);
    }

    /// <summary>
    /// Copies the generated C# code to the clipboard when code is available.
    /// </summary>
    [RelayCommand]
    private void CopyCode()
    {
        var generatedCode = GeneratedCodeDocument.Text;
        if (string.IsNullOrWhiteSpace(generatedCode))
            return;

        CopyToClipboard(generatedCode);
    }

    /// <summary>
    /// Saves the currently generated C# code to a selected file.
    /// </summary>
    /// <return>
    /// A task that represents the asynchronous save operation.
    /// </return>
    [RelayCommand]
    private async Task SaveCodeAsync()
    {
        var generatedCode = GeneratedCodeDocument.Text;
        if (string.IsNullOrWhiteSpace(generatedCode))
            return;
        
        var dialog = new SaveFileDialog
        {
            Filter = "C# file (*.cs)|*.cs",
            DefaultExt = ".cs",
            FileName = ClassName
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            
            await File.WriteAllTextAsync(dialog.FileName, generatedCode);

            await ShowMessageAsync("Save", "File successfully saved.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Save);
        }
    }

    /// <summary>
    /// Saves column aliases for the current table selection and displays any applicable feedback.
    /// </summary>
    /// <return>
    /// A task that represents the asynchronous save operation.
    /// </return>
    [RelayCommand]
    private async Task SaveAliasesAsync()
    {
        await SaveColumnAliasesAsync(false);
    }

    /// <summary>
    /// Saves the current column alias and generation metadata for the selected connection and table.
    /// </summary>
    /// <param name="silent">Indicates whether success notification text should be suppressed after saving.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    private async Task SaveColumnAliasesAsync(bool silent)
    {
        if (SelectedConnection is null || SelectedTable is null)
            return;

        var properties = Columns
            .Select(column => column.ToPropertyDefinition())
            .ToList();

        try
        {
            await aliasService.SaveAsync(
                SelectedConnection,
                SelectedTable,
                ClassName,
                UseEfCoreAttributes,
                properties,
                _cts.Token);

            if (silent)
                return;
            
            ShowInfoText("Aliases saved.", InfoTextType.Success);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Save);
        }
    }

    /// <summary>
    /// Updates the inclusion state of all available columns according to the specified column selection mode.
    /// </summary>
    /// <param name="selectionMode">The selection mode that determines whether all columns are included or excluded.</param>
    [RelayCommand]
    private void SelectColumns(ColumnSelectionMode selectionMode)
    {
        if (Columns.Count == 0)
            return;

        foreach (var column in Columns)
            column.IsIncluded = selectionMode is ColumnSelectionMode.All;
    }

    /// <summary>
    /// Resets all pending column changes for the selected table after user confirmation, reloads the prepared table definition, updates the displayed columns, and reports the reset result.
    /// </summary>
    /// <return>A task that represents the asynchronous reset operation.</return>
    [RelayCommand]
    private async Task ResetColumnChangesAsync()
    {
        if (SelectedConnection is null || SelectedTable is null)
            return;

        var result = await ShowQuestionAsync("Reset", "Do you really want to undo all the changes that have been made?", "Yes", "No");

        if (result != MessageDialogResult.Affirmative)
            return;

        var controller = await ShowProgressAsync("Please wait", "Please wait while resetting column changes...", _cts);

        try
        {
            Columns.Clear();

            _preparedTable = await tablePreparationService.ResetPreparedTableAsync(
                SelectedConnection,
                SelectedTable,
                _cts.Token);

            ClassName = _preparedTable.ClassName;
            UseEfCoreAttributes = _preparedTable.UseEfCoreAttributes;

            foreach (var property in _preparedTable.Properties)
                Columns.Add(ColumnViewModel.FromPropertyDefinition(property));

            ShowInfoText("Column changes reset.", InfoTextType.Success);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Load);
        }
        finally
        {
            await controller.CloseAsync();
        }
    }

    /// <summary>
    /// Opens the project repository page on GitHub in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenOnGithub()
    {
        Helpers.OpenUrl(GitHubUrl);
    }

    /// <summary>
    /// Handles the information command by counting repeated activations within a short time window and displaying application information when the activation threshold is reached.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RelayCommand]
    private async Task ShowInfoAsync()
    {
        var now = DateTime.Now;

        if (_clickCount == 0 || now - _firstClick > TimeSpan.FromSeconds(5))
        {
            _clickCount = 0;
            _firstClick = now;
        }

        _clickCount++;

        if (_clickCount >= 5)
        {
            _clickCount = 0;
            await ShowMessageAsync("Info", messageService.GetInfoMessage());
        }
    }
}