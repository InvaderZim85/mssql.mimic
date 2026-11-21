using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.VisualBasic.ApplicationServices;

using MsSql.Mimic.Domain.Dtos;
using MsSql.Mimic.Domain.Models;

namespace MsSql.Mimic.Ui.ViewModel;

/// <summary>
/// Represents the editable view model for SQL Server connection settings.
/// </summary>
/// <remarks>
/// Provides observable state used by the user interface to capture server, database,
/// authentication, encryption, certificate trust, and application intent options.
/// </remarks>
public sealed partial class ConnectionViewModel : ObservableObject
{
    /// <summary>
    /// Gets or sets the SQL Server instance name or network address used for the connection.
    /// </summary>
    /// <remarks>
    /// This value identifies the target server when creating, validating, or displaying a connection profile.
    /// </remarks>
    [ObservableProperty]
    public partial string Server { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the SQL Server database used for the connection.
    /// </summary>
    /// <remarks>
    /// This value identifies the target database on the configured server when creating or validating a connection profile.
    /// </remarks>
    [ObservableProperty]
    public partial string Database { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether Windows integrated security is used for the SQL Server connection.
    /// </summary>
    /// <remarks>
    /// When enabled, the connection uses the current Windows identity instead of explicit SQL Server credentials.
    /// </remarks>
    [ObservableProperty]
    public partial bool UseIntegratedSecurity { get; set; } = true;

    /// <summary>
    /// Gets or sets the username used for SQL Server authentication.
    /// </summary>
    /// <remarks>
    /// This value is used when integrated security is disabled and explicit credentials are provided.
    /// </remarks>
    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password used for SQL Server authentication.
    /// </summary>
    /// <remarks>
    /// This value is used when integrated security is disabled and credentials are supplied explicitly.
    /// </remarks>
    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the SQL Server certificate is trusted without validation.
    /// </summary>
    /// <remarks>
    /// When enabled, the server certificate is accepted even if it cannot be verified by a trusted certificate authority.
    /// </remarks>
    [ObservableProperty]
    public partial bool TrustServerCertificates { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether encryption is enabled for the SQL Server connection.
    /// </summary>
    /// <remarks>
    /// When enabled, the connection attempts to use encryption for data transmitted between the client and the server.
    /// </remarks>
    [ObservableProperty]
    public partial bool Encrypt { get; set; } = true;

    /// <summary>
    /// Gets or sets the selected SQL Server connection application intent.
    /// </summary>
    /// <remarks>
    /// The value identifies the desired connection intent, such as read-write or read-only, and is stored as an identifier/value pair that maps to a connection intent option.
    /// </remarks>
    [ObservableProperty]
    public partial IdValueDto ApplicationIntent { get; set; } = IdValueDto.Empty();

    /// <summary>
    /// Creates a connection data transfer object from the specified connection profile.
    /// </summary>
    /// <param name="profile">The connection profile containing the connection settings to copy.</param>
    /// <returns>A connection data transfer object populated with values from the specified profile.</returns>
    public static ConnectionViewModel FromProfile(ConnectionProfile profile)
    {
        return new ConnectionViewModel
        {
            Server = profile.Server,
            Database = profile.Database,
            UseIntegratedSecurity = profile.UseIntegratedSecurity,
            UserName = profile.UserName ?? string.Empty,
            Password = profile.Password ?? string.Empty,
            TrustServerCertificates = profile.TrustServerCertificate,
            Encrypt = profile.Encrypt,
            ApplicationIntent = new IdValueDto((int)profile.Intent, profile.Intent.ToString())
        };
    }

    /// <summary>
    /// Creates a connection profile from the current connection view model values.
    /// </summary>
    /// <returns>A connection profile populated with the current connection settings.</returns>
    public ConnectionProfile ToProfile()
    {
        return new ConnectionProfile
        {
            Server = Server,
            Database = Database,
            UseIntegratedSecurity = UseIntegratedSecurity,
            UserName = string.IsNullOrWhiteSpace(UserName) ? null : UserName,
            Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
            TrustServerCertificate = TrustServerCertificates,
            Encrypt = Encrypt,
            Intent = (ConnectionIntent)ApplicationIntent.Id
        };
    }

    /// <summary>
    /// Creates a new connection profile initialized with placeholder server and database values.
    /// </summary>
    /// <return>A connection profile with default connection settings and placeholder values for required fields.</return>
    public static ConnectionViewModel NewConnection()
    {
        return new ConnectionViewModel
        {
            Server = "<Enter server>",
            Database = "<Enter database>"
        };
    }
}