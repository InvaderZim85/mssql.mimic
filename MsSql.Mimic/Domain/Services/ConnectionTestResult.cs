namespace MsSql.Mimic.Domain.Services;

/// <summary>
/// Represents the outcome of an attempt to connect to a SQL Server database.
/// </summary>
/// <param name="Success">
/// Indicates whether the connection attempt completed successfully.
/// </param>
/// <param name="ErrorMessage">
/// Contains the error message returned when the connection attempt fails; otherwise, an empty string.
/// </param>
public sealed record ConnectionTestResult(bool Success, string ErrorMessage = "");