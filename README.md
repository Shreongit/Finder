<h1 align="center">Finder</h1>
<p align="center">
  <strong>A faster way to find anything on Windows.</strong>
</p>

<p align="center">
  A lightweight desktop search launcher for applications, files, folders, Windows settings, and web searches — all from one compact interface.
</p>

<p align="center">
  <a href="https://github.com/Shreongit/Finder/releases/tag/Version1">Download Finder</a> ·
  <a href="https://github.com/Shreongit/Finder/issues">Report a Bug</a> ·
  <a href="https://github.com/Shreongit/Finder/issues">Request a Feature</a>
</p>

---

## Overview

Finder is a Windows search launcher designed to make everyday desktop navigation faster. Open it with a global keyboard shortcut, enter what you're looking for, and launch the matching result without navigating through multiple menus.

Built with C# and WPF, Finder combines application launching, file discovery, Windows Settings shortcuts, and web search in one keyboard-driven interface.

## Screenshots

### Application Search

Find installed applications and launch them directly from Finder.

<p align="center">
  <img src="assets/readme/finder-app-search.png" alt="Finder application search interface" width="850">
</p>

### File, Folder & Web Search

Search common user directories or perform a web search from the same interface.

<p align="center">
  <img src="assets/readme/finder-file-and-web-search.png" alt="Finder file, folder, and web search interface" width="850">
</p>

## Features

- **Global keyboard shortcut** — Open Finder using `Alt + Space`, with `Ctrl + Alt + Space` as a fallback when the preferred shortcut is unavailable.
- **Application launcher** — Find applications through Windows Start Menu shortcuts.
- **File and folder discovery** — Search your user profile and common folders, including Desktop, Documents, Downloads, Pictures, Music, and Videos.
- **Windows Settings search** — Quickly locate supported system settings for display, sound, networking, Bluetooth, updates, storage, and more.
- **Integrated web search** — Search Google from your browser or open a web address.
- **Keyboard-first navigation** — Move through results using the arrow keys and launch a selection with Enter.
- **Light and dark themes** — Follow your Windows app theme preference.
- **System tray integration** — Keep Finder available in the notification area and reopen it when needed.
- **Automatic startup** — Start Finder when you sign in to Windows, enabled by default in the installer.
- **Per-user installation** — Install without requiring administrator access.

## Download & Installation

**The easiest way to get started is to download the Windows installer.**

[**Download FinderSetup.msi from GitHub Releases**](https://github.com/Shreongit/Finder/releases/tag/Version1)

1. Download `FinderSetup.msi` from the release assets.
2. Run the installer.
3. Review and accept the included license agreement.
4. Follow the installation prompts.

Finder installs for your Windows user under:

```text
%LOCALAPPDATA%\Programs\Finder
```

The installer creates a Start Menu shortcut and enables automatic startup by default.

### Disable automatic startup

To install Finder without enabling automatic startup, run this command from the directory containing the installer:

```powershell
msiexec /i FinderSetup.msi AUTOSTART=0
```

You can also manage startup behavior through **Windows Settings → Apps → Startup**.

### Uninstall

Open **Settings → Apps → Installed apps**, find Finder, and select **Uninstall**.

## How to Use Finder

1. Press `Alt + Space` to open Finder. If that shortcut is unavailable, try the fallback shortcut, `Ctrl + Alt + Space`.
2. Type the name of an application, file, folder, or supported Windows setting.
3. Use `↑` and `↓` to navigate the search results.
4. Press `Enter` to launch the selected result, or use the mouse.
5. Press `Esc` to hide Finder.

You can also open Finder from its notification-area icon. The active keyboard shortcut is shown in the tray menu.

## Search Scope & Limitations

Finder is designed for quick discovery, not as a complete desktop indexing engine.

- **Applications:** Searches Windows Start Menu shortcuts available to your account and all users.
- **Files and folders:** Searches common user directories using a bounded, best-effort scan. It does not currently provide a complete indexed search of every drive.
- **Windows Settings:** Supports a set of recognized system-setting queries.
- **Web:** Opens web addresses or sends selected search queries to Google through your browser.

Search results may vary depending on your Windows configuration, installed applications, available folders, and scan limits.

## System Requirements

- Windows 10 or later, 64-bit.
- No separate .NET runtime installation is required when using the packaged release.

## Technology Stack

| Technology | Purpose |
|---|---|
| C# | Application logic |
| .NET 10 | Runtime and application platform |
| Windows Presentation Foundation (WPF) | Desktop user interface |
| WiX Toolset 7 | Windows MSI installer |
| xUnit | Automated tests |

## Project Structure

```text
Finder/
├── src/
│   ├── Finder.App/                 # WPF application and user interface
│   ├── Finder.Core/                # Core search abstractions and services
│   ├── Finder.Platform.Windows/    # Windows integration and launching
│   ├── Finder.Search.Apps/         # Application search
│   ├── Finder.Search.Files/        # File and folder search
│   ├── Finder.Search.Settings/     # Windows Settings search
│   └── Finder.Search.Web/          # Web search
├── tests/
│   └── Finder.Tests/               # Automated tests
├── installer/                      # WiX installer project and license
├── scripts/                        # Build and packaging scripts
├── assets/
│   └── readme/                     # README screenshots and visuals
├── Finder.slnx
└── README.md
```

## Build from Source

### Prerequisites

- Windows 10 or later, 64-bit.
- .NET 10 SDK.
- WiX Toolset 7 and its required extensions to build the MSI installer.

### Clone the repository

```powershell
git clone https://github.com/Shreongit/Finder.git
cd Finder
```

### Build the solution

```powershell
dotnet build Finder.slnx -c Release
```

### Run the automated tests

```powershell
dotnet test Finder.slnx -c Release
```

### Build the installer

From Windows PowerShell, run:

```powershell
.\scripts\build-installer.ps1
```

Review the script's prerequisites and parameters if your local development environment differs. Build and installer outputs are generated locally and are not intended to be committed to the source repository.

## Contributing

Contributions, bug reports, and practical feature suggestions are welcome.

1. Check the existing [issues](https://github.com/Shreongit/Finder/issues) before opening a duplicate.
2. Open an issue to discuss a substantial change.
3. Create a focused branch for your changes.
4. Build the solution and run the automated tests.
5. Submit a pull request with a clear description of the change.

Please avoid committing generated build outputs, local IDE state, or machine-specific configuration.

## License

Finder is distributed under the End-User License Agreement included with the installer. Review the applicable terms before using or redistributing the application. Third-party dependencies may have separate license terms.

## Support

Found a bug or have an idea for improving Finder?

- [Report a bug or request a feature](https://github.com/Shreongit/Finder/issues)
- [Browse the source code](https://github.com/Shreongit/Finder)
- [Download the latest published release](https://github.com/Shreongit/Finder/releases)

---

<p align="center">
  Built to make finding things on Windows simpler.
</p>

<div style="font-family: -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, Roboto, &quot;Helvetica Neue&quot;, Arial, sans-serif; border: 1px solid rgb(224, 224, 224); border-radius: 12px; padding: 20px; max-width: 500px; background: rgb(255, 255, 255); box-shadow: rgba(0, 0, 0, 0.05) 0px 2px 8px;"><div style="display: flex; align-items: center; gap: 12px; margin-bottom: 12px;"><img alt="Finder" src="https://ph-files.imgix.net/45712573-6b49-49c2-8cd2-0f07f398ebff.png?auto=compress,format&amp;codec=mozjpeg&amp;cs=strip&amp;fit=crop&amp;h=80&amp;w=80" style="width: 64px; height: 64px; border-radius: 8px; object-fit: cover; flex-shrink: 0;"><div style="flex: 1 1 0%; min-width: 0px;"><h3 style="margin: 0px; font-size: 18px; font-weight: 600; color: rgb(26, 26, 26); line-height: 1.3; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">Finder</h3><p style="margin: 4px 0px 0px; font-size: 14px; color: rgb(102, 102, 102); line-height: 1.4; overflow: hidden; text-overflow: ellipsis; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;">A faster way to find anything on Windows.</p></div></div><a href="https://www.producthunt.com/products/finder-5?embed=true&amp;utm_source=embed&amp;utm_medium=post_embed" target="_blank" rel="noopener" style="display: inline-flex; align-items: center; gap: 4px; margin-top: 12px; padding: 8px 16px; background: rgb(255, 97, 84); color: rgb(255, 255, 255); text-decoration: none; border-radius: 9999px; font-size: 16px; font-weight: 600; line-height: 1.5;">Check it out on Product Hunt →</a></div>
