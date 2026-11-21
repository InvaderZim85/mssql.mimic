namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Represents a SQL Server connection profile that identifies a target server and database and defines the connection authentication, encryption, certificate trust, and access intent settings.
/// </summary>
/// <remarks>
/// Instances are immutable record values intended for passing connection details between services that select connections, read database schema metadata, and persist connection-specific configuration.
/// The connection key is determined by the server, database, integrated security setting, and user name.
/// </remarks>
public sealed record ConnectionProfile
{
    /// <summary>
    /// Gets the SQL Server instance or data source targeted by the connection profile.
    /// </summary>
    /// <value>
    /// The server name, instance name, or network address used when establishing the connection.
    /// </value>
    public required string Server { get; init; }

    /// <summary>
    /// Gets the name of the SQL Server database targeted by the connection profile.
    /// </summary>
    /// <value>
    /// The database name used when establishing the connection.
    /// </value>
    public required string Database { get; init; }

    /// <summary>
    /// Gets a value indicating whether the SQL Server connection uses integrated security for authentication.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to authenticate with the current Windows credentials; <see langword="false"/> to use SQL Server authentication credentials.
    /// </value>
    public bool UseIntegratedSecurity { get; init; } = true;

    /// <summary>
    /// Gets the username used to authenticate the SQL Server connection when integrated security is not used.
    /// </summary>
    /// <value>
    /// The SQL Server authentication username, or <see langword="null"/> when integrated security is used or no username is specified.
    /// </value>
    public string? UserName { get; init; }

    /// <summary>
    /// Gets the password used to authenticate the SQL Server connection when integrated security is not used.
    /// </summary>
    /// <value>
    /// The connection password, or <see langword="null"/> when no password is specified or password authentication is not required.
    /// </value>
    public string? Password { get; init; }

    /// <summary>
    /// Gets a value indicating whether the SQL Server certificate is trusted without validation.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to trust the server certificate; otherwise, <see langword="false"/> to require certificate validation. The default value is <see langword="true"/>.
    /// </value>
    public bool TrustServerCertificate { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether encryption is enabled for the SQL Server connection.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to require an encrypted connection; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.
    /// </value>
    public bool Encrypt { get; init; } = true;

    /// <summary>
    /// Gets the date and time when the connection profile was last used.
    /// </summary>
    /// <value>
    /// The timestamp representing the most recent use of the connection profile.
    /// </value>
    public DateTime LastUsed { get; init; }

    /// <summary>
    /// Gets the intended SQL Server connection workload, indicating whether the connection is for read-only or read-write operations.
    /// </summary>
    /// <value>
    /// The connection intent to apply when creating the connection. The default value is <see cref="ConnectionIntent.ReadOnly"/>.
    /// </value>
    public ConnectionIntent Intent { get; init; } = ConnectionIntent.ReadOnly;

    /// <summary>
    /// Determines whether the connection key fields differ from the specified connection profile.
    /// </summary>
    /// <param name="newConnection">The connection profile to compare with the current profile.</param>
    /// <return>true if the server, database, integrated security setting, or username has changed; otherwise, false.</return>
    public bool IsConnectionKeyChanged(ConnectionProfile newConnection)
    {
        return !string.Equals(Server, newConnection.Server, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(Database, newConnection.Database, StringComparison.OrdinalIgnoreCase) ||
               UseIntegratedSecurity != newConnection.UseIntegratedSecurity ||
               !string.Equals(UserName, newConnection.UserName, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <inheritdoc />
    public override string ToString()
    {
        var auth = UseIntegratedSecurity
            ? string.Empty
            : " - C";

        return $"{Server} / {Database} ({Intent}{auth})";
    }
}