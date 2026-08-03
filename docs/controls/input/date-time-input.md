# DateTimeInput

## Overview

`DateTimeInput` combines date and time segment editing with an optional Calendar
popup.

The time portion supports `TimeStep`, a positive whole-minute increment that
Up/Down applies while the minute segment is active. The embedded calendar
exposes the same basic date navigation options as `Calendar`. Selecting a date
preserves the current time and `DateTimeKind`.

## API

| Member                         | Default                                         | Description                                                                                                                                                                               |
| ------------------------------ | ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Value`                        | current local date and time                     | The nullable value, clamped to the inclusive bounds.                                                                                                                                      |
| `AllowNull`                    | `true`                                          | Allows Delete or Backspace to clear the value; disabling it repairs a null value.                                                                                                         |
| `Culture`                      | current Gregorian culture or invariant fallback | Sets the popup calendar's month/day names and navigation only; assigned cultures must use a Gregorian calendar. The typed field's segment order, digits, and separators are always fixed. |
| `Use24HourFormat`              | `true`                                          | Selects 24-hour or AM/PM segments.                                                                                                                                                        |
| `ShowSeconds`                  | `false`                                         | Adds the seconds segment.                                                                                                                                                                 |
| `TimeStep`                     | one minute                                      | The positive whole-minute increment for the minute segment.                                                                                                                               |
| `MinimumValue`, `MaximumValue` | `DateTime.MinValue`, `DateTime.MaxValue`        | Ordered inclusive bounds that repair the current value.                                                                                                                                   |
| `IsOpen`                       | `false`                                         | Opens or closes the retained Calendar popup.                                                                                                                                              |
| `DropDownGlyph`                | code-owned disclosure glyph                     | The validated one-cell indicator; `ResetDropDownGlyph()` restores it.                                                                                                                     |
| `ValueChanged`                 | no subscribers                                  | Raised after a committed value transition.                                                                                                                                                |

## Example

![The DateTimeInput control rendered in the live showcase](../../images/controls/date-time-input.png)

```csharp
var dateTimeInput = new DateTimeInput { TimeStep = TimeSpan.FromMinutes(15) };
```

## Expected behavior

| Layer       | Observable evidence                                                                                                               |
| ----------- | --------------------------------------------------------------------------------------------------------------------------------- |
| Unit        | Defaults, bounds, culture, the null policy, the time step, glyph validation, segment edits, and event order behave as documented. |
| Surface     | Date and time formats, the active segment, the popup, focus, the disabled state, and tiny clipping render correctly.              |
| Integration | Keyboard and pointer editing, Calendar selection, light dismiss, and focus restoration work end to end.                           |
