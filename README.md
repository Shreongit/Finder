# Finder

Finder is a lightweight Windows launcher for finding and opening Start Menu applications, files, folders, websites, and Windows Settings.

## Features

- Reusable, compact WPF search window with keyboard navigation.
- Searches user and common Start Menu shortcuts, common user folders, Windows Settings, and web URLs/searches.
- Search providers run independently; stale UI queries are canceled and the filesystem scan is bounded.
- The installer can add a per-user sign-in startup shortcut; Finder starts hidden in the notification area.
- The flyout dismisses when it loses activation and when a result launches successfully. It clears and cancels searches when hidden.
- Finder follows the current user's Windows app light/dark preference and updates when Windows changes it.
- File search covers the current user's profile and standard Desktop, Documents, Downloads, Pictures, Music, and Videos folders. It searches nested folders breadth-first, skips reparse points, and caps each query at 2,000 directories / 150,000 entries and 50 file/folder matches. This is a responsive best-effort user-profile search, not full-disk indexing.

## Technology

C# 14, .NET 10, WPF, and Windows APIs. The project uses a modular solution with separate core, Windows platform, and search provider projects.

## Prerequisites

- Windows 10 or later.
- .NET 10 SDK to build; .NET 10 Desktop Runtime to run framework-dependent builds.
- WiX Toolset SDK 7.0.0 is restored automatically when building the MSI.

## Build and run

```powershell
dotnet restore .\Finder.slnx
dotnet build .\Finder.slnx --no-restore
dotnet run --project .\src\Finder.App\Finder.App.csproj
```

## Build and install the Windows MSI

From the repository root, run:

```powershell
.\scripts\build-installer.ps1 -AcceptWixEula
```

Review the [WiX Open Source Maintenance Fee terms](https://wixtoolset.org/osmf/) before passing `-AcceptWixEula`; WiX 7 requires an explicit acceptance gesture. The script restores and builds the solution in Release mode, runs the tests, publishes a self-contained `win-x64` application, and creates `artifacts\installer\FinderSetup.msi`. Set the MSI version with `-ProductVersion`, for example `-ProductVersion 1.2.0`. The installer is per-user and installs to `%LOCALAPPDATA%\Programs\Finder` without administrator rights. It creates a Start Menu shortcut and, by default, a sign-in Startup shortcut. To install without automatic startup, run `msiexec /i .\artifacts\installer\FinderSetup.msi AUTOSTART=0`. Finder starts hidden with its tray icon available. Uninstall using Windows Settings → Apps → Installed apps → Finder, or `msiexec /x .\artifacts\installer\FinderSetup.msi`.

## Shortcut

Finder attempts to register **Alt + Space** with Windows `RegisterHotKey`, then uses its configured fallback if Windows rejects that registration. The active shortcut appears in the tray menu. Use Escape to hide the launcher.

## Tests

```powershell
dotnet test .\Finder.slnx
```

## Known limitations

- File search performs a bounded, best-effort traversal rather than maintaining an index; very large profiles may not return every match.
- Start Menu shortcuts are indexed once for the process lifetime; newly installed apps require restarting Finder to appear.
- Windows-specific foreground activation, shortcut ownership, shell launching, and settings links require manual verification on the target Windows desktop.
- File searches are bounded; matches beyond the scan limits may not appear. Exact and prefix name matches rank above partial matches.
- The translucent WPF surface follows the Windows app theme preference, but does not enable Mica or Acrylic composition effects.
- Windows-specific foreground activation, shell launching, and settings links require manual verification on the target Windows desktop.
