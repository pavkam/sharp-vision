# TimeInput

## TimeInput contract

`TimeInput` displays a formatted time with inline segment editing.

Use `TimeStep` to configure the minute increment used by Up/Down while the
minute segment is active. It defaults to one minute and accepts positive whole
minutes. `Use24HourFormat`, `ShowSeconds`, and `AllowNull` remain independent
basic display and editing options.

## API

| Member                       | Default                                  | Contract                                                 |
| ---------------------------- | ---------------------------------------- | -------------------------------------------------------- |
| `Value`                      | current local time                       | Nullable committed time clamped to the inclusive bounds. |
| `AllowNull`                  | `true`                                   | Allows Delete or Backspace to clear the value.           |
| `Use24HourFormat`            | `true`                                   | Selects 24-hour or AM/PM segments.                       |
| `ShowSeconds`                | `false`                                  | Adds the seconds segment.                                |
| `TimeStep`                   | one minute                               | Positive whole-minute increment for the minute segment.  |
| `MinimumTime`, `MaximumTime` | `TimeOnly.MinValue`, `TimeOnly.MaxValue` | Ordered inclusive bounds that repair the current value.  |
| `ValueChanged`               | no subscribers                           | Raised after a committed value transition.               |

## Example

![The TimeInput control rendered in the live showcase](../../images/controls/time-input.png)

```csharp
var timeInput = new TimeInput { TimeStep = TimeSpan.FromMinutes(15) };
```

## Expected behavior

| Layer       | Required evidence                                                                               |
| ----------- | ----------------------------------------------------------------------------------------------- |
| Unit        | Defaults, bounds, null policy, time-step validation, segment edits, and event order.            |
| Surface     | 12/24-hour formats, optional seconds, active segment, focus, disabled state, and tiny clipping. |
| Integration | Keyboard and pointer segment selection through mounted routed input.                            |
