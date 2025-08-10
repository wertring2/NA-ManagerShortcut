# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Network Adapter Manager is a Windows overlay application for managing network adapters with a modern, dark-themed UI. It provides quick access to network configuration, adapter management, and profile-based network settings.

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
- Hotkey support (Win+N) to show/hide
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