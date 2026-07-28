# Feature support

## Feature support

This page maps application needs to the section that proves current behavior. It
is a navigation surface, not a second coverage ledger. Terminal support states
are owned exclusively by the
[protocol coverage matrix](../protocols/coverage-matrix.md#coverage).

## Application and UI

| Need                                             | Public surface                                               | Authoritative contract                                                                  |
| ------------------------------------------------ | ------------------------------------------------------------ | --------------------------------------------------------------------------------------- |
| Interactive console hosting                      | `ConsoleApplication` and `ConsoleApplicationBuilder`         | [Hosting](../concepts/hosting.md#hosting-contract)                                      |
| Retained mutable controls                        | `Control`, `Container`, `ContentControl`, `CompositeControl` | [Control catalog](../controls/index.md#control-catalog)                                 |
| Strongly typed model binding                     | `Bind`, `BindItems`, `BindSelection`, `BindingMode`          | [Data binding](../concepts/data-binding.md#data-binding-contract)                       |
| Fixed, auto, percentage, and proportional layout | `Length`, `Stack`, `Dock`, `Grid`, `Overlay`                 | [Layout](../concepts/layout.md#layout-contract)                                         |
| Elevated windows, dialogs, popups, and tooltips  | `FloatingSurface`, `Window`, `Dialog<TResult>`, `Popup`      | [Floating surfaces](../concepts/floating-surfaces.md#floating-surface-contract)         |
| Routed keyboard and pointer input                | Preview/bubble events, focus, pointer capture                | [Input routing](../concepts/input-routing.md#input-routing-contract)                    |
| Scrollable content                               | `Container.AutoScroll` and scrollbar policy                  | [Scrolling](../concepts/scrolling.md#scrolling-contract)                                |
| Styling and themes                               | `Color` and `Theme`                                          | [Themes](../concepts/themes.md#theme-file-contract)                                     |
| Unicode-safe cells                               | Grapheme segmentation, width policy, wide-cell repair        | [Unicode geometry](../concepts/unicode-cell-geometry.md#unicode-cell-geometry-contract) |
| Menus, popups, and windows                       | Retained controls and popup render layer                     | [Control catalog](../controls/index.md#control-catalog)                                 |
| Images                                           | `Image`, `ImageStretch`, and immutable graphics sources      | [Image control](../controls/display/image.md#image-contract)                            |

Every shipped concrete control has a C# example and test obligations on its
individual page. The
[layout-and-controls walkthrough](../walkthroughs/layout-and-controls.md#compose-layout-and-controls)
shows how the surfaces combine.

## Terminal input and output

| Need                                        | Current evidence                                                                                                                     |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Keyboard input, including Kitty enhancement | [Kitty keyboard](../protocols/kitty-keyboard.md#kitty-keyboard-contract) and [ANSI/VT](../protocols/ansi-vt.md#ansi-and-vt-contract) |
| Mouse cells and pixels                      | [Mouse reporting](../protocols/mouse.md#mouse-reporting-contract)                                                                    |
| Bracketed paste and terminal focus          | [Paste and focus](../protocols/paste-focus.md#paste-and-focus-contract)                                                              |
| Color and text attributes                   | [SGR](../protocols/sgr.md#sgr-contract)                                                                                              |
| Bell, title, and clipboard services         | [Terminal-services walkthrough](../walkthroughs/terminal-services.md#use-terminal-services)                                          |
| Synchronized frame output                   | [Synchronized output](../protocols/synchronized-output.md#synchronized-output-contract)                                              |
| Device and capability negotiation           | [Capabilities](../architecture/capabilities.md#capability-contract)                                                                  |
| tmux and GNU screen passthrough             | [tmux](../protocols/tmux.md#tmux-contract) and [GNU screen](../protocols/gnu-screen.md#gnu-screen-contract)                          |

The exact state for every protocol family—typed and implemented, decoded and
observable, extension API with safe fallback, or unsupported with a specific
reason—is in the [coverage table](../protocols/coverage-matrix.md#coverage).

## Explicit boundaries

Kitty graphics, sixel, and iTerm2 multipart images are connected from the public
Image control through Application-owned backend selection and shutdown. A
generic OSC, DCS, APC, capability parser, or environment name still does not
authorize raster output; iTerm2 specifically requires an explicit 3.5+ multipart
override, and detected multiplexer layers never permit an unwrapped direct
fallback around their route policy.

Windows console hosting is implemented and unit-tested at its mode-flag and
P/Invoke boundaries, but the current limitation on real Windows-console
validation is recorded in [hosting](../concepts/hosting.md#windows). Unix
hosting uses the real tty and can preserve both cell and pixel dimensions where
the platform reports them.

## Proof and limits

- [Correctness model](../testing/correctness-model.md#correctness-model) defines
  acceptable proof levels.
- [Protocol testing](../testing/terminal-protocols.md#terminal-protocol-testing)
  covers exact bytes, fragmentation, malformed input, recovery, and routing.
- [Control integration](../testing/controls-integration.md#control-and-integration-testing)
  covers terminal input through controls to final surfaces and bytes.
- [Performance](../testing/performance.md#performance-testing) records current
  bounded-memory and throughput gates.
