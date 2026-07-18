using System.Text.RegularExpressions;

namespace Qistas.Infrastructure.Logging;

/// <summary>
/// Redacts secrets (client_secret, access_token, and similar) out of request/response
/// bodies before they are written to the Serilog request/response audit log
/// (AGENT_INSTRUCTION.md section 5: "Log every request/response pair, secrets redacted").
/// </summary>
public static class SecretRedactor
{
    private static readonly Regex[] Patterns =
    {
        BuildFieldPattern("client_secret"),
        BuildFieldPattern("access_token"),
        BuildFieldPattern("ClientSecretProtected"),
        BuildFieldPattern("ClientSecret"),
        BuildFieldPattern("password"),
        BuildFieldPattern("Authorization"),
    };

    public static string Redact(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content ?? string.Empty;
        }

        string result = content;
        foreach (var pattern in Patterns)
        {
            result = pattern.Replace(result, m => m.Groups[1].Value + "***REDACTED***");
        }

        return result;
    }

    // Matches "field": "value"  or  field=value  (JSON and form-urlencoded shapes), case-insensitive.
    // Group 1 is the "field:/=" prefix (kept); the value that follows is replaced.
    private static Regex BuildFieldPattern(string fieldName) => new(
        $"(\"?{fieldName}\"?\\s*[:=]\\s*\"?)[^\",&\\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
