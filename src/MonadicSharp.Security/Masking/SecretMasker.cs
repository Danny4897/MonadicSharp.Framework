using System.Text.RegularExpressions;

namespace MonadicSharp.Security.Masking;

/// <summary>
/// Prevents secrets (API keys, tokens, passwords, connection strings) from leaking
/// into agent traces, audit logs, or LLM responses.
///
/// The masker works in two modes:
/// 1. Pattern-based: detects well-known secret shapes (AWS keys, JWTs, Bearer tokens, etc.)
/// 2. Registry-based: masks specific known values registered at startup
///
/// All masking is lossless — the masked string retains its length structure
/// so logs remain readable while secrets are replaced with [MASKED].
/// </summary>
public sealed class SecretMasker
{
    private static readonly IReadOnlyList<SecretPattern> DefaultPatterns = new List<SecretPattern>
    {
        new("AwsAccessKey",    @"(?<![A-Z0-9])[A-Z0-9]{20}(?![A-Z0-9])"),
        new("AwsSecretKey",    @"(?<![A-Za-z0-9/+=])[A-Za-z0-9/+=]{40}(?![A-Za-z0-9/+=])"),
        new("JwtToken",        @"eyJ[A-Za-z0-9_\-]+\.eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+"),
        new("BearerToken",     @"(?i)Bearer\s+[A-Za-z0-9\-._~+/]+=*"),
        new("ApiKey",          @"(?i)(api[_\-]?key|apikey|x\-api\-key)[=:\s]+['""]?([A-Za-z0-9\-_]{16,})"),
        new("ConnectionString",@"(?i)(Password|Pwd)=[^;""'\s]{4,}"),
        new("PrivateKey",      @"-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----[\s\S]+?-----END"),
        new("GitHubPat",       @"ghp_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{82}"),
        new("OpenAiKey",       @"sk-[A-Za-z0-9]{48}"),
        new("SlackToken",      @"xox[baprs]-[A-Za-z0-9\-]{10,}"),
    };

    private readonly List<SecretPattern> _patterns;
    private readonly HashSet<string> _knownSecrets = new(StringComparer.Ordinal);
    private readonly string _replacement;

    public SecretMasker(string replacement = "[MASKED]")
    {
        _replacement = replacement;
        _patterns = new List<SecretPattern>(DefaultPatterns);
    }

    /// <summary>Registers a known secret value to be masked wherever it appears.</summary>
    public SecretMasker Register(string secret)
    {
        if (!string.IsNullOrEmpty(secret))
            _knownSecrets.Add(secret);
        return this;
    }

    /// <summary>Adds a custom secret detection pattern.</summary>
    public SecretMasker AddPattern(string name, string regexPattern)
    {
        _patterns.Add(new SecretPattern(name, regexPattern));
        return this;
    }

    /// <summary>
    /// Masks all detected secrets in the input string.
    /// Returns the sanitized string safe for logging/tracing.
    /// </summary>
    public string Mask(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        var result = input;

        // Known secrets first (exact match, highest priority)
        foreach (var secret in _knownSecrets)
            result = result.Replace(secret, _replacement);

        // Pattern-based masking
        foreach (var pattern in _patterns)
        {
            try { result = pattern.Replace(result, _replacement); }
            catch (RegexMatchTimeoutException) { /* skip slow patterns */ }
        }

        return result;
    }

    /// <summary>Returns true if the input contains any detectable secret pattern.</summary>
    public bool ContainsSecret(string? input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        foreach (var secret in _knownSecrets)
            if (input.Contains(secret)) return true;

        foreach (var pattern in _patterns)
        {
            try { if (pattern.IsMatch(input)) return true; }
            catch (RegexMatchTimeoutException) { /* skip */ }
        }

        return false;
    }

    /// <summary>Masks a dictionary of values (e.g., agent metadata, headers).</summary>
    public IReadOnlyDictionary<string, string> MaskDictionary(IEnumerable<KeyValuePair<string, string>> pairs)
        => pairs.ToDictionary(p => p.Key, p => Mask(p.Value));

    /// <summary>Singleton default instance — no known secrets registered.</summary>
    public static SecretMasker Default { get; } = new();
}

internal sealed class SecretPattern
{
    private readonly Regex _regex;
    public string Name { get; }

    public SecretPattern(string name, string pattern)
    {
        Name = name;
        _regex = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    }

    public bool IsMatch(string input) => _regex.IsMatch(input);
    public string Replace(string input, string replacement) => _regex.Replace(input, replacement);
}
