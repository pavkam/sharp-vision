# DateInput

## Overview

`DateInput` displays a formatted date, supports inline segment editing, and
offers an optional Calendar popup.

## API

| Member                       | Default                                         | Description                                                   |
| ---------------------------- | ----------------------------------------------- | ------------------------------------------------------------- |
| `Value`                      | current local date                              | The nullable committed date, clamped to the inclusive bounds. |
| `AllowNull`                  | `true`                                          | Allows clearing the value; disabling it repairs a null value. |
| `Culture`                    | current Gregorian culture or invariant fallback | Supplies the segment order and formatting.                    |
| `Format`                     | `"d"`                                           | A non-null, non-empty date format string.                     |
| `MinimumDate`, `MaximumDate` | `DateOnly.MinValue`, `DateOnly.MaxValue`        | Ordered inclusive bounds that repair the current value.       |
| `DropDownHeight`             | `10` cells                                      | The positive maximum visible calendar height.                 |
| `IsOpen`                     | `false`                                         | Opens or closes the retained Calendar popup.                  |
| `ValueChanged`               | no subscribers                                  | Raised after a committed value transition.                    |

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
