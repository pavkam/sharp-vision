# Calendar

## Calendar contract

`Calendar` is one focusable direct-rendered control for choosing either one
Gregorian date or one inclusive Gregorian date interval. It owns one fixed
six-week month grid and remains one Tab stop; individual day faces are not child
controls.

## API

| Member                       | Default                        | Contract                                                        |
| ---------------------------- | ------------------------------ | --------------------------------------------------------------- |
| `SelectionMode`              | `CalendarSelectionMode.Select` | Selects a single-day or two-activation interval state machine.  |
| `Selection`                  | `null`                         | Nullable committed inclusive `DateInterval`.                    |
| `SelectionChanged`           | no subscribers                 | Raised after selection and related display/active state commit. |
| `Select(DateOnly)`           | —                              | Commits one selectable day.                                     |
| `Select(DateOnly, DateOnly)` | —                              | Commits one ordered selectable inclusive interval.              |
| `ClearSelection()`           | —                              | Clears the committed selection and pending interval anchor.     |
| `GoToToday()`                | —                              | Moves to an available bounded local date and reports success.   |

`CalendarSelectionMode` defines the interaction state machine:

- `Select` is the default. One activation commits a one-day `DateInterval`.
- `Interval` uses the first activation as a pending anchor and the second as the
  opposite endpoint. Endpoints are ordered before the interval commits.

`DateInterval` is an immutable inclusive value with `Start`, `End`, `Days`,
`Contains`, and `Intersects`. Its constructor rejects an end before the start. A
one-day interval is valid.

`Selection` stores the committed value as `DateInterval?` in both modes.
`Select(DateOnly)`, `Select(DateOnly, DateOnly)`, and `ClearSelection()` are
atomic convenience methods over the same property. Select mode rejects a
multi-day interval. Every selection rejects dates outside `MinimumDate` through
`MaximumDate` or an interval containing a blocked date before changing state.

`SelectionChanged` runs after the property and all related active/display state
commit. `CalendarSelectionChangedEventArgs` exposes `PreviousSelection` and
`Selection`; no-op assignments are silent. A programmatic selection or clear
cancels any pending interval anchor before publishing the committed state.
Changing `SelectionMode` clears the committed selection and pending interval
anchor so state from one interaction contract never leaks into the other.

In interval mode, an anchor renders a provisional range through the active
keyboard date or the directly hovered selectable date. Preview does not change
`Selection` or raise `SelectionChanged`. A second endpoint whose inclusive span
contains a blocked date is ignored and preserves the anchor. After a completed
interval, the next activation clears it and starts a fresh anchor.

## Dates, bounds, and culture

| Member           | Default                          | Purpose                                              |
| ---------------- | -------------------------------- | ---------------------------------------------------- |
| `DisplayMonth`   | first day of current local month | Chooses the rendered month; assigned dates normalize |
| `ActiveDate`     | current local date               | Reports the keyboard cursor                          |
| `MinimumDate`    | `DateOnly.MinValue`              | Sets the inclusive lower selection bound             |
| `MaximumDate`    | `DateOnly.MaxValue`              | Sets the inclusive upper selection bound             |
| `Culture`        | current Gregorian culture        | Supplies month text, weekday names, and week start   |
| `FirstDayOfWeek` | culture default                  | Overrides the first weekday column                   |
| `BlockedDates`   | empty                            | Owns normalized unavailable inclusive ranges         |

The parameterless constructor captures local date and culture once; the control
does not advance itself at midnight. `Culture` requires an active
`GregorianCalendar`, because mixing a different calendar system's month labels
with `DateOnly` day geometry would display false dates. Changing culture
rerenders localized month and weekday text without changing selection.

`FirstDayOfWeek` overrides the culture-provided first weekday without changing
the selected date. `GoToToday()` moves `ActiveDate` and `DisplayMonth` to the
local current date when today is in bounds and not blocked; it returns `false`
when today is unavailable.

Bounds must remain ordered. Tightening them clears an invalid selection or
anchor and repairs `ActiveDate` to the nearest selectable value. Display and
movement remain safe at `DateOnly.MinValue` and `DateOnly.MaxValue`.

## Blocked dates

`CalendarBlockedDateCollection` exposes `Block(DateOnly)`,
`Block(DateInterval)`, `Unblock(DateOnly)`, `Unblock(DateInterval)`,
`Contains(DateOnly)`, and `Clear()`. It stores ascending non-touching ranges:
overlapping or adjacent additions coalesce, while unblocking can trim or split
one range. It never expands a long interval into per-date storage.

Blocked dates remain visible with the disabled theme state. Keyboard movement
skips them, pointer activation is consumed without selecting, and a committed
interval may not cross one. Blocking an existing selection clears it and raises
one ordered `SelectionChanged`; blocking a pending anchor clears the anchor.
Attached mutations are dispatcher-affine.

## Authored date faces

`SetMarkup(DateOnly, string)` replaces one date's complete four-cell numeric
face with trusted SharpVision inline markup. `RemoveMarkup` restores one default
numeric face and `ClearMarkup` restores all of them. Markup must produce visible
text; it is parsed before replacing existing state. Rendering clips at complete
grapheme boundaries, never draws half of a wide cluster, and never changes grid
geometry.

Calendar state is authoritative over authored markup. Adjacent-month muting,
hover/interval preview, committed selection, blocked/out-of-range state, and the
focused active indicator are applied after authored facets. A marked blocked
date therefore cannot use markup to appear available.

## Layout and visual states

The content measures 28 by 8 cells:

- one header row with previous/next month targets and a centered localized
  month/year;
- one row of seven localized two-cell weekday headings; and
- six rows of seven four-cell day faces.

The default rounded one-cell border plus one horizontal padding cell produces a
32 by 10 desired border box. Adjacent-month dates fill the leading and trailing
week slots and remain selectable when inside bounds. Selecting one follows its
month. Explicitly smaller bounds clip safely; zero content draws nothing.

The control uses existing theme values only: the focused foreground for header
commands, `Theme.Muted` for weekday and adjacent-month text, pointer-over colors
for hover and pending interval preview, selected colors for committed values,
disabled colors for unavailable values, and an underline for the focused active
date. No Calendar-specific theme entry exists.

## Keyboard and pointer input

The Calendar owns one focus stop with `TabNavigation.None`.

| Input                | Action                                                        |
| -------------------- | ------------------------------------------------------------- |
| Left / Right         | move to the previous / next selectable date                   |
| Up / Down            | move one week, continuing in that direction past blocked days |
| Home / End           | move inward to the first / last selectable date of the week   |
| Page Up / Page Down  | move to the corresponding date in the adjacent month          |
| Enter / Space        | activate `ActiveDate` through the current mode                |
| Header arrow press   | change only `DisplayMonth`                                    |
| Selectable day press | focus, make active, and activate the mapped date              |
| Blocked day press    | consume the press without changing selection                  |
| Pointer move / leave | update or clear direct date hover and interval preview        |
| Wheel up / down      | display the previous / next month                             |

Key press and repeat are accepted; release is ignored. A movement or wheel
command that cannot move inside the bounds remains unhandled so an ancestor may
respond. Header and day hit testing uses committed `ContentBounds`, so border,
padding, clipping, and tiny allocations never create invisible targets.

## Example

![The Calendar control rendered in the live showcase](../../images/controls/calendar.png)

```csharp
var booking = new Calendar
{
    SelectionMode = CalendarSelectionMode.Interval,
    DisplayMonth = new DateOnly(2026, 7, 1),
};

booking.BlockedDates.Block(
    new DateInterval(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14)));
booking.SetMarkup(new DateOnly(2026, 7, 19), "<accent><b>19★ </b></accent>");
booking.SelectionChanged += (_, change) => SaveBooking(change.Selection);
```

## Expected behavior

Tests cover value and enum validation before mutation, interval transitions and
event order, range coalescing/splitting, blocked selection invalidation, bounds,
culture, month arithmetic, leap years, date limits, localized headings, exact
six-week cells, markup/style precedence, Unicode clipping, zero/tiny bounds,
keyboard and pointer parity, hover, focus, Tab, header/wheel navigation,
disabled cleanup, mounted semantic output, exported-control coverage, common
box-model geometry, and the interactive showcase page.
