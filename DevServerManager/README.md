# Dev Server Manager

Windows WPF app for managing local development servers as project groups.

## Run

```powershell
dotnet run --project DevServerManager
```

## Use

1. Click **Add project** and choose its repository folder.
2. Edit the default API and Web commands/ports, or add custom services.
3. Click **Start project** to launch every enabled service in start-order.
4. Use **Stop project** to terminate its tracked process trees, wait up to five seconds for exit, then stop any listeners still using that project's configured service ports. Other configured ports are not inspected.

All app data is portable. On first launch, the app creates `dev-server-manager.db` beside `DevServerManager.exe`; it stores registered projects, color mode, and running-server group expansion in that SQLite database. The initial database seeds the theme to `Device` automatically. Move the executable folder with its database to carry the complete configuration with it; no AppData files are required.

Use `{port}`, `{webPort}`, `{apiPort}`, or `{projectFolder}` in commands and browser URLs. A project must use distinct ports for enabled services.

Templates are selected in the service grid and automatically fill a command, which remains editable. Press `Ctrl+P` to preview the fully expanded command, `Ctrl+T` to re-apply the selected template, or `Ctrl+I` to safely import one or more existing `.bat` launchers. Import reads commands and ports but never executes the batch files.

Right-click a service for individual Start, Stop, Restart, and command-preview diagnostics. Press `Ctrl+F` to filter the activity log; submit a blank value to clear the filter. A server that exits with a non-zero code is shown as **Failed**, and its exit code is recorded in the log.

## Appearance

Use the **Color mode** selector in the lower-left sidebar to choose **Light**, **Dark**, or **Device**. Device mode follows the current Windows app theme preference when the manager starts. The choice is stored in the portable `dev-server-manager.db` alongside the executable.

## Managed Node Servers

The **Managed Node Servers** tab shows every configured Node-family service (Node, npm, npx, Nx, and Angular CLI) across all registered projects. It includes its project, service name, port, source location, command, and current status. Start or stop a service directly from that page.

Use **Refresh** on the Running servers page to rescan external Node processes. Source groups start collapsed and remember which ones you open, including after restarting the app. The project list and server groups scroll under the pointer, and selecting a project returns to its details page.

The tab also discovers external `node.exe` processes running on the machine. For each process, it shows the Windows command line and attempts to match it to a registered project folder; this makes the source visible before you act. Stopping an external process requires a confirmation showing its PID, source location, and exact command. If a source folder was identified, the stopped process remains listed and can be started again with its original command.

Exiting Dev Server Manager runs the same project stop cleanup, including the five-second wait and configured-port listener check. Closing its window sends it to the tray; use **Exit** there to shut it down. Starting the app a second time brings the already-running window to the foreground instead of opening another copy.
