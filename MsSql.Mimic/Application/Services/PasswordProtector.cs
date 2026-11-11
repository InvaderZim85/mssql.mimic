using System.Security.Cryptography;
using System.Text;

namespace MsSql.Mimic.Application.Services;

public sealed class PasswordProtector
{
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