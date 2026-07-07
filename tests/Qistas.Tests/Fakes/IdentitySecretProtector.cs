using Qistas.Application.Abstractions;

namespace Qistas.Tests.Fakes;

/// <summary>
/// Identity <see cref="ISecretProtector"/> fake for tests -- no real DPAPI available/needed
/// off Windows or in-process; Protect/Unprotect are no-ops so tests can assert on the plain
/// "client secret" value they configured.
/// </summary>
public sealed class IdentitySecretProtector : ISecretProtector
{
    public string Protect(string plainText) => plainText;

    public string Unprotect(string protectedText) => protectedText;
}
