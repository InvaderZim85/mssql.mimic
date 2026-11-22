namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Defines the intended workload type for a SQL Server connection.
/// </summary>
public enum ConnectionIntent
{
    /// <summary>
    /// Specifies that the connection is intended for read-only operations.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Indicates that the connection is intended for read and write operations.
    /// </summary>
    ReadWrite
}