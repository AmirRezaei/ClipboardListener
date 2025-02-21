using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WK.Libraries.SharpClipboardNS;
using System.Windows.Forms;

namespace ClipboardListener
{
    internal class Program
    {
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
        /// Escapes special characters for use in a batch file.
        /// Currently, it doubles % characters so they pass literally.
        /// </summary>
        private static string EscapeBatchParameter(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // Escape % characters by replacing each % with %%
            return input.Replace("%", "%%");
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
                        Console.WriteLine("Pattern matched. Executing command...");

                        // Substitute the {clipboard} token if present.
                        string rawParameter = string.IsNullOrEmpty(parameter)
                            ? clipboardText
                            : parameter.Replace("{clipboard}", clipboardText);

                        try
                        {
                            if (pause)
                            {
                                // Use the helper function to escape any problematic characters.
                                string safeParameter = EscapeBatchParameter(rawParameter);

                                // Create a temporary batch file to run the command and pause afterwards.
                                string tempBatchFile = Path.Combine(Path.GetTempPath(), $"ClipboardListener_{Guid.NewGuid()}.bat");
                                // The batch file will execute the command with its parameters and then call pause.
                                string batchContent = $"{command} {safeParameter}{Environment.NewLine}pause";
                                File.WriteAllText(tempBatchFile, batchContent);

                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = "cmd.exe",
                                    Arguments = $"/c \"{tempBatchFile}\"",
                                    UseShellExecute = true
                                };
                                Process.Start(startInfo);
                            }
                            else
                            {
                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = command,
                                    Arguments = rawParameter,
                                    UseShellExecute = true
                                };
                                Process.Start(startInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error executing command: {ex.Message}");
                        }
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
