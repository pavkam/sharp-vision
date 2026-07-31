# DateTimeInput

## DateTimeInput contract

`DateTimeInput` combines date and time segment editing with an optional Calendar
popup.

The time portion supports `TimeStep`, a positive whole-minute increment used by
Up/Down on the minute segment. The embedded calendar also exposes the same basic
date navigation options as `Calendar`.

## API

| Member                         | Default                                         | Contract                                                                 |
| ------------------------------ | ----------------------------------------------- | ------------------------------------------------------------------------ |
| `Value`                        | current local date and time                     | Nullable value clamped to the inclusive bounds.                          |
| `AllowNull`                    | `true`                                          | Allows Delete or Backspace to clear the value.                           |
| `Culture`                      | current Gregorian culture or invariant fallback | Supplies date ordering; assigned cultures must use a Gregorian calendar. |
| `Use24HourFormat`              | `true`                                          | Selects 24-hour or AM/PM segments.                                       |
| `ShowSeconds`                  | `false`                                         | Adds the seconds segment.                                                |
| `TimeStep`                     | one minute                                      | Positive whole-minute increment for the minute segment.                  |
| `MinimumValue`, `MaximumValue` | `DateTime.MinValue`, `DateTime.MaxValue`        | Ordered inclusive bounds that repair the current value.                  |
| `IsOpen`                       | `false`                                         | Opens or closes the retained Calendar popup.                             |
| `DropDownGlyph`                | code-owned disclosure glyph                     | Validated one-cell indicator; `ResetDropDownGlyph()` restores it.        |
| `ValueChanged`                 | no subscribers                                  | Raised after a committed value transition.                               |

## Example

![The DateTimeInput control rendered in the live showcase](../../images/controls/date-time-input.png)

```csharp
var dateTimeInput = new DateTimeInput { TimeStep = TimeSpan.FromMinutes(15) };
```

## Expected behavior

| Layer       | Required evidence                                                                            |
| ----------- | -------------------------------------------------------------------------------------------- |
| Unit        | Defaults, bounds, culture, null policy, time step, glyph validation, edits, and event order. |
| Surface     | Date/time formats, active segment, popup, focus, disabled state, and tiny clipping.          |
| Integration | Keyboard, pointer, Calendar selection, light dismiss, and focus restoration.                 |
