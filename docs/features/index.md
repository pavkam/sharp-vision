# Feature support

This page maps common application needs to the documentation that describes and
proves the current behavior. It is a navigation aid, not a second coverage
table; the current support state for each terminal protocol lives only in the
[protocol coverage matrix](../protocols/coverage-matrix.md#coverage).

## Application and UI

| Need                                             | Public surface                                                       | Reference                                                          |
| ------------------------------------------------ | -------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Interactive console hosting                      | `ConsoleApplication` and `ConsoleApplicationBuilder`                 | [Hosting](../concepts/hosting.md#overview)                         |
| Retained mutable controls                        | `ControlBase`, `Container`, `ContentControl`, `CompositeControlBase` | [Control catalog](../controls/index.md#control-catalog)            |
| Strongly typed model binding                     | `Bind`, `BindItems`, `BindSelection`, `BindingMode`                  | [Data binding](../concepts/data-binding.md#overview)               |
| Editable asynchronous suggestions                | `SuggestionInput` and `SuggestionResolver`                           | [SuggestionInput](../controls/input/suggestion-input.md#overview)  |
| Fixed, auto, percentage, and proportional layout | `Length`, `Stack`, `Dock`, `Grid`, `Overlay`                         | [Layout](../concepts/layout.md#overview)                           |
| Elevated windows, dialogs, popups, and tooltips  | `FloatingSurfaceBase`, `Window`, `Dialog<TResult>`, `Popup`          | [Floating surfaces](../concepts/floating-surfaces.md#overview)     |
| Routed keyboard and pointer input                | Preview/bubble events, focus, pointer capture                        | [Input routing](../concepts/input-routing.md#overview)             |
| Scrollable content                               | `Container.AutoScroll` and scrollbar policy                          | [Scrolling](../concepts/scrolling.md#overview)                     |
| Styling and themes                               | `Color` and `Theme`                                                  | [Themes](../concepts/themes.md#overview)                           |
| Unicode-safe cells                               | Grapheme segmentation, width policy, wide-cell repair                | [Unicode geometry](../concepts/unicode-cell-geometry.md#overview)  |
| Hierarchical path navigation                     | `Breadcrumb` and `BreadcrumbItem`                                    | [Breadcrumb](../controls/navigation/breadcrumb.md#overview)        |
| Menus, popups, and windows                       | Retained controls and popup render layer                             | [Control catalog](../controls/index.md#control-catalog)            |
| Images                                           | `Image`, `ImageStretch`, and immutable graphics sources              | [Image control](../controls/display/image.md#overview)             |
| Toast notifications                              | `Toast` and its edge-slot stacking                                   | [Toast](../controls/notifications/toast.md#overview)               |
| Rich documents and Markdown                      | `Document` and `MarkdownDocumentReader`                              | [Markdown documents](../concepts/markdown-documents.md#overview)   |
| Syntax-highlighted source display                | `CodeView` and `SyntaxDefinitionCatalog`                             | [Syntax highlighting](../concepts/syntax-highlighting.md#overview) |
| Text selection and clipboard copy                | `IsTextSelectionEnabled` and selection commands                      | [Text selection](../concepts/text-selection.md#overview)           |

Every shipped control documents a C# example and its expected behavior on its
own page. The
[layout-and-controls walkthrough](../walkthroughs/layout-and-controls.md#compose-layout-and-controls)
shows how these pieces combine.

## Terminal input and output

| Need                                        | Current evidence                                                                                          |
| ------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Keyboard input, including Kitty enhancement | [Kitty keyboard](../protocols/kitty-keyboard.md#overview) and [ANSI/VT](../protocols/ansi-vt.md#overview) |
| Mouse cells and pixels                      | [Mouse reporting](../protocols/mouse.md#overview)                                                         |
| Bracketed paste and terminal focus          | [Paste and focus](../protocols/paste-focus.md#overview)                                                   |
| Color and text attributes                   | [SGR](../protocols/sgr.md#overview)                                                                       |
| Bell, title, and clipboard services         | [Terminal-services walkthrough](../walkthroughs/terminal-services.md#use-terminal-services)               |
| Synchronized frame output                   | [Synchronized output](../protocols/synchronized-output.md#overview)                                       |
| Device and capability negotiation           | [Capabilities](../architecture/capabilities.md#overview)                                                  |
| tmux and GNU screen passthrough             | [tmux](../protocols/tmux.md#overview) and [GNU screen](../protocols/gnu-screen.md#overview)               |

The exact state of every protocol family—typed and implemented, decoded and
observable, extension API with safe fallback, or unsupported with a specific
reason—is in the [coverage table](../protocols/coverage-matrix.md#coverage).

## Explicit boundaries

The public `Image` control reaches Kitty graphics, sixel, and iTerm2 multipart
images through backend selection and shutdown that the `Application` owns.
Detecting a generic OSC, DCS, or APC parser—or recognizing a terminal by name—is
not enough to turn on raster output: iTerm2 requires a positive capability query
reply or an explicit override, corroborated to the 3.5+ multipart protocol, and
when a multiplexer is detected, graphics always go through its wrapping policy
rather than falling back to unwrapped direct output.

Windows console hosting is implemented, unit-tested at its mode-flag and
P/Invoke boundaries, and exercised against a real ConPTY pseudo console in the
Windows continuous-integration lane; see
[hosting](../concepts/hosting.md#windows) for the platform rules. Unix hosting
uses the real tty and preserves both cell and pixel dimensions where the
platform reports them.

## Proof and limits

- [Correctness model](../testing/correctness-model.md#correctness-model) defines
  acceptable proof levels.
- [Protocol testing](../testing/terminal-protocols.md#terminal-protocol-testing)
  covers exact bytes, fragmentation, malformed input, recovery, and routing.
- [Control integration](../testing/controls-integration.md#control-and-integration-testing)
  covers terminal input through controls to final surfaces and bytes.
- [Performance](../testing/performance.md#performance-testing) records current
  bounded-memory and throughput gates.
