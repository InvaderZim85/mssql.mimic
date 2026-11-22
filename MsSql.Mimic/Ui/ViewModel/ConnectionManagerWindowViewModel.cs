using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MahApps.Metro.Controls.Dialogs;

using MsSql.Mimic.Application.Services;
using MsSql.Mimic.Domain.Dtos;
using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Infrastructure.MsSql;

using System.Collections.ObjectModel;

namespace MsSql.Mimic.Ui.ViewModel;

/// <summary>
/// Provides the view model for the connection manager window.
/// </summary>
/// <remarks>
/// Manages the observable state used to display, edit, save, select, and initialize SQL connection profiles.
/// Coordinates connection profile data with connection-related services and exposes notifications for connection selection and window close requests.
/// </remarks>
/// <param name="connectionProfileService">The service used to load, save, and delete connection profiles.</param>
/// <param name="connectionFactory">The factory used to create SQL Server connections.</param>
public sealed partial class ConnectionManagerWindowViewModel(
    ConnectionProfileService connectionProfileService,
    SqlConnectionFactory connectionFactory) : BaseViewModel
{
    /// <summary>
    /// Occurs when a connection profile is selected for connection.
    /// </summary>
    public event EventHandler<ConnectionProfile?>? ConnectionSelected;

    /// <summary>
    /// Occurs when the connection manager view model requests that the window be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Provides cancellation signaling for asynchronous operations managed by the connection manager view model.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Stores the selected connection profile as it existed before editing, enabling detection of key changes and distinguishing updates from new connections.
    /// </summary>
    private ConnectionProfile? _originalConnection;

    // ReSharper disable once PropertyCanBeMadeInitOnly.Local
    // ReSharper disable once UnusedMember.Local
    /// <summary>
    /// Gets the collection of saved connection profiles available for selection or management.
    /// </summary>
    /// <value>
    /// An <see cref="ObservableCollection{T}"/> of <see cref="ConnectionProfile"/> values representing configured database connections.
    /// </value>
    [ObservableProperty]
    public partial ObservableCollection<ConnectionProfile> Connections { get; private set; } = [];

    // ReSharper disable once PropertyCanBeMadeInitOnly.Local
    // ReSharper disable once UnusedMember.Local
    /// <summary>
    /// Gets the available application intent options for configuring a connection.
    /// </summary>
    /// <value>
    /// An <see cref="ObservableCollection{T}"/> of <see cref="IdValueDto"/> values representing the supported application intents.
    /// </value>
    [ObservableProperty]
    public partial ObservableCollection<IdValueDto> ApplicationIntents { get; private set; } = [];

    /// <summary>
    /// Gets or sets the currently selected connection profile.
    /// </summary>
    /// <value>
    /// A <see cref="ConnectionProfile"/> representing the selected connection, or <see langword="null"/> when no connection is selected.
    /// </value>
    [ObservableProperty]
    public partial ConnectionProfile? SelectedConnection { get; set; }

    /// <summary>
    /// Gets or sets the connection details currently being edited.
    /// </summary>
    /// <value>
    /// A <see cref="ConnectionViewModel"/> containing the editable connection settings.
    /// </value>
    [ObservableProperty]
    public partial ConnectionViewModel EditedConnection { get; private set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the Save button is enabled.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the current connection can be saved; otherwise, <see langword="false"/>.
    /// </value>
    [ObservableProperty]
    public partial bool ButtonSaveEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the connect button is enabled.
    /// </summary>
    [ObservableProperty]
    public partial bool ButtonConnectEnabled { get; set; } = false;

    /// <summary>
    /// Handles changes to the selected connection.
    /// </summary>
    /// <remarks>
    /// Updates the original connection reference and populates the editable connection view model from the selected connection when a non-null value is provided.
    /// </remarks>
    /// <param name="value">The newly selected connection profile, or null when no connection is selected.</param>
    partial void OnSelectedConnectionChanged(ConnectionProfile? value)
    {
        if (value is null)
            return;

        _originalConnection = value;
        EditedConnection = ConnectionViewModel.FromProfile(value);
        ButtonConnectEnabled = value.LastUsed != default; // Check if the last use data has it's default
    }

    /// <summary>
    /// Initializes the connection manager view model.
    /// </summary>
    /// <remarks>
    /// Clears and populates the available application intents, loads saved connections, and assigns the first available application intent to the edited connection when no intent is currently selected.
    /// </remarks>
    /// <param name="ct">A cancellation token used to cancel the asynchronous initialization operation.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public async Task InitViewModelAsync(CancellationToken ct = default)
    {
        ApplicationIntents.Clear();

        foreach (var intent in Enum.GetValues<ConnectionIntent>())
            ApplicationIntents.Add(new IdValueDto((int)intent, intent.ToString()));
        
        // Load connections
        await LoadConnectionsAsync(ct);
        
        // If no intent is selected, set the first one
        if (ApplicationIntents.Count > 0 && EditedConnection.ApplicationIntent == IdValueDto.Empty())
            EditedConnection.ApplicationIntent = ApplicationIntents[0];
    }

    /// <summary>
    /// Loads saved connection profiles into the connection collection.
    /// </summary>
    /// <remarks>
    /// Returns without loading when no connection profile service is available. Clears the existing connection collection before adding loaded profiles, and reports load errors through the view model error handling.
    /// </remarks>
    /// <param name="ct">A cancellation token used to cancel the asynchronous load operation.</param>
    /// <returns>A task that represents the asynchronous load operation.</returns>
    private async Task LoadConnectionsAsync(CancellationToken ct = default)
    {
        try
        {
            Connections.Clear();

            var connections = await connectionProfileService.LoadConnectionsAsync(ct);

            foreach (var connection in connections)
                Connections.Add(connection);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Load);
        }
    }

    /// <summary>
    /// Starts creation of a new connection profile.
    /// </summary>
    /// <remarks>
    /// Clears the currently selected and original connection, replaces the edited connection with a new connection view model, and assigns the first available application intent when one is configured.
    /// </remarks>
    [RelayCommand]
    private void NewConnection()
    {
        SelectedConnection = null;
        _originalConnection = null;

        ButtonSaveEnabled = false;
        ButtonConnectEnabled = false;
        
        EditedConnection = ConnectionViewModel.NewConnection();

        if (ApplicationIntents.Count > 0)
            EditedConnection.ApplicationIntent = ApplicationIntents[0];
    }

    /// <summary>
    /// Tests whether the edited connection profile can establish a SQL Server connection.
    /// </summary>
    /// <remarks>
    /// Converts the edited connection values to a connection profile and attempts to connect using the configured connection factory. Shows a success message and enables saving when the connection succeeds. Shows a failure message with the connection error details when the connection cannot be established. If no connection factory is available, the operation completes without performing a test.
    /// </remarks>
    /// <return>
    /// A task that represents the asynchronous connection test operation.
    /// </return>
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        var controller = await ShowProgressAsync("Connection", "Please wait while an attempt is made to connect to the database.", _cts);
        
        var profile = EditedConnection.ToProfile();
        var result = await connectionFactory.CanConnectAsync(profile, _cts.Token);

        await controller.CloseAsync();
        
        if (result.Success)
        {
            ButtonSaveEnabled = true;
            ButtonConnectEnabled = true;

            await ShowMessageAsync(
                "Connection",
                $"Connection to '{profile.Server}' / '{profile.Database}' successfully established.");
        }
        else
        {
            await ShowMessageAsync(
                "Connection failed",
                $"""
                 Can't establish connection to server '{profile.Server}' / '{profile.Database}'.
                 
                 Message:
                 {result.ErrorMessage}
                 """);
        }
    }

    /// <summary>
    /// Saves the currently edited connection profile.
    /// </summary>
    /// <remarks>
    /// Converts the edited connection to a profile, updates persistent storage, replaces or adds the profile in the displayed collection, refreshes the selected and original connection state, and shows a success message. Shows an error message if the save operation fails.
    /// </remarks>
    /// <return>
    /// A task that represents the asynchronous save operation.
    /// </return>
    [RelayCommand]
    private async Task SaveConnectionAsync()
    {
        var updatedConnection = EditedConnection.ToProfile();

        try
        {
            // Check if the connection already exists
            if (ConnectionAlreadyExists(updatedConnection))
            {
                ShowInfoText("Connection already exists.", InfoTextType.Failure);
                return;
            }
            
            // Check if we should delete the old connection and create a new one
            if (_originalConnection is not null && _originalConnection.IsConnectionKeyChanged(updatedConnection))
            {
                await connectionProfileService.DeleteConnectionAsync(_originalConnection);
                Connections.Remove(_originalConnection);
            }

            await connectionProfileService.SaveConnectionAsync(updatedConnection);

            if (_originalConnection is not null)
            {
                var index = Connections.IndexOf(_originalConnection);

                if (index >= 0)
                    Connections[index] = updatedConnection;
                else
                    Connections.Add(updatedConnection);
            }
            else
                Connections.Add(updatedConnection);

            SelectedConnection = updatedConnection;
            _originalConnection = updatedConnection;
            
            ShowInfoText("Connection saved.", InfoTextType.Success);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Save);
        }
    }

    /// <summary>
    /// Deletes the currently selected connection after user confirmation.
    /// </summary>
    /// <remarks>
    /// Removes the connection from persistent storage and the displayed collection, then clears the current selection and edited connection state. Shows an error message if deletion fails.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous delete operation.
    /// </returns>
    [RelayCommand]
    private async Task DeleteConnectionAsync()
    {
        if (SelectedConnection is null)
            return;

        var result = await ShowQuestionAsync(
            "Delete connection",
            $"Do you really want to delete the connection '{SelectedConnection}'?",
            "Yes",
            "No");

        if (result != MessageDialogResult.Affirmative)
            return;

        try
        {
            await connectionProfileService.DeleteConnectionAsync(SelectedConnection);

            Connections.Remove(SelectedConnection);

            SelectedConnection = null;
            _originalConnection = null;
            EditedConnection = new ConnectionViewModel();

            if (ApplicationIntents.Count > 0)
                EditedConnection.ApplicationIntent = ApplicationIntents[0];
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Delete);
        }
    }

    /// <summary>
    /// Determines whether a matching connection profile already exists.
    /// </summary>
    /// <remarks>
    /// Compares the supplied connection against existing connections while excluding the original connection being edited. Server, database, and username comparisons are case-insensitive.
    /// </remarks>
    /// <param name="updatedConnection">The connection profile to check for duplication.</param>
    /// <returns>True if another connection with the same server, database, authentication mode, and username exists; otherwise, false.</returns>
    private bool ConnectionAlreadyExists(ConnectionProfile updatedConnection)
    {
        return Connections.Any(existing =>
            !ReferenceEquals(existing, _originalConnection) &&
            existing.Server.Equals(updatedConnection.Server, StringComparison.OrdinalIgnoreCase) &&
            existing.Database.Equals(updatedConnection.Database, StringComparison.OrdinalIgnoreCase) &&
            existing.UseIntegratedSecurity == updatedConnection.UseIntegratedSecurity &&
            string.Equals(existing.UserName, updatedConnection.UserName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Connects using the currently edited connection settings.
    /// </summary>
    /// <remarks>
    /// Converts the edited connection details to a connection profile and raises the ConnectionSelected event.
    /// </remarks>
    [RelayCommand]
    private void Connect()
    {
        var connection = EditedConnection.ToProfile();
        ConnectionSelected?.Invoke(this, connection);
    }

    /// <summary>
    /// Requests that the connection manager window be closed.
    /// </summary>
    /// <remarks>
    /// Raises the <c>CloseRequested</c> event to notify subscribers that the view should close.
    /// </remarks>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}