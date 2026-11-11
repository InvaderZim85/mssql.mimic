namespace MsSql.Mimic.Infrastructure.Sqlite.Entities;

internal sealed class ConnectionHistoryEntity
{
    public int Id { get; set; }
    public required string Server { get; set; }
    public required string Database { get; set; }
    public bool UseIntegratedSecurity { get; set; }
    public string? UserName { get; set; }
    public string? ProtectedPassword { get; set; }
    public bool TrustServerCertificate { get; set; }
    public bool Encrypt { get; set; }
    public required string ApplicationIntent { get; set; }
    public DateTime LastUsedAtUtc { get; set; }
}