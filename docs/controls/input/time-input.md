# TimeInput

## Overview

`TimeInput` displays a formatted time with inline segment editing.

`TimeStep` configures the minute increment that Up/Down applies while the minute
segment is active. It defaults to one minute and accepts positive whole minutes.
`Use24HourFormat`, `ShowSeconds`, and `AllowNull` are independent display and
editing options.

## API

| Member                       | Default                                  | Description                                                   |
| ---------------------------- | ---------------------------------------- | ------------------------------------------------------------- |
| `Value`                      | current local time                       | The nullable committed time, clamped to the inclusive bounds. |
| `AllowNull`                  | `true`                                   | Allows Delete or Backspace to clear the value.                |
| `Use24HourFormat`            | `true`                                   | Selects 24-hour or AM/PM segments.                            |
| `ShowSeconds`                | `false`                                  | Adds the seconds segment.                                     |
| `TimeStep`                   | one minute                               | The positive whole-minute increment for the minute segment.   |
| `MinimumTime`, `MaximumTime` | `TimeOnly.MinValue`, `TimeOnly.MaxValue` | Ordered inclusive bounds that repair the current value.       |
| `ValueChanged`               | no subscribers                           | Raised after a committed value transition.                    |

## Example

![The TimeInput control rendered in the live showcase](../../images/controls/time-input.png)

```csharp
var timeInput = new TimeInput { TimeStep = TimeSpan.FromMinutes(15) };
```

## Expected behavior

| Layer       | Observable evidence                                                                                                       |
| ----------- | ------------------------------------------------------------------------------------------------------------------------- |
| Unit        | Defaults, bounds, the null policy, time-step validation, segment edits, and event order behave as documented.             |
| Surface     | The 12- and 24-hour formats, optional seconds, active segment, focus, disabled state, and tiny clipping render correctly. |
| Integration | Keyboard and pointer segment selection work through mounted routed input.                                                 |
