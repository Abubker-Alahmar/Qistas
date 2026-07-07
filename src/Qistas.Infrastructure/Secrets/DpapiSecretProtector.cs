using System.Security.Cryptography;
using System.Text;
using Qistas.Application.Abstractions;

namespace Qistas.Infrastructure.Secrets;

/// <summary>
/// Windows DPAPI-backed secret protector (CurrentUser scope) for the D365 client_secret.
/// Deliberately NOT Balance's ClsCrypto (MD5-derived key + static IV -- see
/// Balance/CLAUDE.md section 6.6 and AGENT_INSTRUCTION.md section 7). Only constructed
/// when <see cref="OperatingSystem.IsWindows"/> is true; see
/// <see cref="NoOpSecretProtector"/> for the non-Windows dev fallback.
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Qistas.D365.ClientSecret.v1");

    public string Protect(string plainText)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] protectedBytes = ProtectedDataWindows(plainBytes, encrypt: true);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
        {
            return string.Empty;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }

        byte[] protectedBytes = Convert.FromBase64String(protectedText);
        byte[] plainBytes = ProtectedDataWindows(protectedBytes, encrypt: false);
        return Encoding.UTF8.GetString(plainBytes);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static byte[] ProtectedDataWindows(byte[] data, bool encrypt) =>
        encrypt
            ? ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser)
            : ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
}

/// <summary>
/// Non-Windows development fallback. NOT secure -- reversible obfuscation only, so local
/// development on macOS/Linux doesn't hard-fail. Never used when
/// <see cref="OperatingSystem.IsWindows"/> is true (see DI registration in
/// ServiceCollectionExtensions, which chooses based on that check).
/// </summary>
public sealed class NoOpSecretProtector : ISecretProtector
{
    private const string Prefix = "plain:";

    public string Protect(string plainText) => Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
        {
            return string.Empty;
        }

        string base64 = protectedText.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedText[Prefix.Length..]
            : protectedText;

        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
