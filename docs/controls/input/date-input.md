# DateInput

## DateInput contract

`DateInput` displays a formatted date with inline segment editing and an
optional Calendar popup.

## API

| Member                       | Default                                         | Contract                                                  |
| ---------------------------- | ----------------------------------------------- | --------------------------------------------------------- |
| `Value`                      | current local date                              | Nullable committed date, clamped to the inclusive bounds. |
| `AllowNull`                  | `true`                                          | Allows clearing; disabling it repairs a null value.       |
| `Culture`                    | current Gregorian culture or invariant fallback | Supplies segment order and formatting.                    |
| `Format`                     | `"d"`                                           | Non-null, non-empty date format string.                   |
| `MinimumDate`, `MaximumDate` | `DateOnly.MinValue`, `DateOnly.MaxValue`        | Ordered inclusive bounds that repair the current value.   |
| `DropDownHeight`             | `10` cells                                      | Positive maximum visible calendar height.                 |
| `IsOpen`                     | `false`                                         | Opens or closes the retained Calendar popup.              |
| `ValueChanged`               | no subscribers                                  | Raised after a committed value transition.                |

## Example

![The DateInput control rendered in the live showcase](../../images/controls/date-input.png)

```csharp
var dateInput = new DateInput();
```

## Expected behavior

| Layer       | Required evidence                                                                           |
| ----------- | ------------------------------------------------------------------------------------------- |
| Unit        | Defaults, bounds, null policy, format/culture validation, segment editing, and event order. |
| Surface     | Exact field, active segment, popup placement, focused/disabled states, and tiny clipping.   |
| Integration | Keyboard, pointer, Calendar selection, light dismiss, and focus restoration.                |
