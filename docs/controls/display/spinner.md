# Spinner

## Overview

`Spinner` displays one automatically advancing glyph. It owns no children,
cannot focus, and is excluded from pointer hit testing.

## API

| Member                      | Default          | Description                                          |
| --------------------------- | ---------------- | ---------------------------------------------------- |
| `Style`                     | `null`           | Optional complete developer-authored `SpinnerStyle`. |
| `ActualStyle`               | Theme spinner    | Always-present resolved style.                       |
| `SpinnerStyle.Braille`      | Theme default    | Ten-frame light Braille orbit.                       |
| `SpinnerStyle.DenseBraille` | Preset           | Eight-frame dense rotation.                          |
| `SpinnerStyle.Ascii`        | Preset           | Portable bar, slash, dash, and backslash sequence.   |
| `Interval`                  | 200 milliseconds | Duration between advances.                           |
| `IsPlaying`                 | `true`           | Enables attached playback.                           |

`SpinnerStyle` owns a bounded immutable frame sequence and the complete
appearance profile. `SpinnerStyleSet` is partial Theme-file composition and is
not exposed on Spinner. Assigning `Style` replaces the whole Theme-owned
presentation; null restores it. A style requires 1–256 printable one-cell
frames.

Changing the effective frame sequence resets to its first frame. Appearance-only
local or Theme style changes repaint without losing the current frame. Interval
changes restart the timer without changing the current frame. Hidden or
collapsed ancestry pauses phase; disabled does not.

## Example

![The Spinner control rendered in the live showcase](../../images/controls/spinner.png)

```csharp
var spinner = new Spinner
{
    Style = SpinnerStyle.DenseBraille,
    Interval = TimeSpan.FromMilliseconds(200)
};
```

## Expected behavior

Cover style validation/copying, local/Theme precedence, phase reset versus
appearance-only repaint, timer lifecycle, visibility pause, attachment,
dispatcher affinity, width fallback, one-cell layout, and exact frames.
