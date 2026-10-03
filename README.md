# Node Server Manager

A Windows desktop app for managing local development servers as project groups. The application is named **Dev Server Manager** and is built with C#, WPF, and .NET 10.

Register a project, configure its services and ports, and start or stop the project from one window. Commands support Node.js, npm, npx, Nx, Angular CLI, and custom launchers.

## Features

- Start, stop, and restart projects or individual services.
- Editable launch templates, command previews, and batch-launcher import.
- Project-wide and service-specific environment variables.
- Live logs, configured-port status refresh, and process discovery.
- Inspect external Node processes or scan a TCP port.
- Portable SQLite settings, light/dark/device themes, and system-tray access.

## Requirements

- Windows and the .NET 10 SDK to build and run from source.
- Node.js, npm, or other command-line tools required by the projects you manage.

## Build and run

From the repository root:

```powershell
dotnet build DevServerManager.sln --configuration Release
dotnet run --project DevServerManager --configuration Release
```

Add a project and choose its repository folder, review the service commands and ports, then select **Start project**. The application creates `dev-server-manager.db` beside its executable and stores configuration there. Keep that database private: it can contain project paths and environment-variable values.

Closing the window sends the app to the tray. Choose **Exit** from the tray menu to shut it down and stop managed projects. Project stop also terminates remaining listeners on the project's configured ports; check those port assignments before using it.

## Documentation

- [Application user guide](DevServerManager/README.md) describes current behavior.
- [Specification and delivery plan](dev-server-manager-spec.md) records the initial design; some requirements have since changed.
- [Local Angular and Node development guide](local-angular-node-development-guide.md).
- [Configure custom Angular and API ports](docs/runbooks/set-up-angular-project-to-run-on-custom-ports.md).
- [Planned improvements](docs/plans/npmservermanager/README.md).
- [Implemented changes](docs/plans/implemented/).

## License

[MIT](LICENSE). Copyright (c) 2026 ntoussaintdev.
