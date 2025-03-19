using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WK.Libraries.SharpClipboardNS;
using System.Windows.Forms;

namespace ClipboardListener
{
    internal class Program
    {
        // Thread-safe collection to track active downloads keyed by clipboard text (URL)
        private static readonly ConcurrentDictionary<string, bool> ActiveDownloads = new ConcurrentDictionary<string, bool>();

        [STAThread]
        static async Task<int> Main(string[] args)
        {
            // Define the command-line options.
            var patternOption = new Option<string>(
                "--pattern",
                description: "The regex pattern to detect in the clipboard text")
            {
                IsRequired = true
            };

            var commandOption = new Option<string>(
                "--command",
                description: "The CLI command to execute when the pattern is detected")
            {
                IsRequired = true
            };

            var parameterOption = new Option<string>(
                "--parameter",
                description: "An arbitrary parameter to pass to the CLI command. Use the token {clipboard} to insert the clipboard text. If omitted, the clipboard text is used.",
                getDefaultValue: () => string.Empty);

            var pauseOption = new Option<bool>(
                "--pause",
                description: "If specified, the command execution will pause after completion so that the terminal remains open.");

            // Create the root command with the defined options.
            var rootCommand = new RootCommand("Clipboard Listener CLI")
            {
                patternOption,
                commandOption,
                parameterOption,
                pauseOption
            };

            // Set the handler for the root command.
            rootCommand.SetHandler((string pattern, string command, string parameter, bool pause) =>
            {
                // Create a new thread and ensure it is STA.
                Thread staThread = new Thread(() =>
                {
                    RunClipboardListener(pattern, command, parameter, pause);
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
            }, patternOption, commandOption, parameterOption, pauseOption);

            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// Splits a command-line string into individual arguments.
        /// This is a simple parser that handles quoted arguments.
        /// </summary>
        private static IEnumerable<string> SplitArguments(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                yield break;

            bool inQuotes = false;
            var arg = new StringBuilder();
            foreach (char c in commandLine)
            {
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (arg.Length > 0)
                    {
                        yield return arg.ToString();
                        arg.Clear();
                    }
                }
                else
                {
                    arg.Append(c);
                }
            }
            if (arg.Length > 0)
                yield return arg.ToString();
        }

        private static void RunClipboardListener(string pattern, string command, string parameter, bool pause)
        {
            Regex regex = new Regex(pattern);
            Console.WriteLine("Listening for clipboard changes...");

            // Create a SharpClipboard instance.
            var clipboard = new SharpClipboard();

            // Subscribe to the ClipboardChanged event.
            clipboard.ClipboardChanged += (sender, args) =>
            {
                string clipboardText = clipboard.ClipboardText;
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    Console.WriteLine($"Clipboard updated: {clipboardText}");
                    if (regex.IsMatch(clipboardText))
                    {
                        // Check if this URL is already being processed.
                        if (!ActiveDownloads.TryAdd(clipboardText, true))
                        {
                            Console.WriteLine("This URL is already being processed. Skipping duplicate.");
                            return;
                        }

                        Console.WriteLine("Pattern matched. Executing command...");

                        // Substitute the {clipboard} token if present.
                        string rawParameter = string.IsNullOrEmpty(parameter)
                            ? clipboardText
                            : parameter.Replace("{clipboard}", clipboardText);

                        // Run the command in a separate task to avoid blocking the clipboard listener.
                        Task.Run(() =>
                        {
                            try
                            {
                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = command,
                                    UseShellExecute = false, // Required for ArgumentList.
                                    CreateNoWindow = false
                                };

                                // Split the rawParameter string into individual arguments.
                                foreach (var arg in SplitArguments(rawParameter))
                                {
                                    startInfo.ArgumentList.Add(arg);
                                }

                                // Start the process.
                                var process = Process.Start(startInfo);

                                // If pause is requested, wait for the process to exit and then prompt.
                                if (pause && process != null)
                                {
                                    process.WaitForExit();
                                    Console.WriteLine("Press any key to continue...");
                                    Console.ReadKey();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error executing command: {ex.Message}");
                            }
                            finally
                            {
                                // Remove the URL from active downloads once processing is finished.
                                ActiveDownloads.TryRemove(clipboardText, out _);
                            }
                        });
                    }
                    else
                    {
                        Console.WriteLine("Pattern not matched.");
                    }
                }
            };

            // Start a task to allow exiting the application.
            Task.Run(() =>
            {
                Console.WriteLine("Press [Enter] to exit.");
                Console.ReadLine();
                Application.ExitThread();
            });

            // Run the Windows Forms message loop.
            Application.Run();
        }
    }
}
