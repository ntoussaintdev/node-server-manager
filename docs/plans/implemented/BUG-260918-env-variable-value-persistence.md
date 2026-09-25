# BUG-260918 — Environment-variable values do not persist

## Status

Implemented.

## Problem

In `EnvironmentVariableDefinition`, the `Value` property setter used the setter parameter named `value` instead of the private backing field. The entered value was therefore discarded when the DataGrid row lost focus, and the project save wrote an empty value.

## Implemented fix

1. Renamed the private backing field to an unambiguous name such as `_value`.
2. Updated the `Value` setter to call `Set(ref _value, value)`.

## Verification record

- An entered environment-variable value remains visible after deselecting its row.
- The value persists across an application restart.
- Project-level environment variables are injected into newly started project services.
