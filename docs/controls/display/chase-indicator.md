# ChaseIndicator

## ChaseIndicator contract

`ChaseIndicator` displays one active glyph bouncing through a fixed-length
horizontal track of inactive glyphs. It derives directly from `Control`, owns no
children, cannot receive focus, and is excluded from pointer hit testing.

## API

| Property    | Type           | Default          | Contract                                                 |
| ----------- | -------------- | ---------------- | -------------------------------------------------------- |
| `Pattern`   | `ChasePattern` | `Circle`         | Selects one built-in active/inactive pair.               |
| `Length`    | `int`          | `5`              | Horizontal track length in terminal cells; at least two. |
| `Interval`  | `TimeSpan`     | 200 milliseconds | Duration between position advances.                      |
| `IsPlaying` | `bool`         | `true`           | Enables attached automatic playback.                     |

`ChasePattern` contains these exact pairs and width-policy fallbacks:

| Value     | Active | Inactive | Wide-policy fallback |
| --------- | ------ | -------- | -------------------- |
| `Circle`  | `●`    | `◯`      | `@` and `o`          |
| `Diamond` | `◆`    | `◇`      | `*` and `.`          |
| `Square`  | `■`    | `□`      | `#` and `.`          |
| `Up`      | `▲`    | `△`      | `^` and `.`          |
| `Down`    | `▼`    | `▽`      | `v` and `.`          |
| `Left`    | `◀`    | `◁`      | `<` and `.`          |
| `Right`   | `▶`    | `▷`      | `>` and `.`          |

Unknown patterns and lengths below two throw `ArgumentOutOfRangeException`.
`Interval` accepts values from 1 through 2,147,483,647 milliseconds and rejects
other values with `ArgumentOutOfRangeException`. Validation precedes mutation.

Changing `Pattern` or `Length` resets position zero and forward movement.
Pattern changes invalidate render; length changes invalidate measure. Changing
`Interval` restarts a running timer from one complete new interval without
changing position. Pausing retains position.

## Sequence, width, and rendering

For length five, positions follow `0, 1, 2, 3, 4, 3, 2, 1, 0`. Each endpoint
appears once per bounce. Length two follows `0, 1, 0`. The cycle continues until
paused, detached, hidden, or disposed.

Every Unicode pair is East Asian Width ambiguous. Under the inherited narrow
policy the exact Unicode pair renders. Under the wide policy each pair resolves
to its documented ASCII fallback, preserving the rule that `Length` always means
terminal cells.

The intrinsic desired size is `Length` by one cell. Horizontal alignment
defaults to `Left`, vertical alignment defaults to `Top`, and rendering writes
only the visible intersection of the horizontal track through the clipped
semantic canvas. Empty bounds draw nothing.

## Lifecycle

Attachment creates one `DispatcherTimer` using the inherited dispatcher.
Detachment or disposal releases it and suppresses queued ticks. A tick advances
and invalidates rendering only while `IsPlaying` and `EffectiveIsVisible` are
true. Hidden and collapsed ancestry pauses phase; disabled state does not pause
this non-interactive status indicator. Reattachment retains phase.

## Example

```csharp
var indicator = new ChaseIndicator
{
    Pattern = ChasePattern.Diamond,
    Length = 7,
    Interval = TimeSpan.FromMilliseconds(200),
};
```

## Test obligations

Cover defaults, validation, all primary and fallback pairs, length two and five
cycles, endpoint uniqueness, reset semantics, interval restart, pause/resume,
effective visibility, attachment, detachment, disposal, reattachment, dispatcher
affinity, clipping, resize, narrow and wide cell policies, excluded interaction,
and consecutive semantic screens through `ChaseIndicatorSurfaceTests`.
