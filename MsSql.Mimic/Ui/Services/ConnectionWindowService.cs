using Microsoft.Extensions.DependencyInjection;

using MsSql.Mimic.Ui.View;

namespace MsSql.Mimic.Ui.Services;

/// <summary>
/// Provides the WPF-based implementation of connection dialog operations for creating or editing connection profiles.
/// </summary>
/// <param name="serviceProvider">
/// The service provider used to resolve connection dialog windows and their associated dependencies.
/// </param>
/// <remarks>
/// The service displays a connection window and converts the confirmed dialog state into a connection profile. If the dialog is cancelled, closed without confirmation, or does not expose the expected view model, the operation returns a null result.
/// </remarks>
public sealed class ConnectionWindowService(IServiceProvider serviceProvider) : IConnectionDialogService
{
    /// <inheritdoc/>
    public Task<ConnectionManagerResult> ShowConnectionManagerAsync(CancellationToken ct = default)
    {
        var window = serviceProvider.GetRequiredService<ConnectionManagerWindow>();

        // Get the "main window" which is our "owner"
        var owner = System.Windows.Application.Current.MainWindow;
        // Set the owner
        if (owner is not null && owner != window)
            window.Owner = owner;

        // Show the window
        window.ShowDialog();
        
        // Create the result
        var result = new ConnectionManagerResult
        {
            SelectedConnection = window.ConnectRequested
                ? window.SelectedConnection
                : null,
            ConnectRequested = window.ConnectRequested
        };

        return Task.FromResult(result);
    }
}