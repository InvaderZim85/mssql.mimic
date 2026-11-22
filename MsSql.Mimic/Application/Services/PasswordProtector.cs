using System.Security.Cryptography;
using System.Text;

namespace MsSql.Mimic.Application.Services;

/// <summary>
/// Provides password protection services for encrypting and decrypting password values for the current user.
/// </summary>
/// <remarks>
/// Uses the operating system data protection API with current-user scope. Protected passwords are returned as Base64-encoded strings and are intended to be decrypted only within the same user context. Null, empty, or whitespace input is treated as an absent password and results in null.
/// </remarks>
public sealed class PasswordProtector
{
    /// <summary>
    /// Encrypts a password string for the current user.
    /// </summary>
    /// <param name="password">The password to encrypt.</param>
    /// <returns>The Base64-encoded protected password, or <c>null</c> if <paramref name="password"/> is null, empty, or whitespace.</returns>
    public string? Protect(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return null;

        var bytes = Encoding.UTF8.GetBytes(password);

        var protectedBytes = ProtectedData.Protect(
            bytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// Decrypts a protected password string for the current user.
    /// </summary>
    /// <param name="protectedPassword">The Base64-encoded protected password to decrypt.</param>
    /// <returns>The decrypted password, or <c>null</c> if <paramref name="protectedPassword"/> is null, empty, or whitespace.</returns>
    public string? Unprotect(string? protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
            return null;

        var protectedBytes = Convert.FromBase64String(protectedPassword);

        var bytes = ProtectedData.Unprotect(
            protectedBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(bytes);
    }
}