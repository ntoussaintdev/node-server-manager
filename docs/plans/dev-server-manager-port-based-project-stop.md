# Plan: Stop project services by process and port

## Status

Implemented. Build and local IPv4/IPv6 listener cleanup smoke test verified.

## Goal

When a project is stopped, Dev Server Manager should first terminate the
process trees it started, wait up to five seconds for them to exit, then stop
any process still listening on that project's configured service ports. This
catches detached or orphaned server processes that survive the normal stop.

The existing 10-second wait used by service restart should also become five
seconds so stop-related waits use one consistent limit.

## Current behavior

- `ServerProcessManager.StopProject` calls `Process.Kill(entireProcessTree: true)`
  for each tracked service, then immediately marks the project stopped.
- Project stop does not wait for process exit and does not inspect configured
  ports afterward.
- `RestartServiceAsync` waits up to 10 seconds for its tracked process to exit.
- `CheckPorts` detects occupied TCP ports but does not identify the process
  that owns a listening port.
- The Stop project UI and application shutdown call the synchronous stop APIs.

## Implementation plan

1. Define one five-second timeout for managed process stops. Use it in service
   restart and in the project stop flow; update the restart error message to
   report five seconds.

2. Add `StopProjectAsync`. At the start of the operation, snapshot the
   project's distinct, valid configured service ports and tracked service
   processes. Request termination of each tracked process tree, preserving
   current service stop logging and status notifications.

3. Await the tracked process exits asynchronously for up to five seconds.
   Handle already-exited processes and per-process failures without abandoning
   cleanup for other services. Keep the UI responsive during this wait.

4. After the wait, query TCP listening endpoints on the captured project
   ports and resolve each endpoint to its owning PID. Use the Windows IP Helper
   TCP owner-PID tables for IPv4 and IPv6, with listener state filtering. Do
   not parse localized `netstat` output or infer a PID from the port-only
   `IPGlobalProperties` listener list.

5. De-duplicate the discovered PIDs, then check that each process still exists
   and terminate its process tree. Log each fallback attempt with its port and
   PID. A PID may own more than one project port, so kill it only once. Log
   inaccessible, exited, and failed processes and continue with other PIDs.

6. Query the captured ports once more after issuing fallback kills. If a
   listener remains, log its port and PID when available. Finish the stop
   operation with a useful warning rather than waiting indefinitely. Only
   captured project ports may be used for this fallback cleanup.

7. Update the callers:

   - Make `StopProject_Click` await `StopProjectAsync` and retain its current
     progress and error reporting.
   - Replace `StopAll` with an asynchronous equivalent and use it for orderly
     application shutdown, so closing the app runs the same port cleanup.
   - Keep individual service Stop behavior unchanged; the requested fallback
     applies when stopping a project.

8. Update `DevServerManager/README.md` to describe the process-tree stop,
   five-second wait, and cleanup of remaining listeners on the project's
   configured ports.

## Verification plan

- Test the listener lookup and stop orchestration for no remaining listener,
  a surviving listener, duplicate PIDs across ports, inaccessible processes,
  failed kills, and a listener that remains after cleanup.
- Confirm only configured project ports are considered and both IPv4 and IPv6
  listeners are found.
- Confirm service restart uses the five-second timeout and matching error
  message.
- Manually stop a project whose launcher leaves a detached child listening on
  a configured port. Confirm the child is terminated after the wait and the
  activity log reports the fallback.
- Confirm a listener on a port outside the project's configuration is not
  affected, and app shutdown completes the same project-stop cleanup.
- Build the WPF app and run its test suite after implementation.

## Acceptance criteria

- Project stop terminates tracked process trees, waits up to five seconds, and
  then stops remaining listeners on the project's captured configured ports.
- App shutdown awaits the same cleanup, and the UI remains responsive during
  the wait.
- The fallback handles IPv4 and IPv6, kills each discovered PID at most once,
  logs attempts and failures, and reports listeners that remain.
- Listener cleanup never targets ports outside the stopped project's
  configuration.
- Restart stop waits and its user-facing timeout message both use five
  seconds.
