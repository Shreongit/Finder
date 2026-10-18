# Finder

Finder is a lightweight Windows launcher for finding and opening Start Menu applications, files, folders, websites, and Windows Settings.

## Features

- Reusable, compact WPF search window with keyboard navigation.
- Searches user and common Start Menu shortcuts, common user folders, Windows Settings, and web URLs/searches.
- Search providers run independently; stale UI queries are canceled and the filesystem scan is bounded.
- Optional user startup configuration is not yet implemented.
- The flyout dismisses when it loses activation and when a result launches successfully. It clears and cancels searches when hidden.
- Finder follows the current user's Windows app light/dark preference and updates when Windows changes it.
- File search covers the current user's profile and standard Desktop, Documents, Downloads, Pictures, Music, and Videos folders. It searches nested folders breadth-first, skips reparse points, and caps each query at 2,000 directories / 150,000 entries and 50 file/folder matches. This is a responsive best-effort user-profile search, not full-disk indexing.

## Technology

C# 14, .NET 10, WPF, and Windows APIs. The project uses a modular solution with separate core, Windows platform, and search provider projects.

## Prerequisites

- Windows 10 or later.
- .NET 10 SDK to build; .NET 10 Desktop Runtime to run framework-dependent builds.

## Build and run

```powershell
dotnet restore .\Finder.slnx
dotnet build .\Finder.slnx --no-restore
dotnet run --project .\src\Finder.App\Finder.App.csproj
```

## Shortcut

Finder attempts to register **Alt + Space** with Windows `RegisterHotKey`. Windows or another application may reserve the same shortcut; if registration fails, Finder reports the issue. The built-in Windows system-menu shortcut can conflict, and the registered behavior depends on Windows accepting Finder's registration. The shortcut has not been manually verified on every Windows configuration. When registration succeeds it toggles the launcher. Use Escape to hide it.

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
- No installer or automatic startup registration is included.
