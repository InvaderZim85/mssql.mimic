using Microsoft.Data.SqlClient;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;

using System.Data;

namespace MsSql.Mimic.Infrastructure.MsSql;

public sealed class SqlConnectionFactory
{
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

    public async Task<ConnectionTestResult> CanConnect(ConnectionProfile profile, CancellationToken ct = default)
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
    }
}