using MonadicSharp.Security.Errors;
using System.Text.RegularExpressions;

namespace MonadicSharp.Security.Guard;

/// <summary>
/// Detects and optionally sanitizes prompt injection attempts before they
/// reach a language model or agent pipeline.
///
/// MonadicSharp.Security's PromptGuard follows the same Railway-Oriented
/// pattern as the rest of the framework: validation failures are typed
/// <see cref="Error"/> values in a <see cref="Result{T}"/>, never exceptions.
///
/// Usage:
/// <code>
/// var guard = PromptGuard.Default;
/// var result = guard.Validate(userInput);
/// result.Match(
///     onSuccess: safe  => pipeline.RunAsync(safe, ctx),
///     onFailure: error => Result&lt;Output&gt;.Failure(error));
/// </code>
/// </summary>
public sealed class PromptGuard
{
    private readonly PromptGuardOptions _options;
    private readonly IReadOnlyList<InjectionRule> _rules;

    /// <summary>Default guard with standard injection detection rules enabled.</summary>
    public static PromptGuard Default => new(PromptGuardOptions.Default);

    /// <summary>Strict guard with extended ruleset and tighter limits.</summary>
    public static PromptGuard Strict => new(PromptGuardOptions.Strict);

    public PromptGuard(PromptGuardOptions options)
    {
        _options = options;
        _rules = BuildRules(options);
    }

    /// <summary>
    /// Validates the input against all configured injection rules.
    /// Returns the original input on success (ready for forwarding to an agent),
    /// or a typed <see cref="SecurityError"/> on the first rule violation.
    /// </summary>
    public Result<string> Validate(string input)
    {
        if (input is null)
            return Result<string>.Success(string.Empty);

        if (_options.MaxInputLength > 0 && input.Length > _options.MaxInputLength)
            return Result<string>.Failure(SecurityError.InputTooLong(input.Length, _options.MaxInputLength));

        if (_options.RejectBinaryContent && ContainsBinary(input))
            return Result<string>.Failure(SecurityError.InputContainsBinary());

        foreach (var rule in _rules)
        {
            if (rule.IsMatch(input))
            {
                var excerpt = ExtractExcerpt(input, rule);
                return Result<string>.Failure(SecurityError.PromptInjectionDetected(rule.Name, excerpt));
            }
        }

        return Result<string>.Success(input);
    }

    /// <summary>
    /// Sanitizes the input by removing or replacing detected injection patterns.
    /// Use when you want to proceed with cleaned input rather than blocking entirely.
    /// </summary>
    public string Sanitize(string input)
    {
        if (input is null) return string.Empty;

        var result = input;
        foreach (var rule in _rules.Where(r => r.SanitizeReplacement != null))
            result = rule.Sanitize(result);

        return result;
    }

    private static bool ContainsBinary(string input)
        => input.Any(c => c < 32 && c != '\n' && c != '\r' && c != '\t');

    private static string? ExtractExcerpt(string input, InjectionRule rule)
    {
        var match = rule.FirstMatch(input);
        if (match == null) return null;
        var start = Math.Max(0, match.Index - 10);
        var length = Math.Min(40, input.Length - start);
        return input.Substring(start, length).Replace('\n', ' ');
    }

    private static IReadOnlyList<InjectionRule> BuildRules(PromptGuardOptions opts)
    {
        var rules = new List<InjectionRule>();

        if (opts.DetectRoleOverride)
        {
            rules.Add(new InjectionRule("RoleOverride",
                @"(?i)(you\s+are\s+now|act\s+as|pretend\s+(to\s+be|you\s+are)|ignore\s+(all\s+)?(previous|prior|above)\s+instructions?|disregard\s+(your\s+)?(previous\s+)?instructions?|forget\s+(everything|all)\s+(you\s+were\s+told|above)|new\s+persona|you\s+have\s+no\s+restrictions?)",
                "[ROLE_OVERRIDE_REMOVED]"));
        }

        if (opts.DetectSystemPromptLeak)
        {
            rules.Add(new InjectionRule("SystemPromptLeak",
                @"(?i)(show\s+me\s+your\s+(system\s+)?prompt|repeat\s+(your\s+)?(system\s+)?instructions?|what\s+(are\s+)?your\s+(initial\s+)?instructions?|print\s+your\s+(prompt|instructions?)|reveal\s+your\s+(system\s+)?prompt)",
                "[SYSTEM_PROMPT_LEAK_REMOVED]"));
        }

        if (opts.DetectDelimiterInjection)
        {
            rules.Add(new InjectionRule("DelimiterInjection",
                @"(?i)(</?(system|user|assistant|human|ai|prompt|instruction|context)>|\[INST\]|\[/INST\]|<\|im_start\|>|<\|im_end\|>|###\s*System|###\s*Human|###\s*Assistant|\[SYSTEM\]|\[USER\]|\[ASSISTANT\])",
                " "));
        }

        if (opts.DetectJailbreak)
        {
            rules.Add(new InjectionRule("Jailbreak",
                @"(?i)(DAN\s+mode|jailbreak|do\s+anything\s+now|enable\s+developer\s+mode|bypass\s+(safety|content|filter)|unrestricted\s+mode|god\s+mode|sudo\s+mode|override\s+(safety|ethical|content)\s+(filter|guard|policy))",
                "[JAILBREAK_ATTEMPT_REMOVED]"));
        }

        if (opts.DetectCodeInjection)
        {
            rules.Add(new InjectionRule("CodeInjection",
                @"(?i)(import\s+os\s*;?\s*(os\.|import)|subprocess\.(run|call|Popen)|eval\s*\(|exec\s*\(|__import__\s*\(|system\s*\(\s*[""'])",
                null, isBlockOnly: true));
        }

        return rules;
    }
}

// ── Rule engine ───────────────────────────────────────────────────────────────

internal sealed class InjectionRule
{
    private readonly Regex _regex;

    public string Name { get; }
    public string? SanitizeReplacement { get; }
    private readonly bool _isBlockOnly;

    public InjectionRule(string name, string pattern, string? sanitizeReplacement, bool isBlockOnly = false)
    {
        Name = name;
        SanitizeReplacement = sanitizeReplacement;
        _isBlockOnly = isBlockOnly;
        _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    }

    public bool IsMatch(string input)
    {
        try { return _regex.IsMatch(input); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    public Match? FirstMatch(string input)
    {
        try
        {
            var m = _regex.Match(input);
            return m.Success ? m : null;
        }
        catch (RegexMatchTimeoutException) { return null; }
    }

    public string Sanitize(string input)
    {
        if (_isBlockOnly || SanitizeReplacement == null) return input;
        try { return _regex.Replace(input, SanitizeReplacement); }
        catch (RegexMatchTimeoutException) { return input; }
    }
}

// ── Options ───────────────────────────────────────────────────────────────────

/// <summary>Configuration for <see cref="PromptGuard"/>.</summary>
public sealed class PromptGuardOptions
{
    public static PromptGuardOptions Default => new()
    {
        DetectRoleOverride = true,
        DetectSystemPromptLeak = true,
        DetectDelimiterInjection = true,
        DetectJailbreak = true,
        DetectCodeInjection = false,
        MaxInputLength = 32_000,
        RejectBinaryContent = true,
    };

    public static PromptGuardOptions Strict => new()
    {
        DetectRoleOverride = true,
        DetectSystemPromptLeak = true,
        DetectDelimiterInjection = true,
        DetectJailbreak = true,
        DetectCodeInjection = true,
        MaxInputLength = 8_000,
        RejectBinaryContent = true,
    };

    public bool DetectRoleOverride { get; init; }
    public bool DetectSystemPromptLeak { get; init; }
    public bool DetectDelimiterInjection { get; init; }
    public bool DetectJailbreak { get; init; }
    public bool DetectCodeInjection { get; init; }
    public int MaxInputLength { get; init; }
    public bool RejectBinaryContent { get; init; }
}
