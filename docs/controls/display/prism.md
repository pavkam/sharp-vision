# Prism

## Prism contract

`Prism` is declared `public sealed class Prism : ContentControl`. It applies a
deterministic RGB hue cycle to the foreground of one ordinary, replaceable
content control through the
[`ContentControl`](../content-control.md#contentcontrol-contract) role. It adds
no input, focus, or activation behavior. Content retains the usual parentage,
inherited context, routed ancestry, measurement, arrangement, rendering, and
disposal behavior of that single-content role.

Assigning or clearing `Content` uses the complete `ContentControl` ownership
transaction. Replacing content returns the detached previous control to the
caller without disposing it; disposing the Prism disposes the current content
exactly once. Content is measured and arranged through the ordinary
[border-and-padding box model](../../concepts/layout.md#passes-and-rounding).

## Effect properties

| Property      | Default                   | Validation                                 | Coordinate |
| ------------- | ------------------------- | ------------------------------------------ | ---------- |
| `Phase`       | `0`                       | Finite and in the half-open range `[0, 1)` | Hue offset |
| `CycleLength` | `18` terminal cells       | Positive integer                           | Divisor    |
| `Direction`   | `PrismDirection.Diagonal` | Defined `PrismDirection` value             | Axis       |

`PrismDirection.Horizontal` uses the content-relative horizontal coordinate `x`,
`Vertical` uses `y`, and `Diagonal` uses the checked sum `x + y`. These
coordinates are relative to the Prism's `ContentBounds`, not to the frame or the
child's own origin.

Invalid `Phase`, `CycleLength`, or `Direction` values throw
`ArgumentOutOfRangeException` before state changes, invalidation, or
notification. For a valid assignment, lifetime and attached-dispatcher access
are checked before equivalence. A changed value is committed, requests render
invalidation, and then raises `PropertyChanged` exactly once for that property.
An equivalent value is a no-op after the access checks. Effect changes never
request measure or arrange, so existing desired size and arranged bounds remain
stable. Setting an effect property after disposal throws
`ObjectDisposedException`; attached off-dispatcher mutation throws
`InvalidOperationException`.

## Color calculation

For the lead cell of each selected stored owner, Prism calculates:

```text
coordinate = Horizontal: x, Vertical: y, Diagonal: checked(x + y)
hue = Phase + (coordinate / CycleLength)
hue = hue - floor(hue)
scaled = hue * 6
sector = floor(scaled)
rising = round((scaled - sector) * 255, away from zero)
falling = 255 - rising
```

The half-open normalized hue always selects one of these six sectors:

| Sector | RGB result          |
| ------ | ------------------- |
| 0      | `(255, rising, 0)`  |
| 1      | `(falling, 255, 0)` |
| 2      | `(0, 255, rising)`  |
| 3      | `(0, falling, 255)` |
| 4      | `(rising, 0, 255)`  |
| 5      | `(255, 0, falling)` |

This calculation is culture-independent. The floor-based modulo-one
normalization wraps negative and positive lead-relative coordinates, while the
public property domain keeps `Phase` normalized. A boundary-straddling owner is
always colored from its lead coordinate, even when that lead lies outside the
requested effect region.

## Rendering and write provenance

Prism follows the shared
[control rendering order](../../architecture/rendering-pipeline.md#control-rendering).
Its own standard body, border, and shadow chrome render first. During the
ordinary child pass, it synchronously captures rendering of its retained
content. The requested Prism region is `ContentBounds` intersected with the
child's arranged `Bounds`; any cell in that region discovers its complete stored
owner. No content is a no-op.

Selection uses the frame's bounded internal write provenance rather than a
semantic before-and-after comparison. A discovered stored owner is eligible only
when its latest mutation was performed by that child-render callback.
Consequently:

- identical overwrites count as writes;
- stored spaces written by the callback participate;
- untouched blanks, pre-existing underlay, and Prism's earlier chrome do not;
- nested ordinary descendants participate because their writes occur inside the
  same synchronous child-render callback; and
- writes performed as a side effect of foreground selection fall after the
  closed draw window and do not become selected by that effect.

The requested region discovers an owner when any of its cells intersects that
region; it does not clip the selected owner to the region. The selector uses the
owner's absolute lead coordinate for hue calculation. A selected wide owner
therefore transforms atomically across its complete span even when its lead or
continuation crosses the requested region boundary. The complete owner must
remain inside the active canvas clip, including every ancestor clip, or it is
skipped. Prism never creates or recolors half of a wide owner.

The transformation replaces foreground only. It preserves the stored grapheme
bytes, background, attributes, hyperlink, underline kind, underline color, and
lead/continuation ownership. These are semantic cell fields under the
[rendering pipeline contract](../../architecture/rendering-pipeline.md#cell-and-frame-rules)
and the
[Unicode ownership rules](../../concepts/unicode-cell-geometry.md#cell-ownership).

Mutation revisions are frame-local, bounded, and internal. They are excluded
from public cell values, semantic equality, hashing, damage detection, terminal
encoding, and terminal bytes. A normal capture starts and ends in constant time
without managed allocation. The complete provenance and failure rules live in
the
[`Canvas.DrawWithForeground` contract](../../architecture/rendering-pipeline.md#cell-and-frame-rules).

### Popup layer

Prism affects only the ordinary retained-child pass. An elevated `Popup` or
promoted popup descendant is omitted from that pass and rendered later through
the root popup pass above ordinary siblings. Its cells are therefore not
recolored by an ancestor Prism. Ordinary nested descendants remain part of the
effect; visual elevation, not ownership depth, defines the boundary.

### Callback failures

The drawing and color callbacks are borrowed synchronously and never retained.
If content drawing throws, no foreground pass begins and the same exception
propagates. If foreground selection throws, the already transformed row-major
prefix remains valid while the failing and later owners remain unchanged. In
either case, control rendering restores render invalidation before propagating
the exception. Capture cleanup still completes.

## Animation and threading

Prism owns no timer and advances no state during rendering. A caller animates
the effect by updating `Phase`, typically from a dispatcher timer. The control
caches its drawing and selection delegates, so steady-state rendering does not
allocate a delegate per frame. Neither Prism nor the canvas retains those
callbacks after the synchronous render call.

Detached construction and mutation follow the base control threading rules.
After attachment, content and effect mutation are dispatcher-affine. Rendering
is dispatcher-affine, rejects reentrancy, and uses only the semantic canvas; the
control never emits terminal protocol bytes.

## Example

```csharp
var title = new Prism
{
    Direction = PrismDirection.Diagonal,
    CycleLength = 18,
    Content = new FigletText(FigletCatalog.Default.Load("Small"))
    {
        Content = "SNAKE",
    },
};

title.Phase = (title.Phase + (1d / 60d)) % 1d;
```

## Test obligations

Tests cover:

- constructor defaults; finite/range, positive-length, and enum validation;
  rejected-state preservation; dispatcher and disposal failures; equivalent
  assignments; invalidation-before-notification ordering; and exact property
  notification order;
- exact RGB values at sector boundaries and rounded interior values, phase and
  coordinate wrap, and horizontal, vertical, and checked diagonal coordinates;
- render-only invalidation, unchanged measure/arrange counts and geometry, null
  content, empty bounds, border-consumed bounds, and tiny clips;
- foreground-only transformation with glyph, background, attributes, hyperlink,
  underline, underline color, and lead/continuation preservation;
- prepainted overlap, untouched underlay and blanks, identical overwrites,
  stored spaces, selector-side writes, nested ordinary effects, and nested
  ordinary child rendering;
- row-major once-per-lead selection, mixed narrow and wide owners, a
  boundary-straddling wide owner selected through either intersecting cell,
  complete-span transformation across the requested region boundary, partial
  wide clipping by the active canvas, lead-coordinate hue wrap, and intact
  ownership after transformation or failure;
- child-drawing failure, selector failure with deterministic partial progress,
  bounded capture cleanup, and no per-frame delegate allocation;
- ordinary `ContentControl` assignment, replacement, clearing, parentage,
  layout, direct-child disposal, owner disposal, and callback ordering; and
- elevated popup and promoted-popup exclusion from the ordinary Prism effect.
