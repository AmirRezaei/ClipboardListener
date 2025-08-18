// ./ProcessRunner.cs
using System.Diagnostics;

namespace ClipboardListener;

public static class ProcessRunner
{
    public static async Task<(int ExitCode, string Output)> RunAsync(
        string command,
        IReadOnlyList<string> args,
        string? workingDirectory,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
            psi.WorkingDirectory = workingDirectory!;

        if (args.Count == 1 && args[0].Contains(' ') && !args[0].Contains('"'))
        {
            // If user provided a single raw string in Parameter, pass as Arguments.
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

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdOut.Add(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stdErr.Add(e.Data); };
        proc.Exited += (_, __) => tcs.TrySetResult(proc.ExitCode);

        if (!proc.Start())
            throw new InvalidOperationException($"Failed to start process: {command}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using (ct.Register(() =>
               {
                   try { if (!proc.HasExited) proc.Kill(true); }
                   catch { /* ignore */ }
               }))
        {
            var exit = await tcs.Task.ConfigureAwait(false);
            var all = string.Join(Environment.NewLine, stdOut.Concat(stdErr));
            return (exit, all);
        }
    }
}