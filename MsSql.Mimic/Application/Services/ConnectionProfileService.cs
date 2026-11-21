using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Infrastructure.Sqlite;
using MsSql.Mimic.Infrastructure.Sqlite.Entities;

namespace MsSql.Mimic.Application.Services;

/// <summary>
/// Provides operations for loading, saving, and deleting persisted database connection profiles.
/// </summary>
/// <remarks>
/// This service uses the application database context to store connection profile metadata and
/// a password protector to handle stored credentials. Saved profiles are normalized before
/// persistence so that equivalent connection settings can be matched consistently.
/// </remarks>
/// <param name="context">
/// The database context used to query and persist connection profile records.
/// </param>
/// <param name="passwordProtector">
/// The service used to protect and unprotect stored connection profile passwords.
/// </param>
public sealed class ConnectionProfileService(AliasDbContext context, PasswordProtector passwordProtector)
{
    /// <summary>
    /// Loads the saved connection profiles ordered by most recent use.
    /// </summary>
    /// <param name="ct">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous load operation. The task result contains the loaded connection profiles.</returns>
    public async Task<IReadOnlyList<ConnectionProfile>> LoadConnectionsAsync(CancellationToken ct = default)
    {
        var entities = await context.Connections
            .AsNoTracking()
            .OrderByDescending(entry => entry.LastUsedAtUtc)
            .ToListAsync(ct);

        return [.. entities.Select(ToProfile)];
    }

    /// <summary>
    /// Saves the specified connection profile, creating a new saved profile or updating an existing matching profile.
    /// </summary>
    /// <param name="profile">The connection profile to save.</param>
    /// <param name="ct">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SaveConnectionAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var normalizedServerName = Normalize(profile.Server);
        var normalizedDatabaseName = Normalize(profile.Database);
        var normalizedUserName = NormalizeNullable(profile.UserName);

        var entity = await LoadEntityAsync(profile, ct);

        if (entity is null)
        {
            entity = new ConnectionEntity
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

            context.Connections.Add(entity);
        }
        else
        {
            entity.TrustServerCertificate = profile.TrustServerCertificate;
            entity.Encrypt = profile.Encrypt;
            entity.ApplicationIntent = (profile.Intent == ConnectionIntent.ReadOnly ? ApplicationIntent.ReadOnly : ApplicationIntent.ReadWrite).ToString();
            entity.LastUsedAtUtc = DateTime.UtcNow;

            entity.ProtectedPassword = entity.UseIntegratedSecurity 
                ? null 
                : passwordProtector.Protect(profile.Password);
        }

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes the persisted connection history entry that matches the specified connection profile, if one exists.
    /// </summary>
    /// <param name="profile">The connection profile identifying the connection history entry to delete.</param>
    /// <param name="ct">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteConnectionAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var entity = await LoadEntityAsync(profile, ct);

        if (entity is null)
            return;

        context.Connections.Remove(entity);

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Updates the last-used timestamp for the specified connection profile.
    /// </summary>
    /// <param name="profile">The connection profile whose last-used timestamp should be updated.</param>
    /// <param name="ct">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateLastUsedDateAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var entity = await LoadEntityAsync(profile, ct);

        if (entity is null)
            return;

        entity.LastUsedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
    }
    
    /// <summary>
    /// Loads the stored connection entity that matches the specified connection profile.
    /// </summary>
    /// <param name="profile">The connection profile whose server, database, authentication mode, and user name are used to find the stored entity.</param>
    /// <param name="ct">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous load operation. The task result contains the matching connection entity, or null if no matching entity exists.</returns>
    private async Task<ConnectionEntity?> LoadEntityAsync(ConnectionProfile profile, CancellationToken ct)
    {
        var normalizedServerName = Normalize(profile.Server);
        var normalizedDatabaseName = Normalize(profile.Database);
        var normalizedUserName = NormalizeNullable(profile.UserName);

        return await context.Connections
            .SingleOrDefaultAsync(entry => entry.Server == normalizedServerName &&
                                           entry.Database == normalizedDatabaseName &&
                                           entry.UseIntegratedSecurity == profile.UseIntegratedSecurity &&
                                           entry.UserName == normalizedUserName, ct);
    }

    /// <summary>
    /// Converts a persisted connection entity into a connection profile.
    /// </summary>
    /// <param name="entity">The connection entity to convert.</param>
    /// <returns>The connection profile created from the entity.</returns>
    private ConnectionProfile ToProfile(ConnectionEntity entity)
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
            Encrypt = entity.Encrypt,
            LastUsed = entity.LastUsedAtUtc.ToLocalTime()
        };
    }

    /// <summary>
    /// Normalizes a string by trimming surrounding whitespace.
    /// </summary>
    /// <param name="value">The string value to normalize.</param>
    /// <returns>The trimmed string.</returns>
    private static string Normalize(string value)
    {
        return value.Trim();
    }

    /// <summary>
    /// Normalizes a nullable string by trimming surrounding whitespace and converting null, empty, or whitespace-only values to null.
    /// </summary>
    /// <param name="value">The nullable string value to normalize.</param>
    /// <returns>The trimmed string when the value contains non-whitespace characters; otherwise, null.</returns>
    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    
}