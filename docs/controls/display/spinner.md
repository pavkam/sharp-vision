# Spinner

## Spinner contract

`Spinner` displays one automatically advancing glyph from a fixed built-in
sequence. It derives directly from `Control`, owns no children, cannot receive
focus, and is excluded from pointer hit testing.

## API

| Property    | Type             | Default          | Contract                             |
| ----------- | ---------------- | ---------------- | ------------------------------------ |
| `Pattern`   | `SpinnerPattern` | `Braille`        | Selects one built-in frame sequence. |
| `Interval`  | `TimeSpan`       | 200 milliseconds | Duration between frame advances.     |
| `IsPlaying` | `bool`           | `true`           | Enables attached automatic playback. |

`SpinnerPattern` contains these exact cyclic sequences:

| Value          | Frames       |
| -------------- | ------------ |
| `Braille`      | `⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏` |
| `DenseBraille` | `⣿⣷⣯⣟⡿⢿⣻⣽`   |
| `Ascii`        | `\|/-\\`     |

Unknown patterns throw `ArgumentOutOfRangeException`. `Interval` accepts values
from 1 through 2,147,483,647 milliseconds; values outside that range throw
`ArgumentOutOfRangeException`. Validation precedes mutation. Changing `Pattern`
resets the current frame to the first frame and invalidates rendering. Changing
`Interval` restarts a running timer from one complete new interval without
changing the current frame. Setting `IsPlaying` to false pauses at the current
frame; setting it to true resumes after one complete interval.

## Lifecycle and rendering

Attachment creates one `DispatcherTimer` using the inherited dispatcher.
Detachment or disposal releases it and suppresses queued ticks. A tick advances
and invalidates rendering only while `IsPlaying` and `EffectiveIsVisible` are
true. Hidden and collapsed ancestry pauses phase; disabled state does not pause
this non-interactive status indicator. Reattachment retains phase.

The intrinsic desired size is one cell by one cell. Horizontal alignment
defaults to `Left`, vertical alignment defaults to `Top`, and rendering writes
the current Rune into the first content cell with the resolved style. Empty
content bounds draw nothing. The Braille ranges are neutral width under the
pinned Unicode policy; callers select `Ascii` when terminal font coverage
requires the maximum-compatibility sequence.

## Example

```csharp
var spinner = new Spinner
{
    Pattern = SpinnerPattern.DenseBraille,
    Interval = TimeSpan.FromMilliseconds(200),
};
```

## Test obligations

Cover defaults, validation before mutation, exact complete cycles, pattern
reset, interval restart, pause/resume, effective visibility, attachment,
detachment, disposal, reattachment, dispatcher affinity, one-cell layout,
zero/tiny bounds, style resolution, excluded interaction, and consecutive
semantic terminal screens through `SpinnerSurfaceTests`.
