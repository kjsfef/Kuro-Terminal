# Kuro Terminal 1.1.1

Kuro is a custom transparent Windows command shell built with C# and WPF. It has its own inline prompt, parser, history, completion, aliases, variables, themes, file commands, system tools, and persistent configuration. Unknown commands can also launch programs found in the normal Windows `PATH`.

Copyright © 2026 @Lthest. All rights reserved.

## What changed in 1.1.1

- Rebuilt the frame as a smoother layered glass edge with a thinner theme-aware stroke
- Rounded and refined the window controls and title-bar spacing
- Replaced the Arch-like mountain with an original Kuro eclipse `K` monogram
- Uses the same Kuro icon in the title bar, executable, taskbar, installer, and watermark
- Removed app updater and release controls from `help`, `commands`, categories, and completion
- Automatic installed-app update checks remain internal and silent
- Built-in command count: 234

## Open the project

1. Extract the ZIP.
2. Open `Kuro.sln` in Visual Studio 2022.
3. Install the **.NET desktop development** workload and .NET 8 if asked.
4. Allow NuGet to restore Velopack.
5. Select **Build → Clean Solution**, then **Build → Rebuild Solution**.
6. Press **Ctrl+F5**.

## Open-source tool hub

Kuro can install official Windows release assets from supported open-source projects. GitHub assets are stored under `%LOCALAPPDATA%\Kuro\Tools`, with release metadata and SHA-256 records saved beside each tool.

```text
tools list
tools licenses
tools info trivy
tools install subfinder
tools install all
tools update all
tools remove ripgrep
```

Integrated projects include Subfinder, OWASP Amass, Gitleaks, Trivy, Syft, Grype, ripgrep, age, jq, and ExifTool.

Kuro exposes focused wrappers intended for public research, local analysis, and authorized systems:

```text
subfinder example.com
amasspassive example.com
metadata photo.jpg
metaclean photo.jpg
secretscan .
trivyfs .
sbom . project-sbom.json
vulnscan .
fastgrep "TODO" .
jsonq ".items[]" data.json
agekey my-key.txt
```

## Automatic updates

Automatic app updating is an internal application feature, not a user terminal command. Installed copies silently check the release feed when Kuro starts. Only the repository owner publishes a newer version.

The owner setup instructions remain in `SETUP-UPDATES.md`. Copies launched directly from Visual Studio are not self-updated.

## Configuration

Settings are saved at:

```text
%APPDATA%\Kuro\config.json
```

Useful customization commands:

```text
theme list
theme random
opacity 0.60
font 16
prompt set "[{user}@{host} {path}]{symbol} "
motd set welcome to kuro
alias projects="cd C:\Users\YourName\Documents\Projects"
bookmark add projects C:\Users\YourName\Documents\Projects
```

## Important files

- `Kuro/MainWindow.xaml` — polished glass frame and visual layout
- `Kuro/MainWindow.xaml.cs` — inline input/output, theming, and silent startup update check
- `Kuro/ShellEngine.cs` — parser and core command system
- `Kuro/OpenSourceToolManager.cs` — open-source tool downloads and execution
- `Kuro/AutoUpdater.cs` — internal update checking and application
- `.github/workflows/publish-kuro.yml` — owner-only release workflow
- `Kuro/Assets/Kuro.ico` — executable and installer icon
- `Kuro/Assets/Kuro.png` — title-bar image and watermark
