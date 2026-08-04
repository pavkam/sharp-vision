# TimeInput

## Overview

`TimeInput` displays a formatted time with inline segment editing.

`TimeStep` configures the minute increment that Up/Down applies while the minute
segment is active. It defaults to one minute and accepts positive whole minutes.
`Use24HourFormat`, `ShowSeconds`, and `AllowNull` are independent display and
editing options.

`Culture` localizes the rendered time separator, the AM/PM designator text, and
each numeric segment's digit glyphs. It defaults to
`CultureInfo.InvariantCulture`, so out-of-the-box rendering never depends on the
host operating system's locale; set it explicitly to localize the field. The
segment order itself - hour, minute, optionally second, optionally an AM/PM
designator - stays controlled by `Use24HourFormat` and `ShowSeconds` rather than
a culture's time pattern, since those two properties are already the field's own
explicit structural contract. `TimeInput` shares its active-segment navigation,
digit-entry buffering, and pointer hit-testing engine with
[`DateInput`](date-input.md) and [`DateTimeInput`](date-time-input.md); see
issue 69. Only the calendar/clock arithmetic for each control's own value type
differs.

## API

| Member                       | Default                                  | Description                                                      |
| ---------------------------- | ---------------------------------------- | ---------------------------------------------------------------- |
| `Value`                      | current local time                       | The nullable committed time, clamped to the inclusive bounds.    |
| `AllowNull`                  | `true`                                   | Allows clearing; disabling it repairs a null value.              |
| `Culture`                    | `CultureInfo.InvariantCulture`           | Localizes the time separator, AM/PM designator text, and digits. |
| `Use24HourFormat`            | `true`                                   | Selects 24-hour or AM/PM segments.                               |
| `ShowSeconds`                | `false`                                  | Adds the seconds segment.                                        |
| `TimeStep`                   | one minute                               | The positive whole-minute increment for the minute segment.      |
| `MinimumTime`, `MaximumTime` | `TimeOnly.MinValue`, `TimeOnly.MaxValue` | Ordered inclusive bounds that repair the current value.          |
| `ValueChanged`               | no subscribers                           | Raised after a committed value transition.                       |

## Example

![The TimeInput control rendered in the live showcase](../../images/controls/time-input.png)

```csharp
var timeInput = new TimeInput { TimeStep = TimeSpan.FromMinutes(15) };
```

## Expected behavior

| Layer       | Observable evidence                                                                                                                                                       |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Unit        | Defaults, bounds, the null policy, time-step validation, segment edits, and event order behave as documented.                                                             |
| Surface     | The 12- and 24-hour formats, optional seconds, active segment, focus, disabled state, tiny clipping, and non-invariant `Culture` separators/designators render correctly. |
| Integration | Keyboard and pointer segment selection work through mounted routed input.                                                                                                 |
