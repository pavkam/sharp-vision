# Showcase exit affordance and ComboBox theme picker

Date: 2026-07-14

## Problem

The showcase gallery has no visible way to quit, and `Ctrl+C` does not reliably
exit it. The sidebar theme selector is two flat `Light`/`Dark` buttons that do
not indicate the active theme and do not scale as more themes are added.

## Root cause: why Ctrl+C does not exit

`ConsoleRun.CreateTerminalOptions` uses the default
`Options.Keyboard = Enhancement.Disambiguate | Enhancement.EventTypes`. When the
terminal proves Kitty keyboard support, `Session` leases the Kitty keyboard
protocol. In that mode the terminal reports `Ctrl+C` as a key event
(`CSI 99;5u`) instead of generating the legacy `ETX` byte, so the OS never
raises `SIGINT` and the host's `Console.CancelKeyPress` handler in
`Application.RunConsoleAsync` never fires. The gallery has no key handler for
`Ctrl+C`, so nothing happens.

In a terminal without Kitty keyboard support the protocol is not leased,
`Ctrl+C` still raises `SIGINT`, and the host path exits. This is why the
behavior is inconsistent across terminals.

The fix is to handle the quit chord as a **key** inside the gallery, which
covers the Kitty case; the existing `SIGINT` host path continues to cover legacy
terminals. No change to negotiation, `isig`, or the host is needed.

## Design

Scope: `src/SharpVision.Showcase/` and its tests plus affected docs. No
framework changes; uses the existing public `Application.Closed()` and
`ComboBox`.

### Exit

- Add a `Quit` `Button` to the sidebar footer whose `Click` calls a new private
  `Gallery.RequestQuit()`.
- `RequestQuit()` calls `Application.Closed()`, the public sink method that
  enqueues a `Closed` record and drives `BeginStopping(forced: true)` on the
  dispatcher. Synchronous, no unobserved task, graceful exit (code 0).
- Register a global key handler on the gallery screen root
  (`AddHandler(Events.Key, OnGlobalKey)`, the seam `List` already uses). On a
  `Ctrl+C` press (`Code.Character`, rune `c`, `Modifiers.Control`) in the
  `Phase.Preview` pass, call `RequestQuit()` and mark the event handled. Preview
  makes `Ctrl+C` exit from anywhere, including while a text-input demo is
  focused. No `Ctrl+Q`.

### Theme picker

- Replace the two theme buttons with a `ComboBox`.
- Add a private ordered `ThemeCatalog` array pairing each display name with a
  `Theme` (Light with `Themes.White`, Dark with `Themes.Dark`). Adding a theme
  later is one array entry.
- `Items` are the names; default `SelectedIndex` is the `Dark` entry to match
  `OnAttach` setting `Themes.Dark`; `SelectionChanged` calls
  `SetTheme(ThemeCatalog[index].Theme)`. `SetTheme` already no-ops before
  attach, so selection wiring during construction is harmless.

### Footer and docs

- Footer top to bottom: dim `Theme` label, the `ComboBox`, the `Quit` button,
  and a dim `Ctrl+C to quit` hint. Footer height grows to fit.
- Update `ThemingShowcasePane` instructional text ("Light and Dark buttons" ->
  "theme picker in the sidebar footer") and `docs/architecture/showcase.md`.

## Tests

- Rewrite `ThemeGalleryTests.Theme_WhenLightIsSelected_PublishesWhiteThemeAsync`
  to select the `Light` entry through the `ComboBox` and assert
  `Application.Theme` is `Themes.White`.
- New: focused control, queue `[99;5u` (`Ctrl+C`), assert the application
  completes with no failure.
- New: activate the `Quit` button, assert the application completes with no
  failure.

## Out of scope (YAGNI)

- No quit confirmation prompt, no `Ctrl+Q`.
- No change to host, negotiation, or `isig` behavior.
