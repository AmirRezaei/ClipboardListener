# Clipboard Listener

Monitors your Windows clipboard and, when the copied text matches any **regex rule** in `config.json`, runs a specified command with arguments. Designed to avoid quoting/escaping pain by taking arguments as a JSON array.

---

## Quick Start

1) **Build**
```bash
dotnet publish -c Release -r win-x64 --self-contained true ^
  /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false

---

## License
This project is licensed under the MIT License.
