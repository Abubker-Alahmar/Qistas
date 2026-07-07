namespace Qistas.Application.Abstractions;

/// <summary>
/// Encrypts/decrypts secrets (e.g. the D365 client_secret) at rest. Windows implementation
/// uses DPAPI (System.Security.Cryptography.ProtectedData); never reuse Balance's
/// ClsCrypto (MD5 key derivation + static IV -- see Balance/CLAUDE.md section 6.6 and
/// AGENT_INSTRUCTION.md section 7).
/// </summary>
public interface ISecretProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedText);
}
