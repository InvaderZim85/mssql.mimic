using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Infrastructure.Sqlite;
using MsSql.Mimic.Infrastructure.Sqlite.Entities;

namespace MsSql.Mimic.Application.Services;

public sealed class ConnectionHistoryService(AliasDbContext context, PasswordProtector passwordProtector)
{
    public const int MaxHistoryCount = 10;

    public async Task<IReadOnlyList<ConnectionProfile>> LoadLastConnectionsAsync(CancellationToken ct = default)
    {
        var entities = await context.ConnectionHistory
            .AsNoTracking()
            .OrderByDescending(entry => entry.LastUsedAtUtc)
            .Take(MaxHistoryCount)
            .ToListAsync(ct);

        return entities
            .Select(ToProfile)
            .ToList();
    }

    public async Task SaveConnectionAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var normalizedServerName = Normalize(profile.Server);
        var normalizedDatabaseName = Normalize(profile.Database);
        var normalizedUserName = NormalizeNullable(profile.UserName);

        var entity = await context.ConnectionHistory
            .SingleOrDefaultAsync(entry => entry.Server == normalizedServerName &&
                                           entry.Database == normalizedDatabaseName &&
                                           entry.UseIntegratedSecurity == profile.UseIntegratedSecurity &&
                                           entry.UserName == normalizedUserName, ct);

        if (entity is null)
        {
            entity = new ConnectionHistoryEntity
            {
                Server = normalizedServerName,
                Database = normalizedDatabaseName,
                UseIntegratedSecurity = profile.UseIntegratedSecurity,
                UserName = normalizedUserName,
                ProtectedPassword = profile.UseIntegratedSecurity
                    ? null
                    : passwordProtector.Protect(profile.Password),
                TrustServerCertificate = profile.TrustServerCertificate,
                Encrypt = profile.Encrypt,
                ApplicationIntent = (profile.Intent == ConnectionIntent.ReadOnly ? ApplicationIntent.ReadOnly : ApplicationIntent.ReadWrite).ToString(),
                LastUsedAtUtc = DateTime.UtcNow
            };

            context.ConnectionHistory.Add(entity);
        }
        else
        {
            entity.TrustServerCertificate = profile.TrustServerCertificate;
            entity.Encrypt = profile.Encrypt;
            entity.ApplicationIntent = (profile.Intent == ConnectionIntent.ReadOnly ? ApplicationIntent.ReadOnly : ApplicationIntent.ReadWrite).ToString();
            entity.LastUsedAtUtc = DateTime.UtcNow;

            if (entity.UseIntegratedSecurity)
                entity.ProtectedPassword = null;
            else
                entity.ProtectedPassword = passwordProtector.Protect(profile.Password);
        }

        await context.SaveChangesAsync(ct);

        await TrimHistoryAsync(ct);
    }

    public async Task DeleteConnectionAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var normalizedServerName = Normalize(profile.Server);
        var normalizedDatabaseName = Normalize(profile.Database);
        var normalizedUserName = NormalizeNullable(profile.UserName);

        var entity = await context.ConnectionHistory
            .SingleOrDefaultAsync(entry => entry.Server == normalizedServerName &&
                                           entry.Database == normalizedDatabaseName &&
                                           entry.UseIntegratedSecurity == profile.UseIntegratedSecurity &&
                                           entry.UserName == normalizedUserName, ct);

        if (entity is null)
            return;

        context.ConnectionHistory.Remove(entity);

        await context.SaveChangesAsync(ct);
    }
    
    private async Task TrimHistoryAsync(CancellationToken ct)
    {
        var oldEntries = await context.ConnectionHistory
            .OrderByDescending(x => x.LastUsedAtUtc)
            .Skip(MaxHistoryCount)
            .ToListAsync(ct);

        if (oldEntries.Count == 0)
            return;

        context.ConnectionHistory.RemoveRange(oldEntries);

        await context.SaveChangesAsync(ct);
    }
    
    private ConnectionProfile ToProfile(ConnectionHistoryEntity entity)
    {
        return new ConnectionProfile
        {
            Server = entity.Server,
            Database = entity.Database,
            UseIntegratedSecurity = entity.UseIntegratedSecurity,
            UserName = entity.UserName,
            Password = entity.UseIntegratedSecurity
                ? null
                : passwordProtector.Unprotect(entity.ProtectedPassword),
            TrustServerCertificate = entity.TrustServerCertificate,
            Intent = Enum.Parse<ConnectionIntent>(entity.ApplicationIntent),
            Encrypt = entity.Encrypt
        };
    }
    
    private static string Normalize(string value)
    {
        return value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}