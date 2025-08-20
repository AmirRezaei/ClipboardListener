// file: ProcessRunner.cs
// ./ProcessRunner.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClipboardListener;

public static class ProcessRunner
{
    /// <summary>
    /// Run a process and stream its output. We treat both '\n' *and* '\r' as line breaks
    /// so tools like yt-dlp / gallery-dl that update a single line with carriage-returns
    /// still produce callbacks in real time.
    /// </summary>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string command,
        IReadOnlyList<string> args,
        string? workingDirectory,
        CancellationToken ct,
        Action<string>? onOutput = null,
        Action<string>? onError  = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
            psi.WorkingDirectory = workingDirectory!;

        if (args.Count == 1 && args[0].Contains(' ') && !args[0].Contains('"'))
        {
            // Single raw string passed as arguments
            psi.Arguments = args[0];
        }
        else
        {
            foreach (var a in args)
                psi.ArgumentList.Add(a);
        }

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var stdOut = new List<string>();
        var stdErr = new List<string>();
        var exitTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        proc.Exited += (_, __) => exitTcs.TrySetResult(proc.ExitCode);

        if (!proc.Start())
            throw new InvalidOperationException($"Failed to start process: {command}");

        using var reg = ct.Register(() =>
        {
            try { if (!proc.HasExited) proc.Kill(true); } catch { /* ignore */ }
        });

        // Read stdout/stderr concurrently; split on both \n and \r
        var stdoutTask = ReadStream(proc.StandardOutput, line =>
        {
            stdOut.Add(line);
            onOutput?.Invoke(line);
        }, ct);

        var stderrTask = ReadStream(proc.StandardError, line =>
        {
            stdErr.Add(line);
            onError?.Invoke(line);
        }, ct);

        // Wait for process exit and readers to finish
        var exitCode = await exitTcs.Task.ConfigureAwait(false);
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        return (exitCode,
            string.Join(Environment.NewLine, stdOut),
            string.Join(Environment.NewLine, stdErr));
    }

    private static async Task ReadStream(
        System.IO.StreamReader reader,
        Action<string> onLine,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new char[4096];

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
            if (read <= 0) break;

            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];
                if (c == '\n' || c == '\r')
                {
                    if (sb.Length > 0)
                    {
                        onLine(sb.ToString());
                        sb.Clear();
                    }
                    // swallow consecutive CR/LF; treat both as boundaries
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (sb.Length > 0)
            onLine(sb.ToString());
    }
}