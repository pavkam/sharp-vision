# DateTimeInput

## Overview

`DateTimeInput` combines date and time segment editing with an optional Calendar
popup.

The time portion supports `TimeStep`, a positive whole-minute increment that
Up/Down applies while the minute segment is active. The embedded calendar
exposes the same basic date navigation options as `Calendar`. Selecting a date
preserves the current time and `DateTimeKind`.

`DateTimeInput` shares its active-segment navigation, digit-entry buffering, and
pointer hit-testing engine with [`DateInput`](date-input.md) and
[`TimeInput`](time-input.md); see issue 69. `Culture` now drives both the popup
calendar's month/day names _and_ the typed field's own date segment order,
widths, and separators - the same way `DateInput.Culture` does, deriving the
layout from `DateTimeFormatInfo.ShortDatePattern` - so a German culture, for
example, renders day before month with a period separator. The time portion
keeps the fixed hour/minute/[second]/[AM-PM] structure `Use24HourFormat` and
`ShowSeconds` already select, localizing only its separator, AM/PM designator
text, and digit glyphs. Set `Format` to a custom combined pattern (for example
`"yyyy/MM/dd hh:mm tt"`) to override that structure directly; pair a 12-hour
`h`/`hh` hour token with a `t`/`tt` AM/PM designator token for correct 12-hour
clamping, since a 12-hour hour token without a designator is treated as a
24-hour segment for editing purposes.

## API

| Member                             | Default                                  | Description                                                                                                                                 |
| ---------------------------------- | ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `Value`                            | current local date and time              | The nullable value, clamped to the inclusive bounds.                                                                                        |
| `AllowNull`                        | `true`                                   | Allows Delete or Backspace to clear the value; disabling it repairs a null value.                                                           |
| `Culture`                          | `CultureInfo.InvariantCulture`           | Localizes both the popup calendar and the typed field's date order, separators, designator text, and digits; must use a Gregorian calendar. |
| `Use24HourFormat`                  | `true`                                   | Selects 24-hour or AM/PM segments.                                                                                                          |
| `ShowSeconds`                      | `false`                                  | Adds the seconds segment.                                                                                                                   |
| `Format`                           | `null`                                   | A custom combined pattern overriding the derived segment order and count.                                                                   |
| `TimeStep`                         | one minute                               | The positive whole-minute increment for the minute segment.                                                                                 |
| `Minimum`, `Maximum`               | `DateTime.MinValue`, `DateTime.MaxValue` | Ordered inclusive bounds that repair the current value.                                                                                     |
| `DropDownHeight`                   | `10` cells                               | The positive maximum visible calendar height.                                                                                               |
| `Opened`                           | `false`                                  | Opens or closes the retained Calendar popup.                                                                                                |
| Themed disclosure glyph            | `InputStyle.DropDownGlyph` (`▼`)         | Authored once for every drop-down input through a theme's `styles.input` section.                                                           |
| `ValueChanged`                     | no subscribers                           | Raised after a committed value transition.                                                                                                  |
| `DropDownOpened`, `DropDownClosed` | no subscribers                           | Raised after the Calendar popup opens or closes.                                                                                            |

## Example

![The DateTimeInput control rendered in the live showcase](../../images/controls/date-time-input.png)

```csharp
var dateTimeInput = new DateTimeInput { TimeStep = TimeSpan.FromMinutes(15) };
```

## Expected behavior

| Layer       | Observable evidence                                                                                                                                               |
| ----------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Unit        | Defaults, bounds, culture, the null policy, the time step, glyph validation, segment edits, and event order behave as documented.                                 |
| Surface     | Date and time formats, culture-driven segment order and separators, the active segment, the popup, focus, the disabled state, and tiny clipping render correctly. |
| Integration | Keyboard and pointer editing, Calendar selection, light dismiss, and focus restoration work end to end.                                                           |
