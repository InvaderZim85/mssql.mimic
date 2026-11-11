namespace MsSql.Mimic.Domain.Models;

public sealed record ConnectionProfile
{
    public required string Server { get; init; }
    public required string Database { get; init; }
    public bool UseIntegratedSecurity { get; init; } = true;
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public bool TrustServerCertificate { get; init; } = true;
    public bool Encrypt { get; init; } = true;
    public ConnectionIntent Intent { get; init; } = ConnectionIntent.ReadOnly;

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
            ? Environment.UserName
            : UserName;

        return $"{Server} / {Database} ({Intent}, {auth})";
    }
}