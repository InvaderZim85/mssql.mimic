using System.ComponentModel.DataAnnotations;

namespace MsSql.Mimic.Infrastructure.Sqlite.Entities;

/// <summary>
/// Represents a persisted SQL Server connection profile stored by the SQLite-backed alias infrastructure.
/// </summary>
internal sealed class ConnectionEntity
{
    /// <summary>
    /// Gets or sets the primary key that uniquely identifies the connection history record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the SQL Server name or instance associated with the connection history record.
    /// </summary>
    [MaxLength(256)]
    [Required]
    public required string Server { get; set; }

    /// <summary>
    /// Gets or sets the normalized database name associated with the stored connection profile.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string Database { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connection uses integrated security for authentication.
    /// </summary>
    public bool UseIntegratedSecurity { get; set; }

    /// <summary>
    /// Gets or sets the username associated with the connection, or null when no username is specified.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the protected password stored for the connection when SQL authentication is used.
    /// </summary>
    public string? ProtectedPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the server certificate is trusted without validation when establishing the connection.
    /// </summary>
    public bool TrustServerCertificate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether encryption is enabled for the connection.
    /// </summary>
    public bool Encrypt { get; set; }

    /// <summary>
    /// Gets or sets the application intent used when connecting to the database.
    /// </summary>
    [Required]
    [MaxLength(32)]
    public required string ApplicationIntent { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the connection was most recently used.
    /// </summary>
    public DateTime LastUsedAtUtc { get; set; }
}