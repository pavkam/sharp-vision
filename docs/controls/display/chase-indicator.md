# ChaseIndicator

## ChaseIndicator contract

`ChaseIndicator` moves one or two highlighted glyphs through a bounded
horizontal or vertical track with a fading history. It owns no children and is
excluded from focus and pointer hit testing.

## API

| Member                                    | Default              | Contract                                                    |
| ----------------------------------------- | -------------------- | ----------------------------------------------------------- |
| `Style`                                   | `null`               | Optional complete developer-authored `ChaseIndicatorStyle`. |
| `ActualStyle`                             | Theme chase          | Always-present resolved style.                              |
| `Circle`, `Diamond`, `Square`, directions | Presets              | Complete active/inactive glyph-pair styles.                 |
| `Movement`                                | `Bounce`             | Bounce, wrapping, or center-spread sequence.                |
| `Length`, `Spacing`, `TrailLength`        | `5`, `0`, `2`        | Track geometry and retained history.                        |
| `HeadColor`, `TrailColor`, `TrackColor`   | Theme colors         | Optional semantic or concrete animation colors.             |
| `FadeDuration`, `Interval`, `IsPlaying`   | 400 ms, 200 ms, true | Animation timing and playback.                              |

`ChaseIndicatorStyle` owns the validated one-cell active/inactive glyph pair and
complete appearance profile. `ChaseIndicatorStyleSet` is partial Theme-file
composition. Assigning `Style` replaces the whole Theme-owned presentation; null
restores it. Under a wide-cell policy, configured ambiguous glyphs fall back to
a role-appropriate one-cell ASCII head and `.` track.

Changing the effective glyph pair resets phase. Appearance-only Theme changes
repaint without losing phase. Movement or length changes reset phase; spacing,
trail, timing, and colors preserve it. History is bounded by
`min(TrailLength, Length - 1)`.

## Example

![The ChaseIndicator control rendered in the live showcase](../../images/controls/chase-indicator.png)

```csharp
var indicator = new ChaseIndicator
{
    Movement = ChaseMovement.Spread,
    Style = ChaseIndicatorStyle.Diamond,
    Length = 21,
    TrailLength = 5,
    HeadColor = Color.Rgb(90, 247, 142)
};
```

## Expected behavior

Cover all movement sequences, style validation and precedence, Theme
replacement, phase reset/preservation, bounded history, RGB fade interpolation,
terminal-default fallback, orientation, spacing, clipping, wide-policy fallback,
timer lifecycle, visibility pause, and exact consecutive screens.
