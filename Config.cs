// file: Config.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ClipboardListener;

public sealed class AppConfig
{
    public int PollIntervalMs { get; init; } = 400;

    /// <summary>Global pause after each matched run (can be overridden per rule).</summary>
    public bool PauseAfterRun { get; init; } = false;

    public List<Rule> Rules { get; init; } = new();

    [JsonIgnore]
    internal bool IsValidated { get; set; }
}

public sealed class Rule
{
    /// <summary>Friendly name for logs (optional).</summary>
    public string? Name { get; init; }

    /// <summary>Enable/disable this rule (default: true).</summary>
    public bool? Enabled { get; init; } = true;

    /// <summary>Regular expression pattern that must match the clipboard text.</summary>
    public string Pattern { get; init; } = "";

    /// <summary>Command to execute (e.g., "yt-dlp.exe" or "gallery-dl").</summary>
    public string Command { get; init; } = "";

    /// <summary>Arguments as an array to avoid quoting/escaping issues. Use "{clipboard}" placeholder.</summary>
    public string[]? Args { get; init; }

    /// <summary>
    /// Optional fallback if you really want to pass a single raw argument string.
    /// Prefer Args instead to avoid quoting issues.
    /// </summary>
    public string? Parameter { get; init; }

    /// <summary>Optional working directory for the process.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Override global PauseAfterRun for this rule.</summary>
    public bool? PauseAfterRun { get; init; }

    /// <summary>Case-insensitive match (default true).</summary>
    public bool? IgnoreCase { get; init; } = true;

    [JsonIgnore] internal Regex CompiledRegex { get; set; } = null!;
}

public static class ConfigLoader
{
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Config file not found", path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<AppConfig>(json, options)
                     ?? throw new InvalidOperationException("Failed to parse config.json");

        ValidateAndCompile(config);
        config.IsValidated = true;
        return config;
    }

    private static void ValidateAndCompile(AppConfig config)
    {
        if (config.Rules.Count == 0)
            throw new InvalidOperationException("No rules defined. Please add at least one rule.");

        foreach (var rule in config.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Pattern))
                throw new InvalidOperationException("Rule has empty 'pattern'.");

            if (string.IsNullOrWhiteSpace(rule.Command))
                throw new InvalidOperationException($"Rule '{rule.Pattern}' has empty 'command'.");

            var opts = RegexOptions.Compiled;
            if (rule.IgnoreCase is null || rule.IgnoreCase.Value)
                opts |= RegexOptions.IgnoreCase;

            rule.CompiledRegex = new Regex(rule.Pattern, opts, TimeSpan.FromSeconds(2));
        }
    }
}