# Showcase follow-up interaction polish

## Goal

Complete the second showcase remediation pass by fixing the shared layout,
rendering, pointer, keyboard, and example-composition contracts exposed by the
latest visual review. The result must behave as executable documentation rather
than a collection of static control fragments.

## Scope

This pass covers seven related outcomes:

1. the selected page fills the complete region remaining beside the sidebar;
2. ordinary `Text` preserves an already painted parent background;
3. list hover and selection backgrounds remain continuous beneath row text;
4. Popup and Window examples use populated stages and begin without promoted
   surfaces obscuring their documentation;
5. a primary double-click selects the complete Unicode-safe word beneath the
   pointer;
6. hosted text inputs support `Ctrl+C`, `Ctrl+X`, and `Ctrl+V` without the
   showcase stealing `Ctrl+C` for exit; and
7. the sidebar footer becomes a legible utility group with a full-width theme
   picker and a full-width Quit action.

Table scrolling and the earlier control-state remediation are outside this pass
because they are already covered by the preceding verified changes.

## Chosen design

### Page ownership and fill

`Gallery` replaces its vertical `Stack` page host with a non-scrolling `Dock`.
The selected page is the Dock's final child and therefore receives the complete
remaining rectangle on both axes. Each `Doc.Page` continues to own its fixed
header and independently scrolling body. The gallery never mutates alignment
properties on a page returned by a pane factory.

This is preferred over forcing every page to set stretch alignment because the
host, not each document, owns allocation of the main application region.

### Transparent text and row surfaces

`Text` treats its resolved `FillMode` as the authority for unmarked glyph
background replacement. With the default `FillMode.Transparent`, glyphs apply
foreground and decorations while preserving destination backgrounds. An explicit
markup background remains opaque, and callers that want a control-wide text
background set `FillMode.Opaque` together with `Background`.

This makes the opaque parent surface the single owner of the page-header and
list-row background. `ListItem` continues to paint the complete hovered or
selected row before its template renders; transparent `Text` can no longer punch
normal-theme holes into that row. Custom text backgrounds remain available
through the explicit opaque-fill contract.

### Popup and Window teaching stages

Every Popup specimen begins closed. A visible Button opens it, and selection,
Escape, outside click, or a second trigger activation closes it. Placement
buttons both select the preferred side and open the shared placement popup.
Lifecycle, style, edge, and resize examples likewise expose an explicit trigger
instead of rendering permanent promoted surfaces through the documentation.

Window examples render over populated, opaque application surfaces. The main
project-settings specimen removes the redundant outer frame, keeps its shadow
inside a clipped stage, and shows the workspace around every side of the window.
Styled and composition specimens receive sufficient width, wrapping, centering,
and backdrop ownership. The unreadable two-cell Window is replaced in the
showcase by a minimum readable boundary specimen; exact two-cell saturation
remains a library test rather than a visual lesson.

### Multi-click gesture and word selection

`CaptureManager` synthesizes a routed click count because terminal mouse
protocols report presses but not desktop-style gestures. Consecutive presses
count together only when they target the same control, primary button, and cell
within 500 milliseconds. A different target, button, cell, or expired interval
starts a new sequence. Non-press events expose a click count of zero.

`PointerEventArgs.ClickCount` carries that dispatcher-affine gesture metadata.
The manager accepts an optional `TimeProvider` so tests advance time without
sleeping. `Edit.SelectWord` returns a grapheme-aligned selection: contiguous
letters, digits, and underscore form a word; a non-word selects its one complete
grapheme; the source end selects an empty range. On a primary double-press,
`TextInput` focuses and selects that range without starting a character drag
that would collapse the selection on release.

### Clipboard shortcuts and exit ownership

The running `Application` owns a small in-process text clipboard used by focused
`TextInput` controls. A root preview handler recognizes exact Control-modified
`C`, `X`, and `V` presses:

- Copy stores `CopySelection()` and mirrors non-empty text through
  `Application.Terminal.Clipboard.Write` when supported.
- Cut stores `CutSelection()` under the same password/read-only rules.
- Paste inserts the application-owned text through the same `TextInput` policy,
  events, Unicode boundaries, maximum length, and undo history as terminal
  paste.

An empty or password-suppressed copy/cut does not erase the existing clipboard.
An empty clipboard makes paste a handled no-op. External terminal paste remains
the bracketed `Paste` event path; this pass does not claim synchronous host
clipboard reads that the terminal has not supplied.

The showcase runs with `TreatControlCAsInput` enabled and moves its global exit
binding from `Ctrl+C` to `Ctrl+Q`. Thus copy reaches the focused input while
Quit remains available from the footer and a non-conflicting keyboard chord.

### Sidebar utility group

The sidebar retains its 28-cell bordered width. Its footer becomes a vertical
utility group separated from component navigation:

- an `Appearance` heading with one intentional palette emoji;
- a full-content-width themed ComboBox showing the complete theme name; and
- a full-width Quit Button whose composite content aligns `Quit` left and the
  muted `Ctrl+Q` shortcut right.

Hover and focus styling belongs to the complete interactive row. The design
adapts the leading-label/trailing-metadata ActionList pattern documented by
[GitHub Primer](https://primer.style/product/components/action-list/) and the
separation of persistent utilities from primary navigation used by
[Visual Studio Code](https://code.visualstudio.com/docs/configure/custom-layout).

## Alternatives rejected

- **Showcase-only background overrides:** setting every heading or list label
  background manually would hide the broken `Text` contract and fail in custom
  templates.
- **Permanent Popup canvases:** keeping popups open makes the promoted layer
  obscure the very trigger and prose the example is meant to explain.
- **Control-local wall-clock double-click state:** this would duplicate gesture
  policy across controls and make tests timing-dependent.
- **Static process-wide clipboard state:** process globals cross application
  ownership and complicate dispatcher and security guarantees.
- **A wider sidebar as the footer fix:** the current 28-cell width is adequate
  when controls receive the row; the broken two-column allocation is the actual
  cause of clipping.

## Documentation and tests

The change updates the Text, TextInput, input-routing, showcase architecture,
and showcase-testing contracts. Public/internal XML documentation covers click
count, word selection, clipboard ownership, and opacity behavior.

Regression proof includes:

- exact gallery/sidebar/page/header/body bounds at representative sizes;
- header and list rows whose text cells preserve the painted parent background;
- Popup pages with all promoted surfaces closed initially and opened by their
  corresponding triggers;
- Window stages whose child content and shadows remain inside readable stages;
- deterministic click-count reset and accumulation with a manual clock;
- Unicode word selection through routed double-click input;
- an application-level `Ctrl+C` → `Ctrl+V` path between focused inputs, plus
  cut, read-only, password, and empty-clipboard behavior;
- decoded `Ctrl+Q` showcase shutdown and continued bracketed-paste behavior;
- constrained footer layout without overlap or clipped theme content; and
- the complete `make format`, `make lint`, `make build`, and `make test` gates.
