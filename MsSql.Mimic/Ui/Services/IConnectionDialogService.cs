namespace MsSql.Mimic.Ui.Services;

/// <summary>
/// Provides a service for displaying a connection dialog and obtaining the user's connection selection.
/// </summary>
public interface IConnectionDialogService
{
    /// <summary>
    /// Shows the connection manager dialog and returns the user's selection and connection request state.
    /// </summary>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the selected connection, if a connection was requested, and whether the user requested a connection.</returns>
    Task<ConnectionManagerResult> ShowConnectionManagerAsync(CancellationToken ct = default);
}