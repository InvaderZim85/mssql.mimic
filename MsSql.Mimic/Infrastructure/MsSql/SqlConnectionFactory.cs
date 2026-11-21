using Microsoft.Data.SqlClient;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;

using System.Data;

namespace MsSql.Mimic.Infrastructure.MsSql;

/// <summary>
/// Provides factory methods for creating and validating SQL Server database connections from connection profile settings.
/// </summary>
/// <remarks>
/// Builds SQL Server connection strings from the supplied profile, including server, database, authentication,
/// encryption, and certificate trust options. Use this type when application services need a configured
/// database connection or need to verify whether a profile can connect successfully.
/// </remarks>
public sealed class SqlConnectionFactory
{
    /// <summary>
    /// Creates a SQL Server database connection using the settings from the specified connection profile.
    /// </summary>
    /// <param name="profile">The connection profile containing the server, database, authentication, and encryption settings.</param>
    /// <returns>A database connection configured with the connection string built from the specified profile.</returns>
    public IDbConnection CreateConnection(ConnectionProfile profile)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = profile.Server,
            InitialCatalog = profile.Database,
            Encrypt = profile.Encrypt,
            TrustServerCertificate = profile.TrustServerCertificate,
            MultipleActiveResultSets = false
        };

        if (profile.UseIntegratedSecurity)
            builder.IntegratedSecurity = true;
        else
        {
            builder.IntegratedSecurity = false;
            builder.UserID = profile.UserName;
            builder.Password = profile.Password;
        }

        return new SqlConnection(builder.ConnectionString);
    }

    /// <summary>
    /// Tests whether a connection can be established using the specified connection profile.
    /// </summary>
    /// <param name="profile">The connection profile containing the SQL Server connection settings to test.</param>
    /// <param name="ct">A cancellation token used to cancel the connection attempt.</param>
    /// <returns>A result indicating whether the connection attempt succeeded, including an error message when it fails.</returns>
    public async Task<ConnectionTestResult> CanConnectAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        await using var connection = (SqlConnection)CreateConnection(profile);

        try
        {
            await connection.OpenAsync(ct);

            return new ConnectionTestResult(true);
        }
        catch (SqlException ex)
        {
            return new ConnectionTestResult(false, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return new ConnectionTestResult(false, "Operation cancelled by the user.");
        }
    }
}