using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using ICSharpCode.AvalonEdit.Document;

using MahApps.Metro.Controls.Dialogs;

using Microsoft.Win32;

using MsSql.Mimic.Application.Options;
using MsSql.Mimic.Application.Services;
using MsSql.Mimic.Domain.Models;

using System.Collections.ObjectModel;
using System.IO;

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
public sealed partial class MainWindowViewModel : BaseViewModel
{
    /// <summary>
    /// Provides cancellation support for asynchronous operations in the main window view model.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Manages loading and deleting persisted database connection history for the main window view model.
    /// </summary>
    private readonly ConnectionHistoryService? _connectionHistoryService;

    /// <summary>
    /// Provides access to connection dialog operations for creating and editing connection profiles.
    /// </summary>
    private readonly IConnectionDialogService? _connectionDialogService;

    /// <summary>
    /// Provides table loading and metadata preparation services for the main window view model.
    /// </summary>
    private readonly TablePreparationService? _tablePreparationService;

    /// <summary>
    /// Generates C# class source code from the selected table metadata and generation options.
    /// </summary>
    private readonly CSharpClassGenerator? _classGenerator;

    /// <summary>
    /// Provides access to alias persistence operations for saving class and column alias settings.
    /// </summary>
    private readonly AliasService? _aliasService;

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

    /// <summary>
    /// Gets the collection of column view models for the currently prepared table.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<ColumnViewModel> Columns { get; private set; } = [];

    /// <summary>
    /// Gets or sets the namespace to use for the generated C# class.
    /// </summary>
    [ObservableProperty]
    public partial string NamespaceName { get; set; } = "Generated";

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

    /// <summary>
    /// Gets the prepared table definition for the currently selected table.
    /// </summary>
    [ObservableProperty]
    public partial PreparedTableDefinition? PreparedTable { get; private set; }

    /// <summary>
    /// Provides the main window view model responsible for coordinating database connection selection, table metadata loading, generation settings, and generated C# output.
    /// </summary>
    public MainWindowViewModel() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class with the services required to manage connections, prepare table metadata, generate C# code, and resolve aliases.
    /// </summary>
    /// <param name="connectionHistoryService">The service used to load and manage saved database connection profiles.</param>
    /// <param name="connectionDialogService">The service used to display and handle database connection dialogs.</param>
    /// <param name="tablePreparationService">The service used to prepare selected table metadata for class generation.</param>
    /// <param name="classGenerator">The service used to generate C# source code from prepared table definitions.</param>
    /// <param name="aliasService">The service used to manage table and column aliases.</param>
    public MainWindowViewModel(
        ConnectionHistoryService connectionHistoryService,
        IConnectionDialogService connectionDialogService,
        TablePreparationService tablePreparationService,
        CSharpClassGenerator classGenerator,
        AliasService aliasService)
    {
        _connectionHistoryService = connectionHistoryService;
        _connectionDialogService = connectionDialogService;
        _tablePreparationService = tablePreparationService;
        _classGenerator = classGenerator;
        _aliasService = aliasService;
    }

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
    public async void InitViewModel()
    {
        if (_connectionHistoryService is null)
            return;
        
        var controller = await ShowProgressAsync("Please wait", "Please wait while loading the connections...", _cts);

        try
        {
            // Set the available accessors
            Accessors = AccessorOption.All.ToObservableCollection();
            SelectedAccessor = Accessors.FirstOrDefault();
            
            // Load the connections
            var connections = await _connectionHistoryService.LoadLastConnectionsAsync(_cts.Token);
            Connections = connections.ToObservableCollection();
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
    /// Opens the new connection dialog and, when a connection profile is provided, adds it to the available connections and selects it.
    /// </summary>
    /// <return>
    /// A task that represents the asynchronous operation.
    /// </return>
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

        ShowInfoText("New connection created.", InfoTextType.Success);
    }

    /// <summary>
    /// Opens the edit dialog for the selected connection, updates the connection collection with the edited profile, resets the current surface, and displays a success notification.
    /// </summary>
    /// <return>
    /// A task that represents the asynchronous edit operation.
    /// </return>
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
        
        ResetSurface();
        
        ShowInfoText("Connection updated.", InfoTextType.Success);
    }

    /// <summary>
    /// Deletes the selected connection after confirmation, removes it from the saved connection history, and clears the current selection.
    /// </summary>
    /// <return>A task that represents the asynchronous delete operation.</return>
    [RelayCommand]
    private async Task DeleteConnectionAsync()
    {
        if (_connectionHistoryService is null || SelectedConnection is null)
            return;

        var result = await ShowQuestionAsync("Delete", $"Do you really want to delete the connection '{SelectedConnection}'?", "Yes", "No");
        if (result != MessageDialogResult.Affirmative)
            return;

        try
        {
            await _connectionHistoryService.DeleteConnectionAsync(SelectedConnection, _cts.Token);

            Connections.Remove(SelectedConnection);
            SelectedConnection = null;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Save);
        }
    }

    /// <summary>
    /// Clears the current surface state by removing loaded tables and columns, resetting the prepared table, and creating a new empty generated code document.
    /// </summary>
    private void ResetSurface()
    {
        Tables.Clear();
        Columns.Clear();
        PreparedTable = null;
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
        if (SelectedConnection is null || _tablePreparationService is null)
            return;

        var controller = await ShowProgressAsync("Please wait", "Please wait while loading tables...", _cts);

        try
        {
            ResetSurface();
            
            var tables = await _tablePreparationService.LoadTablesAsync(SelectedConnection, _cts.Token);
            _tables = [.. tables];
            
            foreach (var table in tables)
                Tables.Add(table);
            
            ShowInfoText("Connection established.", InfoTextType.Success);
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
    /// Loads metadata for the selected table, updates the prepared table state, and refreshes the displayed column definitions.
    /// </summary>
    /// <return>
    /// A task that represents the asynchronous operation.
    /// </return>
    private async Task LoadSelectedTableAsync()
    {
        if (SelectedConnection is null || SelectedTable is null || _tablePreparationService is null)
            return;

        var controller = await ShowProgressAsync("Please wait", "Please wait while loading table metadata...", _cts);

        try
        {
            Columns.Clear();

            PreparedTable = await _tablePreparationService.LoadPreparedTableAsync(
                SelectedConnection,
                SelectedTable,
                _cts.Token);

            ClassName = PreparedTable.ClassName;
            UseEfCoreAttributes = PreparedTable.UseEfCoreAttributes;

            foreach (var property in PreparedTable.Properties)
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
        if (SelectedTable is null || _classGenerator is null)
            return;

        var selectedProperties = Columns
            .Where(column => column.IsIncluded)
            .Select(column => column.ToPropertyDefinition())
            .ToList();

        var options = new ClassGenerationOptions
        {
            NamespaceName = NamespaceName,
            SchemaName = SelectedTable.Schema,
            TableName = SelectedTable.Name,
            ClassName = ClassName,
            UseEfCoreAttributes = UseEfCoreAttributes,
            GenerateSummary = GenerateSummary,
            AccessorKind = SelectedAccessor?.Kind ?? AccessorKind.GetSet,
            Properties = selectedProperties
        };

        GeneratedCodeDocument = new TextDocument(_classGenerator.Generate(options));

        await SaveAliasesAsync();
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
    /// Saves the current column aliases and related generation settings for the selected connection and table.
    /// </summary>
    /// <return>
    /// A task that represents the asynchronous save operation.
    /// </return>
    [RelayCommand]
    private async Task SaveAliasesAsync()
    {
        if (SelectedConnection is null || SelectedTable is null || _aliasService is null)
            return;

        var properties = Columns
            .Select(column => column.ToPropertyDefinition())
            .ToList();

        try
        {
            await _aliasService.SaveAsync(
                SelectedConnection,
                SelectedTable,
                ClassName,
                UseEfCoreAttributes,
                properties,
                _cts.Token);
            
            ShowInfoText("Aliases saved.", InfoTextType.Success);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Save);
        }
    }
}