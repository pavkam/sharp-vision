# Component Surface Test Harness Design

## Purpose and scope

SharpVision needs a test-only component harness that mounts one retained control
in a deterministic terminal surface, drives it through the same input and
rendering path as an interactive application, and exposes both semantic state
and final terminal cells for assertions.

This change establishes the reusable harness and applies it only to `Button`. It
does not migrate existing control tests, expose test hooks from production
assemblies, or introduce a general snapshot approval system.

## Test notation

A test creates the control normally, mounts it in a fixed-size surface, performs
typed user actions, and asserts semantic state plus the rendered surface:

```csharp
var button = new Button
{
    Width = Length.Cells(8),
    Height = Length.Cells(3),
    Content = new ControlText("Save"),
};
await using var surface = await ComponentSurface.MountAsync(
    button,
    new Size(9, 4),
    TestContext.Current.CancellationToken);

surface.ShouldHaveState(button, State.Normal);
surface.ShouldRender("""
╭──────╮
│Save  │▓
╰──────╯▓
 ▓▓▓▓▓▓▓
""");

await surface.Pointer.MoveToAsync(button);

surface.ShouldHaveState(button, State.Hovered);
surface.Cell(new Point(0, 0)).Style.Attributes.ShouldBe(Attributes.Bold);
```

The exact Button art and styles will be taken from the verified implementation
rather than assumed by this illustrative example.

Inline text represents every surface row and preserves leading and interior
spaces. The assertion right-pads shorter rows with blank cells to the surface
width, avoiding invisible trailing whitespace in source files. It provides a
compact reviewable appearance oracle. Tests must also assert the relevant
control state and representative cell styles so a text-only snapshot cannot
conceal a styling or interaction regression.

## Architecture

All harness types remain internal to `SharpVision.Tests`. Each named type has
its own file.

### `ComponentSurface`

`ComponentSurface` owns the mounted control, a private host container, the test
terminal, the real `Application`, and the latest virtual screen. `MountAsync`
validates a non-null detached control and positive surface dimensions, queues
the initial resize, starts the application, and does not return until the first
frame has been applied to the virtual screen.

The host provides empty space around explicitly sized controls so shadows and
pointer movement outside the target remain observable. Mounting follows ordinary
SharpVision ownership and disposal rules; no production test seam is added. The
host is also a neutral initial focus anchor. A Tab action can therefore enter
the mounted control through ordinary key routing without directly mutating the
focus manager.

Every action completes only after its input bytes have been decoded and the
resulting dispatcher work and frame write have settled. A bounded timeout
reports the action, pending state, mounted bounds, and most recent screen
instead of hanging a test.

The surface exposes:

- `Pointer`, for terminal pointer actions;
- `Keyboard`, for terminal key actions required by the Button scenarios;
- `Cell(Point)`, for grapheme and style inspection;
- `ShouldRender(string)`, for exact fixed-size text comparison; and
- `ShouldHaveState(Control, State)`, for exact public visual-state assertions.

### Input drivers

`ComponentPointer` resolves a target control's current arranged bounds on the
application dispatcher. `MoveToAsync(Control)` uses a deterministic interior
cell. Coordinate overloads allow tests to move outside a target. `PressAsync`,
`ReleaseAsync`, and `ClickAsync` emit SGR mouse bytes with one-based wire
coordinates and primary-button semantics.

`ComponentKeyboard` emits real terminal bytes for the focused-key scenarios used
by this change. `PressAsync(Code.Tab)` is sufficient to focus the sole mounted
Button through ordinary tab navigation. Unsupported codes fail immediately with
an argument exception; later control tests can add encodings test-first.

Neither driver calls `Router.Route`, `FocusManager.Focus`, `SetHovered`, or
`SetPressed`. Those shortcuts would bypass the behavior the harness is intended
to prove.

### Test terminal and screen model

The test terminal implements the real transport and resize boundaries. Input is
queued as immutable byte arrays. Each application write is copied, then applied
through the terminal parser to an independent semantic screen model.

The model tracks graphemes, wide-cell continuations, cursor position and
visibility, foreground, background, attributes, underline state, and hyperlinks
needed by the renderer contract. It ignores unrelated terminal session-control
sequences but rejects malformed modeled output. Applying incremental writes must
yield the same cells presented by `ComponentSurface` assertions.

The existing renderer `VirtualScreen` helper is adapted into the SharpVision
test project rather than referenced across test assemblies. This preserves an
independent output oracle without changing production visibility.

## Deterministic settling and lifecycle

Mount and action methods subscribe to frame completion before queuing work. They
wait for the corresponding transport write to be applied and for `FrameRendered`
to complete. The wait uses the xUnit cancellation token and a short explicit
timeout. An action that legitimately changes no pixels still completes through a
dispatcher barrier after its input has been consumed; it does not require an
unnecessary frame.

Disposal stops the application and releases the host tree and transport exactly
once. Cleanup exceptions retain the earliest failure while still attempting
every remaining cleanup step, matching SharpVision runtime ownership rules.

## Button proof

One dedicated `ButtonSurfaceTests` fixture demonstrates the notation and proves
the harness through public behavior.

The default bordered, shadowed Button is checked in these isolated conditions:

1. normal after initial mount;
2. hovered after a pointer move into the face;
3. pressed after a primary pointer press without release; and
4. focused after a real Tab key sequence reaches the sole mounted Button.

Chrome coverage uses the complete matrix:

| Border  | Shadow  | Required proof                                              |
| ------- | ------- | ----------------------------------------------------------- |
| present | present | normal and pressed text, geometry, styles, and state        |
| present | absent  | normal and pressed text, stationary face, styles, and state |
| absent  | present | normal and pressed text, translated face, styles, and state |
| absent  | absent  | normal and pressed text, stationary face, styles, and state |

Every scenario asserts the public state flags relevant to the action, exact
screen text, and representative face, border, and shadow cell styles. The
focused case proves keyboard decoding and focus independently from hover. The
pressed cases release the pointer or dispose their isolated surface so capture
cannot leak between scenarios.

A held shadowed Button translates its face into the released shadow footprint
and suppresses further shadow emission. Rendering another shadow one offset past
the held face violates the pressed-depth contract.

## Failure behavior

Public test-helper arguments are validated before the mounted tree or queued
input changes. Attempts to mount an attached or disposed control, target a
foreign or detached control, use an out-of-bounds point, compare a snapshot with
the wrong dimensions, or encode an unsupported key fail with actionable
exceptions.

Snapshot failures show expected and actual rows with visible row boundaries.
Cell failures include coordinates, grapheme, continuation ownership, and
resolved style. Timeouts include the action name and latest rendered text.

## Documentation and verification

`docs/testing/controls-integration.md` will own the normative component-surface
testing pattern and link to the existing input, focus, styling, and rendering
contracts. The Button control specification will reference the mounted-surface
proof without duplicating the harness contract.

Implementation follows red-green-refactor: first compile a wished-for Button
test against the missing harness, then add lifecycle, real input, semantic
screen, and assertion behavior in the smallest passing increments. Verification
runs the focused Button fixture, all `SharpVision.Tests`, documentation checks,
and finally `make format`, `make lint`, `make build`, and `make test`.
