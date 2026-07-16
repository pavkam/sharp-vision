# Snake and Prism redesign

## Status

Approved for implementation on 2026-07-16.

## Context

The Snake example already has a FIGlet title, difficulty selection, high scores,
a gameplay HUD, special apples, speed boost, pause, and a short death flash. Its
presentation is still visually static: the title is one green color, the title
panels form a long undifferentiated stack, the HUD is one clipped text string,
and most game objects do not animate between movement ticks. `Ctrl+Q` exits only
during active play and the visible UI does not teach that shortcut.

SharpVision has no reusable control that can apply a spatial color effect to a
rendered child. A theme or inherited foreground supplies one color per control;
it cannot produce a moving rainbow across FIGlet cells. The semantic canvas can
replace a complete cell style, but callers cannot currently replace only the
foreground while preserving the rendered child's other style fields.

## Goals

- Make the title screen feel like a polished terminal arcade attract screen.
- Animate a restrained rainbow across the FIGlet title and record celebrations
  without coloring the whole game board.
- Organize gameplay information and controls into a readable two-row HUD.
- Display movement, pause, and quit shortcuts, with `Ctrl+Q` reserved and
  readable at narrow widths.
- Make title, collectible, boost, snake-head, and death visuals move between
  game ticks.
- Add one focused reusable SharpVision control for spatial rainbow foreground
  effects.
- Preserve deterministic UI state, dispatcher affinity, Unicode cell ownership,
  clipping, and bounded rendering.
- Prove the core control and the example through focused behavioral and
  rendering tests, documentation, and a showcase page.

## Non-goals

- A general composable filter or shader framework.
- Background, glyph, geometry, blur, transparency, or terminal-protocol effects.
- A control-owned animation scheduler or a new application timer subsystem.
- Rainbow coloring across the gameplay board or snake body.
- Changes to FIGfont parsing, catalog contents, font provenance, or FIGlet
  composition.
- Persistence or networking for high scores.

## Chosen approach

Add a focused `Prism` control backed by a narrow semantic-canvas foreground
operation. `Prism` derives from `ContentControl`, renders its ordinary child,
then replaces the foreground of rendered child graphemes with a deterministic
spatial rainbow. The caller changes `Phase` to animate the effect.

This keeps the reusable behavior in SharpVision without introducing arbitrary
render callbacks, effect graphs, or hidden scheduling. A Snake-only title
renderer would duplicate core cell-safety rules and discard the reusable idea. A
general `Filter` abstraction would require contracts for composition, ordering,
failure, allocation, clipping, and effect interaction that this feature does not
need.

## Core rendering primitive

`SharpVision.Terminal.Rendering.Canvas` gains:

```csharp
public void ApplyForeground(Rect region, Func<Point, Color> selector)
```

The method validates `selector` before touching the frame, intersects `region`
with the canvas clip and frame bounds, and visits stored grapheme owners in
row-major order. It invokes `selector` once for the lead coordinate of each
complete owner and replaces only `CellStyle.Foreground`. Background, attributes,
underline style and color, hyperlink, grapheme bytes, width, and continuation
ownership remain unchanged.

Untouched frame blanks have no stored grapheme and are skipped. A stored space
is a grapheme and is transformed. If any cell belonging to a wide grapheme lies
outside the effective clip, the complete owner is skipped so lead and
continuation styles cannot disagree. Continuation cells are never transformed
independently.

The selector runs during ordinary control rendering. If it throws, rendering
fails through the existing render-failure path; the incomplete back frame is not
committed to the terminal. The API does not retain the delegate or expose
frame-owned memory after the call.

This primitive belongs in the terminal rendering layer because it enforces cell
and frame ownership. It has no knowledge of controls, animation, FIGlet, or
rainbows.

## Prism control

`SharpVision.Controls.Prism` is a public sealed `ContentControl`. It uses the
ordinary replaceable `Content` ownership contract, contributes no extra desired
size, and arranges content through the inherited border, padding, and content
box behavior. Input, focus, hit testing, lifecycle, theme inheritance, and popup
traversal remain those of the content edge.

After its normal child pass, `Prism` calls `ApplyForeground` over its effective
content bounds. Its own fill, border, and shadow chrome are not recolored.

### Public properties

- `Phase` is a `double` in the half-open range `[0, 1)`, defaults to `0`, and
  has render impact. It is the normalized hue offset. NaN, infinities, and
  values outside the range throw `ArgumentOutOfRangeException` before state or
  invalidation changes.
- `CycleLength` is a positive integer number of terminal cells, defaults to
  `18`, and has render impact. Zero and negative values throw
  `ArgumentOutOfRangeException` before mutation.
- `Direction` is a defined `PrismDirection`, defaults to `Diagonal`, and has
  render impact. Unknown values throw `ArgumentOutOfRangeException` before
  mutation.

`PrismDirection` contains `Horizontal`, `Vertical`, and `Diagonal`. Horizontal
uses the content-relative X coordinate, vertical uses Y, and diagonal uses
`X + Y`. The normalized hue for coordinate `c` is:

```text
fraction(Phase + c / CycleLength)
```

The hue converts to full-saturation, full-value RGB by linearly interpolating
between red, yellow, green, cyan, blue, magenta, and the next red. Component
rounding is deterministic and culture independent. Existing terminal color
projection continues to map those semantic RGB values to the active terminal
profile.

`Prism` never starts a task or timer. Animation is an explicit sequence of
dispatcher-affine `Phase` assignments. Equivalent property assignments are
no-ops under the existing property contract.

## Snake composition

The title, HUD, and phase panels become retained controls created once in the
`SnakeScreen` constructor. Phase changes update content and visibility instead
of repeatedly clearing and reconstructing the overlay tree.

### Start screen

The start screen uses three visual layers:

1. `SnakeBoard` draws a low-contrast animated attract-mode field instead of an
   empty black rectangle.
2. A centered `Prism` wraps the existing audited `FigletText` title.
3. A bounded two-column area presents an action card and a compact high-score
   card. The action card leads with `Enter  Start`, presents `1 / 2 / 3`
   difficulty selection as one grouped choice, and keeps `Q  Quit` visible.

The title adds a concise arcade subtitle and a small status line that names the
selected difficulty. At normal terminal sizes the cards sit side by side to
avoid the current tall stack. At constrained sizes the layout clips safely, and
the persistent HUD remains the authoritative quit-shortcut surface.

### HUD

The HUD occupies two rows above the board:

- The first row contains the Snake brand, zero-padded score, lives, difficulty,
  best score, and an optional boost badge in separate layout regions.
- The second row contains `Arrows / WASD  Move` and `P  Pause`, while a
  right-reserved `Ctrl+Q  Quit` region is arranged first so less-critical
  guidance clips before the exit chord.

Title mode replaces gameplay metrics with the selected difficulty while
retaining the quit region. Paused mode changes the status region to make the
resume action explicit.

`Ctrl+Q` is handled before phase dispatch and closes the application from title,
play, pause, death, high-score entry, and game-over phases. Plain `Q` remains a
title-screen shortcut. Movement continues to accept arrows and WASD; `P` toggles
pause during play and resumes from pause.

## Animation model

Snake keeps game simulation and visual animation separate:

- The existing game tick advances position and collision state at the selected
  difficulty and boost interval.
- One cancellable visual pulse advances presentation every 80 ms. It posts
  immutable pulse records to the application dispatcher; all control and
  presentation-state mutation occurs on that dispatcher. A delayed pulse is
  coalesced rather than replayed, so a busy dispatcher never receives a burst of
  visual catch-up work.
- A monotonically increasing visual frame drives `Prism.Phase`, attract-mode
  motion, apple sparkle, snake-head breathing, and boost trails. Every delivered
  pulse advances the title phase by `1 / 60`, producing one 4.8-second color
  cycle before wrapping to zero.
- Death animation becomes a deterministic 12-pulse segment wave. Each pulse
  advances the activated prefix by a proportional share of the current body, so
  every body length reaches the tail within 960 ms. Three final pulses hold the
  completed hot-color state before the existing high-score or game-over
  transition.
- Record entry wraps its FIGlet heading in `Prism`; ordinary game-over text
  remains red for immediate readability.

The visual pulse starts after application startup, is cancelled during disposal,
and ignores posted work after disposal. Game-loop cancellation and visual-loop
cancellation have distinct ownership so stopping movement does not freeze the
title or record animation. No callback mutates the tree from a pool thread.

The board presentation remains cell based. Animation may change glyphs or styles
only through the semantic canvas and never emits terminal bytes.

## Focused source structure

The example is split along retained responsibilities rather than expanding the
existing `SnakeScreen` and `SnakeBoard` files indefinitely:

- `SnakeScreen` owns application lifecycle, phase transitions, global input, and
  the two loops.
- `SnakeHud` owns the two-row retained HUD and its update surface.
- `SnakeTitlePanel` owns the title, action card, and high-score card.
- `SnakeAnimationState` owns deterministic visual-frame and death-wave state.
- `SnakeBoard` renders board and attract-mode state supplied by those owners.

Each named type receives its own exact-name file. These example types do not
create a second control framework; they compose existing SharpVision controls
and keep game state independent of rendering.

## Failure and boundary behavior

- Every new public argument and property is validated before observable state
  changes.
- Prism rendering with null content is a no-op after its own normal chrome.
- Empty bounds, empty clips, and regions containing no stored graphemes are
  no-ops.
- Tiny and zero-sized layouts do not index outside the frame or divide by zero.
  The HUD has an explicit 40-cell-wide proof where `Ctrl+Q  Quit` remains fully
  visible while movement guidance may clip.
- Selector failure prevents the frame from committing through the existing
  renderer error path.
- Application disposal cancels both loops and prevents late dispatcher work from
  mutating disposed controls.
- Terminals without true color use the renderer's existing deterministic color
  projection; Prism does not perform capability detection.

## Documentation and showcase

The implementation adds a normative `Prism` control contract under
`docs/controls/display/`, links it from the control catalog and main index, and
updates the rendering pipeline and testing documentation for the new canvas
operation. Public and internal members receive XML documentation including
validation, units, side effects, threading, and exceptions.

The SharpVision showcase adds a `Prism` page with static horizontal, vertical,
and diagonal specimens plus an interactive phase control. The page makes clear
that `Prism` is caller-driven and foreground-only. The Snake example remains the
complete animated integration demonstration.

## Test strategy

### Terminal rendering tests

- Exact foreground replacement while every other `CellStyle` field remains
  unchanged.
- Row-major selector coordinates and one invocation per grapheme owner.
- Narrow, wide, stored-space, untouched-blank, empty, and clipped-owner cases.
- Null selector validation before mutation and selector failure without frame
  ownership leaks.

### Prism control tests

- Defaults and validation for every property.
- Render-only invalidation and equivalent assignment.
- Null, assign, replace, clear, layout, clipping, input, and focus behavior
  inherited from `ContentControl`.
- Exact RGB output for each direction, phase change, cycle boundary, stored
  spaces, and wide graphemes.
- Preservation of child backgrounds, attributes, underline, hyperlink, and Prism
  chrome.
- Tiny and zero bounds.

### Showcase tests

- Gallery registration and documentation content.
- All three directions render distinct deterministic patterns.
- Changing phase updates cells without changing text or layout.

### Snake tests

A dedicated example test project references `examples/Snake` and exercises
public behavior with deterministic presentation-state advancement:

- Start screen contains the FIGlet title, action card, difficulty choices, high
  scores, and quit guidance.
- HUD places metrics and all shortcuts, with `Ctrl+Q  Quit` remaining complete
  in a 40-cell-wide viewport.
- `Ctrl+Q` closes from every phase; plain `Q` closes from title only.
- One visual pulse advances title colors, attract mode, apples, and head style
  without moving game state.
- Game ticks move game state without depending on the visual frame rate.
- Death frames progress head-to-tail and transition exactly once.
- Disposal cancels loop work and produces no late mutation.
- Representative full screens validate continuation ownership and semantic
  colors at title, play, pause, death, record, and game-over phases.

## Verification

Development runs focused terminal rendering, Prism control, showcase, and Snake
tests after observing each new test fail for the intended missing behavior.
Completion requires:

```bash
make format
make lint
make build
make test
```

All commands must finish with zero warnings and errors, the configured minimum
test count, and no Markdown or local-link failures.
