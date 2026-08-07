# DateInput

## Overview

`DateInput` displays a formatted date, supports inline segment editing, and
offers an optional Calendar popup.

While the popup is open, arrow, Page Up, Page Down, Home, and End keys navigate
the calendar. Enter or Space commits its active date, and Escape closes the
popup without changing the value.

Custom date formats follow .NET quoting and escaping rules. Date letters inside
quoted or escaped literals remain display text and never become editable
segments. When a format contains no editable segments, segment-editing keys are
left unhandled while popup and clearing commands remain available.

`DateInput` shares its active-segment navigation, digit-entry buffering, and
pointer hit-testing engine with [`TimeInput`](time-input.md) and
[`DateTimeInput`](date-time-input.md); see issue 69. Each control keeps its own
calendar/clock arithmetic and pattern (`ResolveDatePattern` here) on top of that
shared engine.

## API

| Member                             | Default                                         | Description                                                   |
| ---------------------------------- | ----------------------------------------------- | ------------------------------------------------------------- |
| `Value`                            | current local date                              | The nullable committed date, clamped to the inclusive bounds. |
| `AllowNull`                        | `true`                                          | Allows clearing the value; disabling it repairs a null value. |
| `Culture`                          | current Gregorian culture or invariant fallback | Supplies the segment order and formatting.                    |
| `Format`                           | `"d"`                                           | A non-null, non-empty date format string.                     |
| `Minimum`, `Maximum`               | `DateOnly.MinValue`, `DateOnly.MaxValue`        | Ordered inclusive bounds that repair the current value.       |
| `DropDownHeight`                   | `10` cells                                      | The positive maximum visible calendar height.                 |
| `Opened`                           | `false`                                         | Opens or closes the retained Calendar popup.                  |
| Themed disclosure glyph            | `InputStyle.DropDownGlyph` (`▼`)                | Authored once for every drop-down input via `styles.input`.   |
| `ValueChanged`                     | no subscribers                                  | Raised after a committed value transition.                    |
| `DropDownOpened`, `DropDownClosed` | no subscribers                                  | Raised after the Calendar popup opens or closes.              |

## Example

![The DateInput control rendered in the live showcase](../../images/controls/date-input.png)

```csharp
var dateInput = new DateInput();
```

## Expected behavior

| Layer       | Observable evidence                                                                                                                                   |
| ----------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| Unit        | Defaults, bounds, the null policy, format and culture validation, segment editing, and event order behave as documented.                              |
| Surface     | The field and active segment render in their exact cells, the popup places correctly, focused and disabled states apply, and tiny bounds clip safely. |
| Integration | Keyboard and pointer editing, Calendar selection, light dismiss, and focus restoration work end to end.                                               |
