### Clipboard Listener CLI

This is a C# command-line tool that listens for clipboard changes and executes a specified command when a regex pattern is matched. It is built as a **single executable** using .NET 9.0 and requires no external dependencies.

---

### Features
- **Monitor Clipboard Changes**: Listens for text updates in the clipboard.
- **Regex Pattern Matching**: Triggers actions when clipboard content matches a specified regex pattern.
- **Execute Commands**: Runs a custom command with optional parameters.
- **Clipboard Text as Parameter**: Use `{clipboard}` as a placeholder to inject the clipboard text into the command.

---

### How to Use

**Syntax:**
```bash
ClipboardListener.exe --pattern "<regex>" --command "<command>" --parameter "<parameter>"
```

**Example:**
```bash
ClipboardListener.exe --pattern "hello\\s+\\w+" --command "echo" --parameter "Clipboard says: {clipboard}"
```

This example:
- Watches the clipboard for text matching the pattern `hello <word>`.
- When matched, executes the `echo` command.
- Passes the parameter `Clipboard says: <clipboard text>` to the command.

**Parameter Placeholder:**
- Use `{clipboard}` in the `--parameter` value to include the actual clipboard text.

---

### Build and Run

To build a single executable:
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true /p:TrimUnusedDependencies=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true
```

The executable will be located at:
```
bin\Release\net9.0-windows\win-x64\publish\ClipboardListener.exe
```

---

### Notes
- This tool is designed for **Windows** and requires **.NET 9.0**.
- Ensure to run it from a terminal with the necessary permissions to access the clipboard.
