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

The app stores registered projects, color mode, and running-server group expansion in SQLite. By default, the database is `%LOCALAPPDATA%\DevServerManager\dev-server-manager.db`. With `portable.flag` beside `DevServerManager.exe` (included in the portable ZIP), the database stays beside the executable. Move the complete portable folder with its database to carry configuration with it. The initial database seeds the theme to `Device` automatically.

For an earlier build's database beside the EXE, add `portable.flag` to keep that location or let the new build import it into AppData once, when no AppData database exists. The original remains available. See the repository's `docs/releases.md` for installation and upgrades.

Use `{port}`, `{webPort}`, `{apiPort}`, or `{projectFolder}` in commands and browser URLs. A project must use distinct ports for enabled services.

Templates are selected in the service grid and automatically fill a command, which remains editable. Press `Ctrl+P` to preview the fully expanded command, `Ctrl+T` to re-apply the selected template, or `Ctrl+I` to safely import one or more existing `.bat` launchers. Import reads commands and ports but never executes the batch files.

Right-click a service for individual Start, Stop, Restart, and command-preview diagnostics. Press `Ctrl+F` to filter the activity log; submit a blank value to clear the filter. A server that exits with a non-zero code is shown as **Failed**, and its exit code is recorded in the log.

Use **Refresh status** on a project page to check each configured TCP port. The service grid shows the current listener PID(s): **Running** for a tracked service with a listener, **Listening** for a listener outside the manager's tracked process, **Starting** for a tracked process without a listener yet, or **Stopped** when neither is present.

Environment variable rows can target **All services** or one service chosen from the **Service** dropdown. Existing project variables continue to apply to all services. A service-specific variable with the same name overrides the all-services value for that service.

## Appearance

Use the **Color mode** selector in the lower-left sidebar to choose **Light**, **Dark**, or **Device**. Device mode follows the current Windows app theme preference when the manager starts. The choice is stored in the configuration database for the current installation mode.

## Managed Node Servers

The **Managed Node Servers** tab shows every configured Node-family service (Node, npm, npx, Nx, and Angular CLI) across all registered projects. It includes its project, service name, port, source location, command, and current status. Start or stop a service directly from that page.

Use **Refresh** on the Running servers page to rescan external Node processes. Source groups start collapsed and remember which ones you open, including after restarting the app. The project list and server groups scroll under the pointer, and selecting a project returns to its details page.

Enter a TCP port and click **Scan port** to add a **Scanned port** group showing the processes listening there, including non-Node processes. **Stop** confirms the PID and rechecks that it still owns the port before terminating its process tree. A stopped row can be started again: registered services use their saved project command and environment variables; other processes prompt for a launch command and working directory, prefilled from process details when available. Scans are kept for the current app session, and the Running servers **Refresh** button rescans the selected port. Processes launched through a scan without a registered project are independent of project stop and should be stopped from their scanned row.

The tab also discovers external `node.exe` processes running on the machine. For each process, it shows the Windows command line and attempts to match it to a registered project folder; this makes the source visible before you act. Stopping an external process requires a confirmation showing its PID, source location, and exact command. If a source folder was identified, the stopped process remains listed and can be started again with its original command.

Exiting Dev Server Manager runs the same project stop cleanup, including the five-second wait and configured-port listener check. Closing its window sends it to the tray; use **Exit** there to shut it down. Starting the app a second time brings the already-running window to the foreground instead of opening another copy.
