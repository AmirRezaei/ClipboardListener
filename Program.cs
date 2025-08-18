// file: Program.cs
using System.Text.RegularExpressions;

namespace ClipboardListener;

internal static class Program
{
    private static CancellationTokenSource? _cts;

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            var configPath = GetConfigPath(args);
            var config = ConfigLoader.Load(configPath);

            Console.WriteLine($"[ClipboardListener] Loaded config: {Path.GetFullPath(configPath)}");
            Console.WriteLine($"[ClipboardListener] PollIntervalMs={config.PollIntervalMs}, Rules={config.Rules.Count}");

            _cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _cts!.Cancel();
            };

            using var watcher = new ClipboardWatcher(config.PollIntervalMs);
            watcher.TextCopied += (_, text) =>
            {
                HandleClipboardText(text, config);
            };

            Console.WriteLine("[ClipboardListener] Monitoring clipboard. Press Ctrl+C to exit.");
            watcher.Start(_cts.Token);

            // Wait until cancelled
            _cts.Token.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException)
        {
            // graceful exit
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[ClipboardListener] Fatal error:");
            Console.Error.WriteLine(ex);
        }
    }

    private static string GetConfigPath(string[] args)
    {
        // Usage:
        //   ClipboardListener.exe [path-to-config.json]
        // Defaults to "config.json" next to the executable.
        if (args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0]))
            return args[0];

        var exeDir = AppContext.BaseDirectory;
        var defaultPath = Path.Combine(exeDir, "config.json");
        return defaultPath;
    }

    private static void HandleClipboardText(string text, AppConfig config)
    {
        foreach (var rule in config.Rules)
        {
            if (rule.Enabled is false) continue;
            if (!rule.CompiledRegex.IsMatch(text)) continue;

            var args = rule.Args?.ToList() ?? new List<string>();
            if (args.Count == 0 && !string.IsNullOrWhiteSpace(rule.Parameter))
            {
                // Fallback: pass raw string as a single argument set (kept for compatibility).
                // Prefer "args": [] in JSON to avoid quoting issues.
                args = new List<string> { rule.Parameter! };
            }

            // Replace placeholders
            for (int i = 0; i < args.Count; i++)
            {
                args[i] = args[i].Replace("{clipboard}", text, StringComparison.Ordinal);
            }

            var displayName = string.IsNullOrWhiteSpace(rule.Name) ? rule.Pattern : rule.Name!;
            Console.WriteLine($"[ClipboardListener] Match: {displayName}");
            Console.WriteLine($"[ClipboardListener] -> {rule.Command} {string.Join(' ', args.Select(QuoteIfNeeded))}");

            _ = Task.Run(async () =>
            {
                var (code, _) = await ProcessRunner.RunAsync(rule.Command, args, rule.WorkingDirectory, CancellationToken.None);
                Console.WriteLine($"[ClipboardListener] Exit code: {code}");

                if (rule.PauseAfterRun ?? config.PauseAfterRun)
                {
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                }
            });
        }
    }

    private static string QuoteIfNeeded(string s)
    {
        return s.Any(char.IsWhiteSpace) ? $"\"{s}\"" : s;
    }
}