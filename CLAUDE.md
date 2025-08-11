# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Network Adapter Manager is a Windows overlay application for managing network adapters with a modern, dark-themed UI. It provides quick access to network configuration, adapter management, and profile-based network settings.

## Debug Monitoring for Claude Code

The application includes a comprehensive debug monitoring system that allows Claude Code to capture, analyze, and auto-fix runtime issues:

### Debug Commands

```bash
# Start debug monitoring
start-debug [--autofix]    # Start capturing events (with optional auto-fix)
stop-debug                 # Stop capturing events
analyze                    # Analyze current application state
get-errors [count]         # Get recent errors (default: 10)
apply-fix <error-type>     # Apply auto-fix for specific error type
generate-report            # Generate comprehensive debug report
clear-output               # Clear debug output
get-output                 # Get current debug output
watch-file <path>          # Watch file changes
test-error                 # Generate test error for testing
```

### Debug Features

1. **Real-time Event Capture**: Monitors all application events, errors, warnings, and performance metrics
2. **Auto-Fix System**: Automatically suggests and applies fixes for common errors
3. **Performance Monitoring**: Tracks CPU, memory usage, and operation timings
4. **Error Analysis**: Groups and analyzes error patterns for systematic fixes
5. **Debug Window**: Visual interface for monitoring application state
6. **Output Logging**: Saves debug logs to `debug_logs/` and Claude output to `claude_output/`

### Using Debug Mode

1. Open Debug Window: Available from main application or programmatically
2. Start capture with: `_claudeInterface.StartCapture(autoFix: true)`
3. Monitor events in real-time through the Debug Window tabs
4. Generate reports for analysis: `await _debugMonitor.GenerateDebugReport()`
5. Apply fixes automatically or manually through the interface

### Error Pattern Recognition

The system recognizes and can auto-fix:
- NullReferenceException: Adds null checks
- Network errors: Implements retry logic
- Permission errors: Suggests elevation requirements
- WMI timeouts: Adjusts timeout values

## Build and Development Commands

### Build the project
```bash
dotnet build
```

### Run the application (requires Administrator privileges)
```bash
dotnet run --project NA-ManagerShortcut/NA-ManagerShortcut.csproj
```

### Build for release
```bash
dotnet build -c Release
```

### Clean build artifacts
```bash
dotnet clean
```

### Restore NuGet packages
```bash
dotnet restore
```

## Architecture

The application follows MVVM (Model-View-ViewModel) pattern with service layer:

### Core Components

- **Models** (`/Models`): Data models for network adapters and profiles
  - `NetworkAdapterInfo`: Represents network adapter with all properties
  - `NetworkProfile`: Configuration profiles for network settings
  
- **Services** (`/Services`):
  - `NetworkAdapterService`: WMI-based network adapter management
  - `ProfileManager`: Handles saving/loading network profiles

- **ViewModels** (`/ViewModels`):
  - `BaseViewModel`: Base class with INotifyPropertyChanged
  - `MainViewModel`: Main window logic and adapter management

- **Views** (`/Views`):
  - `MainWindow`: Overlay window with adapter list
  - `ConfigurationWindow`: IP/DNS configuration dialog
  - `ProfileWindow`: Profile management interface

### Key Features

- Always-on-top overlay window with transparency
- Hotkey support (Ctrl+Alt+N) to show/hide
- System tray integration
- Real-time network adapter monitoring
- One-click enable/disable adapters
- Static IP/DHCP configuration
- Profile management with import/export
- Network statistics display

## Key Technical Details

- **Framework**: .NET 9.0 Windows-specific
- **UI Framework**: WPF with XAML
- **Dependencies**: 
  - System.Management (WMI access)
  - Hardcodet.NotifyIcon.Wpf (System tray)
  - Newtonsoft.Json (Profile serialization)
- **Privileges**: Requires Administrator (configured in app.manifest)
- **Language Features**: C# with nullable reference types
- **Pattern**: MVVM with Commands and Data Binding