using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
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

            // Create the root command with the defined options.
            var rootCommand = new RootCommand("Clipboard Listener CLI")
            {
                patternOption,
                commandOption,
                parameterOption
            };

            // Set the handler for the root command.
            rootCommand.SetHandler((string pattern, string command, string parameter) =>
            {
                // Create a new thread and ensure it is STA.
                Thread staThread = new Thread(() =>
                {
                    RunClipboardListener(pattern, command, parameter);
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
            }, patternOption, commandOption, parameterOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static void RunClipboardListener(string pattern, string command, string parameter)
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
                        string actualParameter = string.IsNullOrEmpty(parameter)
                            ? clipboardText
                            : parameter.Replace("{clipboard}", clipboardText);

                        try
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = command,
                                Arguments = actualParameter,
                                UseShellExecute = true
                            };

                            Process.Start(startInfo);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error executing command: {ex.Message}");
                        }
                    }
                }
            };

            // Start a background task to allow exiting the application.
            Task.Run(() =>
            {
                Console.WriteLine("Press [Enter] to exit.");
                Console.ReadLine();
                Application.ExitThread(); // Ends the message loop.
            });

            // Start the Windows Forms message loop to process clipboard events.
            Application.Run();
        }
    }
}
