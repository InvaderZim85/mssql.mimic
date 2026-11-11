using Microsoft.Extensions.DependencyInjection;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Ui.ViewModel;

namespace MsSql.Mimic.Ui.Services;

public sealed class ConnectionDialogService(IServiceProvider serviceProvider) : IConnectionDialogService
{
    /// <inheritdoc />
    public Task<ConnectionProfile?> ShowNewConnectionDialogAsync(CancellationToken ct = default)
    {
        var window = serviceProvider.GetRequiredService<ConnectionWindow>();

        var result = window.ShowDialog();

        if (result != true || window.DataContext is not ConnectionWindowViewModel viewModel)
            return Task.FromResult<ConnectionProfile?>(null);

        var connectionProfile = viewModel.Connection.ToProfile();
        return Task.FromResult<ConnectionProfile?>(connectionProfile);
    }

    /// <inheritdoc />
    public Task<ConnectionProfile?> ShowEditConnectionDialogAsync(ConnectionProfile connectionProfile, CancellationToken ct = default)
    {
        var window = serviceProvider.GetRequiredService<ConnectionWindow>();

        if (window.DataContext is ConnectionWindowViewModel viewModel)
            viewModel.LoadConnection(connectionProfile);

        var result = window.ShowDialog();

        if (result != true || window.DataContext is not ConnectionWindowViewModel updatedViewModel)
            return Task.FromResult<ConnectionProfile?>(null);

        return Task.FromResult<ConnectionProfile?>(updatedViewModel.Connection.ToProfile());
    }
}