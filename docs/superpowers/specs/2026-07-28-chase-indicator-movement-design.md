# ChaseIndicator movement modes

## Goal

Extend `ChaseIndicator` with two movement choices while retaining its existing
bounce as the default:

- a wrapping traversal that reaches the final position and restarts at the
  first position; and
- a symmetric traversal whose heads start at the center, move to both ends,
  and return to the center.

Every choice keeps the existing time-based gradual tail fade. The control
specification, XML documentation, focused tests, public API evidence, and
showcase page remain aligned.

## Public API

Add a `ChaseMovement` enum in its own file under
`SharpVision.Controls.Display`:

| Value | Sequence contract |
| --- | --- |
| `Bounce` | One head traverses from position zero to the final position and back. This is the default and preserves current behavior. |
| `Wrap` | One head traverses from position zero to the final position, then restarts at position zero. |
| `Spread` | One or two mirrored heads start at the center, traverse to both endpoints, and return to the center. |

Add `ChaseIndicator.Movement`, defaulting to `ChaseMovement.Bounce`. Assigning
an undefined value throws `ArgumentOutOfRangeException` before observable state
changes. Assigning a different valid value resets playback to phase zero,
re-seeds the fading history, and invalidates render without invalidating
measure.

`Pattern` remains exclusively responsible for the active and inactive glyph
pair. Movement does not change desired size, orientation, spacing, focus, hit
testing, or lifecycle behavior.

## Movement sequences

Positions use the existing zero-based logical track before orientation and
spacing are applied.

For length five:

- `Bounce`: `0, 1, 2, 3, 4, 3, 2, 1`, then repeats at `0`;
- `Wrap`: `0, 1, 2, 3, 4`, then repeats at `0`; and
- `Spread`: `{2}, {1, 3}, {0, 4}, {1, 3}`, then repeats at `{2}`.

For length six, `Spread` has two central starting positions:

`{2, 3}, {1, 4}, {0, 5}, {1, 4}`, then repeats at `{2, 3}`.

Thus odd lengths share one exact center cell at phase zero, while even lengths
use the two central cells. Endpoints appear once per outward-and-return cycle.
Length two in `Spread` displays both central/end positions and remains visually
stationary; its fade clock and lifecycle still obey the normal playback
contract.

Horizontal orientation interprets increasing positions as movement to the
right. Vertical orientation interprets them as movement downward.

## Tail and rendering

A movement step abandons the one- or two-position frame occupied by the heads
in the preceding phase. Each abandoned frame records its own animation-clock
timestamp and uses the current linear interpolation from `HeadColor` to
`TrailColor`. A frame therefore changes through intermediate colors rather
than switching directly to the trail endpoint.

`TrailLength` retains `min(TrailLength, Length - 1)` preceding movement frames.
For `Bounce` and `Wrap`, each frame contains one position. For `Spread`, each
frame contains both mirrored positions, except that an odd-length shared center
is stored once. Both positions in a spread frame share one fade timestamp, so
capacity stays bounded and symmetry cannot be broken by evicting half a pair.

Rendering remains oldest-to-newest, followed by current heads. A newer visit
wins when reversal causes overlap, and a current head always wins over every
trail visit. `Wrap` does not clear the endpoint visit when phase restarts, so
the old endpoint continues fading while the new head begins at position zero.
`Spread` records and fades both arms with identical timestamps, producing
symmetric colors.

Changing `Movement`, `Pattern`, or `Length` resets and seeds history according
to the selected movement. Changing the existing color, interval, fade, pause,
orientation, and spacing properties preserves their current reset and
invalidation contracts.

## Implementation shape

Keep one timer and one bounded frame-history store inside `ChaseIndicator`.
Represent each frame with parallel primitive arrays for its first position,
optional second position, and fade timestamp; no additional named helper type
is necessary. Replace the single hard-coded triangular position calculation
with movement-specific phase helpers that expose the current one- or
two-position frame and cycle length. Do not introduce child controls, per-mode
timers, allocations during render, or a public custom-path framework.

## Showcase

Expand the existing `ChaseIndicator` page rather than creating another page.
Show all three movements live on labeled horizontal tracks of at least fifteen
positions. Include a longer `Spread` specimen with an odd length and a longer
vertical specimen so center symmetry, orientation, and gradual fading are
visible. Use trail lengths long enough to demonstrate several intermediate
fade colors without crowding the page description.

## Verification

Unit and mounted-surface tests cover:

- the `Bounce` default and undefined-value validation;
- exact `Wrap` progression and restart;
- exact odd- and even-length `Spread` progression;
- the stationary length-two `Spread` boundary;
- endpoint uniqueness and phase reset after changing movement;
- symmetric spread history and persistent wrap-end history;
- intermediate fade colors for all three movement choices;
- clipping, spacing, horizontal and vertical rendering; and
- bounded frame-history resizing when movement or trail length changes.

The focused control and surface suites run before the repository formatting,
lint, build, and test gates.
