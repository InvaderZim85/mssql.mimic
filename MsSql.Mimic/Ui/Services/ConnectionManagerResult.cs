using MsSql.Mimic.Domain.Models;

namespace MsSql.Mimic.Ui.Services;

/// <summary>
/// Represents the result of displaying the connection manager dialog, including whether a connection was requested and the selected connection profile when applicable.
/// </summary>
public sealed class ConnectionManagerResult
{
    /// <summary>
    /// Gets the connection profile selected in the connection manager.
    /// </summary>
    /// <value>
    /// The selected connection profile, or <see langword="null" /> if no connection was selected or a connection was not requested.
    /// </value>
    public ConnectionProfile? SelectedConnection { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user requested a connection from the connection manager.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if a connection was requested; otherwise, <see langword="false" />.
    /// </value>
    public bool ConnectRequested { get; init; }
}