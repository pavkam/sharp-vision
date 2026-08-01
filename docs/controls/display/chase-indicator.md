# ChaseIndicator

## Overview

`ChaseIndicator` animates one or two highlighted glyphs along a bounded
horizontal or vertical track, leaving a fading trail behind the head. It is a
pure display control: it owns no children and takes no part in focus or pointer
hit testing.

## API

| Member                                    | Default              | Description                                                  |
| ----------------------------------------- | -------------------- | ------------------------------------------------------------ |
| `Style`                                   | `null`               | Optional complete developer-authored `ChaseIndicatorStyle`.  |
| `ActualStyle`                             | Theme chase          | The resolved style; always present.                          |
| `Circle`, `Diamond`, `Square`, directions | Presets              | Complete preset styles built on active/inactive glyph pairs. |
| `Movement`                                | `Bounce`             | Selects the bounce, wrapping, or center-spread sequence.     |
| `Length`, `Spacing`, `TrailLength`        | `5`, `0`, `2`        | Track geometry and how much history the trail retains.       |
| `HeadColor`, `TrailColor`, `TrackColor`   | Theme colors         | Optional semantic or concrete colors for each animated part. |
| `FadeDuration`, `Interval`, `IsPlaying`   | 400 ms, 200 ms, true | Animation timing and playback.                               |

A `ChaseIndicatorStyle` holds the validated one-cell active/inactive glyph pair
together with a complete appearance profile. `ChaseIndicatorStyleSet` exists for
partial composition in Theme files; it is not a control property. Assigning
`Style` replaces the entire Theme-owned presentation, and assigning `null`
restores it. If the active cell-width policy is wide and a configured glyph
becomes ambiguous, the control falls back to a role-appropriate one-cell ASCII
head and a `.` track glyph.

Changing the effective glyph pair resets the animation phase. Appearance-only
Theme changes repaint without losing the phase. Changing `Movement` or `Length`
also resets the phase, while spacing, trail, timing, and color changes preserve
it. The trail history never grows beyond `min(TrailLength, Length - 1)` entries.

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

Callers can rely on the documented behavior end to end: every movement sequence
advances as described; style values are validated and resolved with the
documented local-over-Theme precedence, and replacing the Theme takes effect
immediately; phase resets exactly when the glyph pair, movement, or length
changes and survives appearance-only changes; trail history stays within its
bound; trail colors fade by RGB interpolation and degrade correctly against
terminal-default colors; both orientations, spacing, and clipping render as
specified, with the wide-policy ASCII fallback applied when needed; the
animation timer starts and stops with attachment and playback, pauses while the
control is not visible, and the rendered output matches exact consecutive
screens.
