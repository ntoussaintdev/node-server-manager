# Dev Server Manager — Small UI and navigation tweaks

## Status

Implemented. Build and app startup verified; multi-monitor and mixed-DPI checks remain manual.

## Scope

Three usability fixes for `DevServerManager`, implemented alongside the running-server refresh and pointer scrolling improvements.

## 1. Correct custom-chrome maximization bounds

### Observed behavior

The attached screenshot shows the app in its maximized state but not filling the usable display: a visible vertical strip remains on the right and a horizontal strip remains at the bottom. The custom title bar reaches the left and top edges, so the resulting presentation looks like a partially sized window rather than a true maximized window.

### Likely cause

`Maximize_Click` sets `MaxWidth` and `MaxHeight` to `SystemParameters.WorkArea` before setting `WindowState = Maximized`. Those constraints can conflict with WPF's maximized sizing when custom `WindowChrome` is in use, particularly because work-area dimensions are not equivalent to the window's expected maximized bounds on the current monitor.

### Implementation plan

1. Reproduce the issue on the monitor shown in the screenshot, including the custom title bar and taskbar configuration.
2. Remove or revise the manual `MaxWidth`/`MaxHeight` work-area constraint so the window manager owns maximized bounds; if custom chrome still requires a workaround, calculate monitor-specific bounds with the appropriate DPI-aware WPF/Win32 APIs rather than global `SystemParameters.WorkArea` dimensions.
3. Keep the existing maximize/restore toggle and glyph update behavior intact.
4. Verify maximize, restore, minimize, and resize on each connected monitor and at differing DPI scales.

### Acceptance criteria

- Maximizing fills the available work area without the screenshot's right or bottom gaps.
- The window does not cover the taskbar unless the operating system's normal maximization behavior would do so.
- Restore returns to the prior normal-size window and the custom title-bar controls remain clickable.

## 2. Preserve running-server group expansion state

### Observed behavior

The Running servers page binds every `Expander` with `IsExpanded="True"`. Every group therefore opens initially, and rebuilding `ServerGroups` after server actions also loses a user's collapsed groups.

### Implementation plan

1. Add expansion state to the running-server group view model, keyed by a stable group identity such as its source location.
2. Default groups to collapsed when there is no remembered state.
3. Bind the `Expander` two-way to that group state instead of assigning `IsExpanded="True"`.
4. Before `RefreshManagedServices` rebuilds groups, retain the current states and apply them to the replacement group instances.
5. Persist the state in the existing UI-settings store so it survives app restarts, while safely ignoring obsolete source locations.

### Acceptance criteria

- Opening Running servers starts with all groups collapsed on first use.
- A group a user expands or collapses retains that choice after refreshes, including start/stop actions and page navigation.
- The most recently chosen expansion state is restored after restarting the app.

## 3. Make project-sidebar selection navigate to the project page

### Observed behavior

When a user is on Running servers, selecting a project in the sidebar updates selection but leaves the Running servers page visible. The separate Managed projects button is then needed solely to return to the selected project's page.

### Implementation plan

1. Update the project-list selection handler to switch `MainTabs` to the project page and update the active navigation styling when a project is selected by the user.
2. Preserve normal initialization behavior so the initially assigned project does not produce unwanted navigation or duplicate refresh work.
3. Remove the redundant Managed projects button from the sidebar and delete its click handler and related style/state references.
4. Keep Running servers as the explicit navigation destination, including its refresh behavior.

### Acceptance criteria

- Selecting any project sidebar card from Running servers immediately shows that project's page.
- Selecting a project while already on the project page continues to update that page and its log normally.
- The sidebar has no Managed projects button, and the active styling remains correct for project selection and Running servers.
