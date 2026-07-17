# Adaptive Color Picker Design

## Goal

Add reusable `Slider` and capability-aware `ColorPicker` controls whose mouse,
keyboard, layout, rendering, documentation, and showcase behavior agree. The
picker must expose the colors the active terminal can actually reproduce while
retaining the existing Canvas color-grid visual language.

## Public surface

### Slider

`Slider` is a focusable integer range control. It exposes `Minimum`, `Maximum`,
`Value`, `SmallChange`, `LargeChange`, `Orientation`, and a `ValueChanged`
event carrying the previous and committed values. The default range is 0
through 100, the default small change is 1, the default large change is 10, and
the default orientation is horizontal.

The range may include negative values. Setters reject an undefined orientation,
negative changes, endpoints that invert the range or exclude the current value,
and direct values outside the inclusive endpoints before observable state
changes. Command changes use saturating arithmetic and clamp to the endpoints.

Horizontal sliders map minimum to the left and maximum to the right. Vertical
sliders map minimum to the bottom and maximum to the top. Pointer press maps
directly to the nearest representable value, takes capture, and subsequent cell
or inferred-pixel movement drags against immutable press geometry. Release,
detach, disable, hide, disposal, focus loss, or capture cancellation ends the
drag without an extra value commit. Arrow keys and wheel input use
`SmallChange`; Page Up and Page Down use `LargeChange`; Home and End select the
endpoints. Releases do not change the value, and an endpoint no-op remains
available to routed-event bubbling.

The control draws one semantic rail with filled, unfilled, and thumb cells. It
uses the resolved appearance for normal, hovered, pressed, focused, and disabled
states, remains contained in zero and tiny bounds, and uses narrow ASCII
fallbacks when a configured Unicode glyph is not one cell under the active
width policy.

### ColorPicker

`ColorPicker` is a retained `CompositeControl`. It exposes a terminal `Color`
`Value` and a `ValueChanged` event carrying the previous and committed colors.
Its private implementation root is created once in the constructor from shared
`Grid`, `Stack`, `Dock`, and `Overlay` controls. Layout, measure, arrange,
rendering, and hit testing use the normal retained-control pipeline.

The active application capability profile is inherited by every control in the
same way as cell policy and theme context. A detached picker accepts and retains
the supplied concrete color. On attachment and every capability change, it
normalizes that value before publication:

- true color commits an RGB value;
- indexed 256 commits the nearest xterm-compatible palette index;
- basic 16 commits the nearest ANSI palette index from 0 through 15; and
- monochrome commits `Color.Default`.

Downgrading is intentionally lossy. A later upgrade does not restore a discarded
RGB value. A changed normalization raises one `ValueChanged` event after the
value commits; a no-op is silent. Programmatic assignment while attached uses
the same normalization.

## Adaptive presentation

The picker owns all four presentation branches for its complete lifetime and
changes their `Visibility` when color depth changes.

At true color, a stretching grid presents:

- a saturation/value plane with a contrasting selected-cell marker;
- a hue ramp overlaid by a horizontal `Slider`;
- exact red, green, and blue sliders covering 0 through 255;
- a preview swatch; and
- an uppercase `#RRGGBB` readout.

Pointer coordinates map through committed bounds. The saturation/value plane is
keyboard focusable and uses horizontal arrows for saturation and vertical
arrows for value. Hue and RGB sliders retain the standard slider keyboard
contract. Updating any surface synchronizes every other retained part before
the picker publishes its one semantic value change.

At indexed 256, the picker presents a 16-by-16 row-major swatch grid based on
the Canvas showcase sample, using two terminal columns per swatch where space
permits. At basic 16, it presents a 4-by-4 row-major grid. Each palette surface
is one focusable target: arrows move one column or row, Home and End reach the
first and last swatch, and pointer press or drag selects the swatch under the
committed local coordinate. At monochrome, the control presents a disabled
default-only swatch and status label.

All branches clip safely under tiny allocation. Unsupported visual fidelity is
never simulated by retaining an RGB `Value` the renderer will project later.

## Shared color projection

The terminal project exposes one shared color-palette utility for deterministic
projection and indexed-to-RGB resolution. The frame encoder and ColorPicker use
that exact implementation. Projection preserves RGB only for true color,
preserves already-contained indexed values, maps RGB or larger indexed values
to the nearest supported reference entry, uses ascending index for ties, and
returns the terminal default in monochrome.

The public utility validates unknown color depths. Indexed resolution follows
the existing xterm-compatible basic, 6-by-6-by-6 cube, and grayscale tables.

## Event and failure behavior

All public mutation is dispatcher-affine after attachment. Argument validation
precedes mutation. Slider and picker events observe committed values and run
without internal locks. Reentrant handlers may request a subsequent valid
change; the controls do not rebuild private composition from callbacks.

Pointer and keyboard input is ignored while effectively hidden or disabled.
Capture cleanup follows the shared input contract. Rendering uses only the
semantic terminal canvas and never emits escape bytes.

## Showcase and documentation

The Canvas page stops presenting the palette grid as an ad hoc custom-drawing
widget. Dedicated `Slider` and `ColorPicker` pages join the gallery catalog.
The Slider page demonstrates horizontal, vertical, negative, stepped, and live
value variants. The ColorPicker page demonstrates the active terminal tier,
live preview and value reporting, pointer selection, and keyboard instructions.

The control catalog, layout/input/styling links, showcase contract, testing
contract, XML documentation, and dedicated control specifications update in the
same change.

## Correctness evidence

Slider tests cover validation without partial mutation, signed and extreme
ranges, event order, exact cell geometry, tiny bounds, orientation, keyboard,
wheel bubbling, direct track selection, cell and inferred-pixel dragging,
capture cancellation, disabled and hidden state, resize, and resolved visual
states. A mounted surface test drives decoded terminal key and pointer input to
the final frame.

Color tests cover the shared projector at every tier, all palette entries,
tie-breaking, invalid depth, and fixed-seed RGB projection invariants.
ColorPicker tests cover detached assignment, attachment and runtime capability
transitions, lossy downgrade, no resurrection on upgrade, exact RGB slider
synchronization, saturation/value and palette mapping, pointer capture,
keyboard navigation, event order, focus, disabled state, zero/tiny/resize
layout, selected markers, and final semantic cells. Fixed-seed randomized tests
require every committed value to belong to the active color tier and every
render write and hit target to remain contained.

Showcase tests verify both catalog pages, their required explanatory content,
representative rendered screens, and live interaction logs. Focused tests run
during each red/green cycle, followed by `make format`, `make lint`,
`make build`, and `make test`.
