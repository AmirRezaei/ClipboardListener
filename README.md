## Clipboard Listener CLI

This is a C# command-line tool that listens for clipboard changes and executes a specified command when a regex pattern is matched. It is built as a **single executable** using .NET 9.0 and requires no external dependencies.

---

## Features
- **Monitor Clipboard Changes**: Listens for text updates in the clipboard.
- **Regex Pattern Matching**: Triggers actions when clipboard content matches a specified regex pattern.
- **Execute Commands**: Runs a custom command with optional parameters.
- **Clipboard Text as Parameter**: Use `{clipboard}` as a placeholder to inject the clipboard text into the command.

---

## Getting Started

### Prerequisites
- **.NET SDK 9.0** or later
- **Windows** (Clipboard monitoring requires Windows Forms)

---

### Setup Project

1. **Clone the Repository**
```bash
git clone <repository_url>
cd ClipboardListener
```

2. **Install Dependencies**
```bash
dotnet restore
```

3. **Build the Project**
```bash
dotnet build
```

4. **Run the Project**
```bash
dotnet run -- --pattern "hello\\s+\\w+" --command "echo" --parameter "Clipboard says: {clipboard}"
```

---

### Build as Single Executable

To build a fully self-contained single executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true /p:TrimUnusedDependencies=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true
```

The executable will be located at:
```
bin\Release\net9.0-windows\win-x64\publish\ClipboardListener.exe
```

You can then move this `.exe` to any Windows machine and run it without needing to install .NET.

---

## Usage

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

---

### Parameter Placeholder
- Use `{clipboard}` in the `--parameter` value to include the actual clipboard text.

---

## Notes
- This tool is designed for **Windows** and requires **.NET 9.0**.
- Ensure to run it from a terminal with the necessary permissions to access the clipboard.

---

## Contributing
Feel free to fork this repository, submit issues, or make pull requests. All contributions are welcome!

---

## License
This project is licensed under the MIT License.
