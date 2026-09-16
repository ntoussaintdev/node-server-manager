# Dev Server Manager — Specification and Delivery Plan

**Status:** Active development — Stages 1–4 implemented; Stage 5 pending  
**Last updated:** 2026-09-14

## 1. Purpose

Dev Server Manager is a local Windows desktop application for running several development projects simultaneously. It is aimed at Angular/Nx projects that commonly consist of a static web application and an API, while also supporting any project that can be launched from a command line.

The application manages processes and their ports without requiring changes to a project's committed source files.

## 2. Product decisions

| Area | Decision |
| --- | --- |
| Platform | Windows desktop application written in C# |
| UI framework | WPF on .NET 10 |
| Primary operation | Start Project / Stop Project starts or stops every enabled service in that project |
| Individual services | Available as advanced controls for diagnosis and recovery, not the default workflow |
| Configuration | Local SQLite database, outside project repositories by default |
| Initial audience | One developer, local machine, local development servers |
| Project source changes | Not required for basic port assignment and launching |

## 3. Core user workflow

1. Add a project and choose a launch template or enter custom commands.
2. Assign a distinct web and API port, as applicable.
3. Click **Start Project**.
4. The manager validates the folder, checks port availability, then starts all enabled services in dependency order.
5. It shows each service's status and combined or per-service logs.
6. Click **Stop Project** to stop every process tree started for that project and free its ports.

For a typical Angular + API project, Start Project starts both web and API together. Individual Start/Stop Web and Start/Stop API controls remain available only when needed. :codex-annotation{index="1"}

## 4. Scope

### Included in the first release

- Register multiple projects with a name and working directory.
- Define one or more services per project.
- Start and stop all enabled services as one project operation.
- Assign and validate ports.
- Capture standard output and standard error into an application log view.
- Open a running web service in the default browser.
- Persist configuration locally in SQLite.
- Provide Nx, Angular CLI, npm, and custom-command templates.

### Explicitly out of scope for the first release

- Editing application source code, `angular.json`, Nx configuration, or API code.
- Remote server management, Docker orchestration, deployment, or authentication.
- Sharing a running local server over the internet.
- Replacing full terminal applications or IDE task runners.
- Automatically resolving port conflicts by changing a project's port without user approval.

## 5. Project and service model

Each project contains one or more services. A service is enabled or disabled independently, but enabled services are included by default in Start Project and Stop Project.

```json
{
  "name": "Chartchemy",
  "workingDirectory": "C:\\Projects\\chartchemy",
  "services": [
    {
      "name": "API",
      "template": "nx-api",
      "command": "npx nx serve api --port {apiPort}",
      "port": 3001,
      "startOrder": 10,
      "enabled": true
    },
    {
      "name": "Web",
      "template": "nx-web",
      "command": "npx nx serve web --port {webPort} --proxy-config proxy.conf.local.json",
      "port": 4201,
      "startOrder": 20,
      "enabled": true,
      "browserUrl": "http://localhost:{webPort}"
    }
  ]
}
```

Supported placeholders initially: `{projectFolder}`, `{webPort}`, `{apiPort}`, and `{port}`.

## 6. Templates

Templates prefill a service definition but never lock it. Users can edit the resulting command, environment variables, working directory, and port.

| Template | Default command |
| --- | --- |
| Nx web | `npx nx serve {target} --port {port}` |
| Nx API | `npx nx serve {target} --port {port}` |
| Angular CLI | `npx ng serve --port {port}` |
| npm development script | `npm run dev -- --port {port}` |
| Custom command | User-supplied command |

The initial template implementation only fills commands. Project-specific proxy setup remains the project's responsibility, though a later release may offer a non-destructive helper for it.

## 7. UI requirements

### Main screen

- List projects and their aggregate state: stopped, starting, running, degraded, or failed.
- Primary actions: **Start Project**, **Stop Project**, **Restart Project**, and **Open Web**.
- Show service statuses and assigned ports.
- Provide an expandable log area.

### Project editor

- Project name and working-directory picker.
- Service list with add, edit, remove, enable, and reorder controls.
- Template selector and editable command fields.
- Port, browser URL, environment-variable, and start-order fields.
- Validate configuration before saving.

### Service detail and diagnostics

- Per-service Start, Stop, and Restart.
- Clear indication of a port conflict, exited process, invalid directory, or failed command.
- Filterable standard-output and standard-error logs.

## 8. Process and safety requirements

- Launch commands with the selected working directory and explicitly configured environment variables.
- Store the started process ID and command metadata for each service.
- Stop the entire child-process tree created by a service, not just the command shell.
- Check configured ports before starting; report the process using an occupied port where practical.
- Never terminate an unrelated process merely because it uses a configured port.
- Mark services as unknown after an application restart until their ownership can be safely established.
- Keep all configuration under the user's local application-data directory unless an explicit export is requested.

## 9. Staged delivery checklist

Completion means the acceptance criteria for the stage have been manually verified. Checkboxes are updated in this document as work is completed.

### Stage 0 — Specification and decisions

- [x] Define the core problem and primary user workflow.
- [x] Define Start/Stop Project as the default paired web-and-API workflow.
- [x] Define initial scope and exclusions.
- [x] Define staged delivery and completion tracking.
- [x] Confirm WPF and target .NET version before creating the solution.
- [ ] Confirm the first real project(s) to use as acceptance-test fixtures.

**Exit criteria:** The owner approves this specification and the two remaining decisions.

### Stage 1 — Application foundation

- [x] Create the C# solution and WPF shell.
- [x] Implement local SQLite configuration loading and saving, with one-time migration from the earlier local JSON file.
- [x] Implement project list and project editor.
- [x] Implement manual custom-service configuration.
- [ ] Validate missing folders, missing commands, duplicate project names, and duplicate configured ports.

**Exit criteria:** A user can add, edit, remove, persist, and reload projects and their services without launching a process.

### Stage 2 — Project lifecycle

- [x] Implement Start Project for all enabled services in configured start order.
- [x] Implement Stop Project with child-process-tree termination.
- [x] Implement Restart Project.
- [x] Implement live status transitions and error states.
- [x] Implement safe port-availability checks.

**Exit criteria:** A two-service Angular/API project starts and stops through one project action, with no orphaned server process.

### Stage 3 — Visibility and control

- [x] Capture and display per-service standard output and standard error.
- [x] Add service-level Start, Stop, and Restart diagnostics.
- [x] Add Open Web action for configured browser URLs.
- [x] Add log filtering and clear, actionable launch failures.

**Exit criteria:** A user can identify a failed service, inspect its output, and selectively restart it without affecting the healthy service.

### Stage 4 — Templates and onboarding

- [x] Add Nx web and Nx API templates.
- [x] Add Angular CLI and npm templates.
- [x] Add custom template.
- [x] Add placeholder expansion and preview of the final command.
- [x] Add an import flow for existing `.bat` launchers without executing them during import.

**Exit criteria:** A new Nx or Angular project can be registered using a template, adjusted, and launched without editing repository files.

### Stage 5 — Reliability and release readiness

- [ ] Test concurrent projects with unique web/API port pairs.
- [ ] Test process cleanup after normal stop, command failure, and application closure.
- [ ] Test persistence, invalid configuration handling, and port-conflict safety.
- [ ] Produce a short user guide and initial release package.

**Exit criteria:** The manager reliably operates at least three concurrent projects and provides understandable recovery information for expected failures.

## 10. Future candidates (not committed)

- Saved workspace profiles, such as “Start Chartchemy workspace.”
- System-tray operation.
- Project configuration import/export for teams.
- Proxy-configuration assistant that writes only a clearly user-approved local file.
- Terminal-window integration.
- Docker Compose and remote-host adapters.

## 11. Change log

| Date | Change |
| --- | --- |
| 2026-09-14 | Initial specification created. Development has not started. |
| 2026-09-14 | Retargeted to .NET 10; Stage 4 implementation checklist completed. |
| 2026-09-14 | Stage 3 implementation checklist completed. |
| 2026-09-14 | Added light, dark, and device-match visual themes with persisted selection. |
| 2026-09-14 | Added single-instance protection, shutdown cleanup, and a managed Node-server page. |
| 2026-09-15 | Second launches now activate the existing window; Node-server page can inspect and control matched external Node processes. |
