namespace MsSql.Mimic.Domain.Models;

public interface IConnectionDialogService
{
    Task<ConnectionProfile?> ShowNewConnectionDialogAsync(CancellationToken ct = default);
    Task<ConnectionProfile?> ShowEditConnectionDialogAsync(ConnectionProfile connectionProfile, CancellationToken ct = default);
}