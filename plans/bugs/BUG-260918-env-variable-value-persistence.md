# BUG-260918 — Environment-variable values do not persist

## Status

Planned — implementation deferred.

## Problem

In `EnvironmentVariableDefinition`, the `Value` property setter uses the setter parameter named `value` instead of the private backing field. The entered value is therefore discarded when the DataGrid row loses focus, and the project save writes an empty value.

## Fix plan

1. Rename the private backing field to an unambiguous name such as `_value`.
2. Update the `Value` setter to call `Set(ref _value, value)`.
3. Verify that editing a value survives row selection changes.
4. Restart the app and confirm that the value reloads from the portable SQLite project definition.
5. Start a configured service and verify the resulting process receives the saved value.

## Acceptance criteria

- An entered environment-variable value remains visible after deselecting its row.
- The value persists across an application restart.
- Project-level environment variables are injected into newly started project services.
