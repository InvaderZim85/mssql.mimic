using MahApps.Metro.Controls;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Ui.ViewModel;

namespace MsSql.Mimic.Ui.View;

/// <summary>
/// Represents the modal window used to manage database connection profiles and initiate a connection request.
/// </summary>
/// <remarks>
/// This window is backed by a connection manager view model, initializes connection state when loaded,
/// and translates view model selection or close notifications into dialog results for the calling service.
/// </remarks>
public partial class ConnectionManagerWindow : MetroWindow
{
    /// <summary>
    /// Gets the connection profile selected in the connection manager window.
    /// </summary>
    /// <value>
    /// The selected <see cref="ConnectionProfile" />, or <see langword="null" /> if no connection has been selected.
    /// </value>
    public ConnectionProfile? SelectedConnection { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the connection manager was closed because the user requested a connection.
    /// </summary>
    /// <value>
    /// <see langword="true" /> when a connection was selected and a connection was requested; otherwise, <see langword="false" />.
    /// </value>
    public bool ConnectRequested { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionManagerWindow"/> class using the specified view model.
    /// </summary>
    /// <param name="viewModel">The view model that provides connection profile state, initialization logic, and window action notifications.</param>
    /// <remarks>
    /// Sets the window data context, initializes the view model when the window is loaded, and maps view model selection and close notifications to the window dialog result.
    /// </remarks>
    public ConnectionManagerWindow(ConnectionManagerWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.InitViewModelAsync();

        viewModel.ConnectionSelected += (_, connection) =>
        {
            SelectedConnection = connection;
            ConnectRequested = true;
            DialogResult = true;
        };

        viewModel.CloseRequested += (_, _) =>
        {
            ConnectRequested = false;
            DialogResult = false;
        };
    }
}