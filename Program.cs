// file: Program.cs
// file: Program.cs
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ClipboardListener;

internal static class Program
{
    private static CancellationTokenSource? _cts;

    // --- Simple FIFO queue to serialize downloads ---
    private static readonly ConcurrentQueue<Job> Queue = new();
    private static readonly SemaphoreSlim QueueSignal = new(0);
    private static Task? _workerTask;

    // Console writes serialized (so progress lines don't interleave)
    private static readonly object ConsoleLock = new();

    // Monotonic job id
    private static int _nextJobId = 1;

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            var configPath = GetConfigPath(args);
            var config = ConfigLoader.Load(configPath);

            lock (ConsoleLock)
            {
                Console.WriteLine($"[ClipboardListener] Loaded config: {Path.GetFullPath(configPath)}");
                Console.WriteLine($"[ClipboardListener] PollIntervalMs={config.PollIntervalMs}, Rules={config.Rules.Count}");
            }

            _cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _cts!.Cancel();
                // Wake the worker if it's waiting
                try { QueueSignal.Release(); } catch { /* ignore */ }
            };

            // Start the single worker that processes the queue sequentially
            _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));

            using var watcher = new ClipboardWatcher(config.PollIntervalMs);
            watcher.TextCopied += (_, text) =>
            {
                try { HandleClipboardText(text, config); }
                catch (Exception ex)
                {
                    lock (ConsoleLock)
                    {
                        Console.Error.WriteLine("[ClipboardListener] Error handling clipboard text:");
                        Console.Error.WriteLine(ex);
                    }
                }
            };

            lock (ConsoleLock)
                Console.WriteLine("[ClipboardListener] Monitoring clipboard. Press Ctrl+C to exit.");

            watcher.Start(_cts.Token);

            // Wait until cancelled
            _cts.Token.WaitHandle.WaitOne();

            // Drain/finish worker (best effort)
            try { _workerTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        }
        catch (OperationCanceledException)
        {
            // graceful exit
        }
        catch (Exception ex)
        {
            lock (ConsoleLock)
            {
                Console.Error.WriteLine("[ClipboardListener] Fatal error:");
                Console.Error.WriteLine(ex);
            }
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
                args[i] = args[i].Replace("{clipboard}", text, StringComparison.Ordinal);

            var displayName = string.IsNullOrWhiteSpace(rule.Name) ? rule.Pattern : rule.Name!;
            EnqueueJob(new Job
            {
                Id = Interlocked.Increment(ref _nextJobId),
                DisplayName = displayName,
                Command = rule.Command,
                Args = args,
                WorkingDirectory = rule.WorkingDirectory,
                PauseAfterRun = rule.PauseAfterRun ?? config.PauseAfterRun
            });
        }
    }

    // -------------------- Queue engine --------------------

    private static void EnqueueJob(Job job)
    {
        Queue.Enqueue(job);
        QueueSignal.Release();

        lock (ConsoleLock)
        {
            Console.WriteLine($"[Queue] Enqueued #{job.Id}: {job.DisplayName}");
            Console.WriteLine($"[Queue] Status: {Queue.Count} waiting");
        }
    }

    private static async Task WorkerLoopAsync(CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await QueueSignal.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (ct.IsCancellationRequested) break;

            // Drain one job (we process sequentially)
            if (!Queue.TryDequeue(out var job))
                continue;

            var waitingAfterDequeue = Queue.Count;
            lock (ConsoleLock)
            {
                Console.WriteLine();
                Console.WriteLine($"===== Starting #{job.Id}: {job.DisplayName} =====");
                Console.WriteLine($"[Queue] {waitingAfterDequeue} still waiting");
            }

            try
            {
                await RunJobAsync(job, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (ConsoleLock)
                    Console.WriteLine($"[Job #{job.Id}] Cancelled.");
                break;
            }
            catch (Exception ex)
            {
                lock (ConsoleLock)
                {
                    Console.Error.WriteLine($"[Job #{job.Id}] Unhandled error:");
                    Console.Error.WriteLine(ex);
                }
            }

            lock (ConsoleLock)
            {
                Console.WriteLine($"[Queue] Done with #{job.Id}. {Queue.Count} waiting.");
            }
        }
    }

    // -------------------- Single job execution --------------------
   private static async Task RunJobAsync(Job job, CancellationToken ct)
    {
        // Progress printing state (single-line rewrite)
        bool progressLineActive = false;
        int lastWidth = 0;

        void PrintProgress(string line)
        {
            lock (ConsoleLock)
            {
                progressLineActive = true;
                var prefix = $"[Job #{job.Id}] ";
                var text = $"{prefix}{line}";
                var width = Math.Max(1, Console.BufferWidth - 1);
                if (text.Length < width) text = text.PadRight(width);
                else if (text.Length > width) text = text.Substring(0, Math.Max(0, width - 1)) + "…";
                Console.Write('\r');
                Console.Write(text);
                lastWidth = width;
            }
        }

        void EndProgressLineIfAny()
        {
            lock (ConsoleLock)
            {
                if (progressLineActive)
                {
                    Console.WriteLine(); // move to next line so further logs don't overwrite
                    progressLineActive = false;
                }
            }
        }

        void Log(string s)
        {
            EndProgressLineIfAny();
            lock (ConsoleLock) Console.WriteLine($"[Job #{job.Id}] {s}");
        }

        void LogErr(string s)
        {
            EndProgressLineIfAny();
            lock (ConsoleLock) Console.Error.WriteLine($"[Job #{job.Id}] {s}");
        }

        // Classify progress-ish lines from yt-dlp/gallery-dl
        static bool IsProgressLike(string line)
            => line.StartsWith("[download]")
            || line.StartsWith("[Merger]")
            || line.StartsWith("[ExtractAudio]")
            || line.StartsWith("[Fixup")
            || line.StartsWith("[gallery-dl]");

        static bool IsAlreadyDownloadedText(string line)
        {
            var l = line.ToLowerInvariant();
            return (l.Contains("already downloaded")
                    || (l.Contains("already") && (l.Contains("exist") || l.Contains("present")))
                    || l.Contains("exists, skipping")
                    || l.Contains("file is already present"));
        }

        // Announce command
        lock (ConsoleLock)
        {
            Console.WriteLine($"[Job #{job.Id}] Command: {job.Command} {string.Join(' ', job.Args.Select(QuoteIfNeeded))}");
        }

        // Run the process and stream its output
        var (exit, stdout, stderr) = await ProcessRunner.RunAsync(
            job.Command,
            job.Args,
            job.WorkingDirectory,
            ct,
            onOutput: line =>
            {
                if (IsAlreadyDownloadedText(line))
                    Log("Already downloaded.");
                else if (IsProgressLike(line))
                    PrintProgress(line);
                else
                    Log(line);
            },
            onError: line =>
            {
                if (IsAlreadyDownloadedText(line))
                    Log("Already downloaded.");
                else if (IsProgressLike(line))
                    PrintProgress(line);
                else
                    LogErr(line);
            });

        // finalize progress line if we were rewriting it
        EndProgressLineIfAny();

        Log($"Exit code: {exit}");
        if (exit != 0)
        {
            if (!string.IsNullOrWhiteSpace(stderr)) LogErr(stderr);
            else if (!string.IsNullOrWhiteSpace(stdout)) LogErr(stdout);
        }

        if (job.PauseAfterRun)
        {
            lock (ConsoleLock) Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }

    private static string QuoteIfNeeded(string s)
        => s.Any(char.IsWhiteSpace) ? $"\"{s}\"" : s;

    // -------------------- Types --------------------

    private sealed class Job
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = "";
        public string Command { get; set; } = "";
        public List<string> Args { get; set; } = new();
        public string? WorkingDirectory { get; set; }
        public bool PauseAfterRun { get; set; }
    }
}