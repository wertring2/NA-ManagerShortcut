# Repository Guidelines

## Project Structure & Module Organization
- `NA-ManagerShortcut/`: WPF .NET app (MVVM).
  - `Models/`, `Services/`, `ViewModels/`, `Views/`, `Converters/`, `Commands/`.
  - Entry: `App.xaml`, UI shell: `MainWindow.xaml`.
- Root scripts: `RunApp.ps1`, `RunDebug.ps1`, `build-release.bat`, `build-installer.bat`.
- Installer configs: `installer.nsi`, `installer-simple.nsi`, `installer-inno.iss`.

## Build, Test, and Development Commands
```bash
dotnet restore                      # Restore packages
dotnet build -c Debug               # Build for development
dotnet build -c Release             # Release build
dotnet run --project NA-ManagerShortcut/NA-ManagerShortcut.csproj
# Windows launchers (recommended):
powershell -File .\RunApp.ps1      # Build + run (admin optional)
powershell -File .\RunDebug.ps1    # Debug session (auto-elevates)
# Installer (requires NSIS):
build-release.bat && build-installer.bat
```

## Coding Style & Naming Conventions
- C#/.NET 9, nullable enabled; 4-space indentation.
- Types/methods/properties: PascalCase; locals/fields: camelCase (`_field` for private).
- One public class per file; file name matches class (e.g., `MainViewModel.cs`).
- MVVM boundaries: UI in `Views/`, state/actions in `ViewModels/`, I/O in `Services/`.
- XAML: keep code-behind minimal; bind via `ICommand` (see `RelayCommand`).

## Testing Guidelines
- No automated test project present. Prefer manual verification:
  - Launch via `RunDebug.ps1` and exercise adapter enable/disable, IP/DNS changes, profiles.
  - Review `debug_logs/` and `claude_output/` for errors; attach excerpts to PRs.
- If adding tests, use xUnit + `NA-ManagerShortcut.Tests` project layout.

## Commit & Pull Request Guidelines
- Commits: short, imperative subject; optional scope, e.g. `Services: fix WMI timeout`.
- Group logical changes; avoid formatting-only noise.
- PRs must include:
  - Purpose, summary of changes, and risk/impact.
  - Repro and validation steps (commands used, admin required?).
  - Screenshots/GIFs for UI changes.
  - Linked issues (e.g., `Fixes #123`).

## Security & Configuration Tips
- Many actions require Administrator privileges (see `app.manifest`). Document when features need elevation.
- Use async APIs in services; avoid blocking the UI thread—marshal to UI via `Dispatcher` when needed.
- WMI/network calls: add retries and timeouts; surface errors via the debug monitor.
- Building installers requires NSIS; install locally or adjust PATH. Artifacts land under `NA-ManagerShortcut\bin\Release\net9.0-windows\`.

