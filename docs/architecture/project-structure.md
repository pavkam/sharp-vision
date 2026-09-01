# Project structure

## Overview

The solution contains five production libraries, five executable examples, and
eight test projects. Each library has its own test project; examples are
demonstrations whose behavior the owning libraries' tests already cover, so they
carry no test projects of their own.

```mermaid
flowchart LR
    Terminal["SharpVision.Terminal"]
    UI["SharpVision"]
    Document["SharpVision.Document"]
    FigletFonts["SharpVision.FigletFonts"]
    Syntax["SharpVision.SyntaxHighlighting"]
    Showcase["SharpVision.Showcase"]
    Snake["Snake"]
    TextEditor["TextEditor"]
    ProcessMonitor["ProcessMonitor"]
    TerminalDebugger["TerminalDebugger"]
    TerminalTests["SharpVision.Terminal.Tests"]
    Probe["SharpVision.Terminal.Probe"]
    UITests["SharpVision.Tests"]
    CompatibilityTests["SharpVision.Compatibility.Tests"]
    DocumentTests["SharpVision.Document.Tests"]
    UI --> Terminal
    FigletFonts -. "NuGet >= current version" .-> UI
    Document --> UI
    Syntax --> UI
    Showcase --> UI
    Showcase --> FigletFonts
    Showcase --> Document
    Snake --> UI
    Snake --> FigletFonts
    TextEditor --> UI
    ProcessMonitor --> UI
    TerminalDebugger --> UI
    TerminalTests -. tests .-> Terminal
    TerminalTests -. launches .-> Probe
    Probe --> Terminal
    UITests -. tests .-> UI
    UITests -. tests .-> FigletFonts
    CompatibilityTests -. public API snapshot .-> Terminal
    CompatibilityTests -. public API snapshot .-> UI
    CompatibilityTests -. public API snapshot .-> FigletFonts
    CompatibilityTests -. public API snapshot .-> Document
    CompatibilityTests -. public API snapshot .-> Syntax
    DocumentTests -. parser and model tests .-> Document
```

The FigletFonts, Document, SyntaxHighlighting, and shared test-support projects
each carry their own test project as well; the diagram shows a representative
subset of the test edges rather than all eight projects.

`SharpVision.Terminal` owns protocols, transport, capabilities, input events,
Unicode cell geometry, screen buffers, damage, and terminal output. It has no
reference to the UI project.

Its public runtime boundaries are: `Protocols`, which provides exact encoders
and streaming framing; `Input.InputDecoder`, which produces typed input values;
`Rendering.Frame`, `TerminalCanvas`, and `Renderer`, which handle semantic
output; `Transport.ITransport`, which performs bounded I/O; and
`Runtime.Session`, which owns mode leases and ordered input, resize, closure,
and fault delivery. Internal pooled storage never becomes a cross-project
contract.

### Friend access

`SharpVision.Terminal` grants `InternalsVisibleTo` only to its own test and
probe assemblies and to the UI test assembly. It deliberately does **not** grant
it to `SharpVision`.

The UI project consumes the terminal project exactly like any external consumer,
through public API only. That constraint is load-bearing: whenever the UI layer
needs something, the answer is a designed public seam, not friend access. If a
capability is not expressible publicly without leaking low-level machinery, the
terminal project publishes a focused facade that owns that machinery instead.

Two such seams provide this boundary:

| UI need                          | Public seam                                    | Stays internal                                                    |
| -------------------------------- | ---------------------------------------------- | ----------------------------------------------------------------- |
| Expand named terminfo programs   | `TerminalProfile.CreateProgramExpander`        | `Programs`, `DescriptionProgram`, `Interpreter`, `ProgramLimits`  |
| Obtain graphics-capable renderer | `Renderer(Capabilities, MultiplexerRoute?, …)` | `IGraphicsBackend`, `GraphicsBackendSelector`, backend identities |

`TerminalContext` and the whole `Backends` namespace also stay internal. The
session owns the only terminal context, and the application retains just the
immutable `TerminalProfile`, so no second context lineage can drift from the
resolved backend identity.

`SharpVision` owns the dispatcher, application lifecycle, traditional mutable
controls, layout, styling, focus, and routed input. It draws to the terminal
project's cell canvas and never emits escape bytes. The UI project provides
these infrastructure namespaces:

| Namespace                          | Shipped responsibility                                                         |
| ---------------------------------- | ------------------------------------------------------------------------------ |
| `SharpVision`                      | `Application`, its console bootstrap, and the events and accessors it exposes. |
| `SharpVision.Threading`            | Single-owner dispatcher, invocation, and idle transition.                      |
| `SharpVision.Controls`             | Foundational mutable control tree, ownership, invalidation, and drawing.       |
| `SharpVision.Controls.Display`     | Text, images, indicators, and passive presentation controls.                   |
| `SharpVision.Controls.Charts`      | Reactive bar, line, area, and compact trend charts.                            |
| `SharpVision.Controls.Input`       | Buttons, command bars, suggestion editors, pickers, and value controls.        |
| `SharpVision.Controls.Layout`      | Panels, overlays, structural chrome, and tables.                               |
| `SharpVision.Controls.Collections` | Lists, tabs, trees, typed collections, and item realization.                   |
| `SharpVision.Controls.Scrolling`   | The ScrollBar control and its glyph and style values.                          |
| `SharpVision.Menus`                | Menus, typed menu entries, and context menus.                                  |
| `SharpVision.Navigation`           | Breadcrumb paths, sidebar navigation controls, and entries.                    |
| `SharpVision.Surfaces`             | Shared elevated-surface lifecycle, bounds, and modality coordination.          |
| `SharpVision.Popups`               | Anchored popup, flyout, and tooltip surfaces.                                  |
| `SharpVision.Windows`              | Free-standing retained window surfaces.                                        |
| `SharpVision.Dialogs`              | Complete retained modal tasks with typed options and results.                  |
| `SharpVision.Layout`               | Shared box geometry, measure/arrange, and track allocation.                    |
| `SharpVision.Scrolling`            | Scroll axes, visibility, chrome, range/thumb math, and transition events.      |
| `SharpVision.Input`                | Shared routed input, focus, hit testing, and pointer capture.                  |
| `SharpVision.Styling`              | Shared style resources, chrome contracts, and visual-state resolution.         |
| `SharpVision.DataBinding`          | Typed model bindings, property-path observation, and collection tracking.      |
| `SharpVision.Fonts`                | FIG-font parsing, layout metadata, and glyph rendering for FIGlet text.        |
| `SharpVision.Text`                 | Grapheme-safe text layout, wrapping, editing, and selection primitives.        |
| `SharpVision.Notifications`        | The `Toast` notification surface and its stacking host.                        |
| `SharpVision.Runtime`              | Application-facing runtime seams such as `IClipboardCopySource`.               |

`SharpVision.FigletFonts` is an optional package over the public FIGlet engine
in `SharpVision`. It owns `FigletCatalog`, `FigletFontInfo`, the curated
manifest, third-party notices, and 19 independent embedded FIGfont resources. It
references `SharpVision` only through the NuGet dependency `SharpVision` at the
core's own version being built, derived from `SharpVisionVersion` rather than
pinned as a literal; it has no project reference to the UI project. The two
packages publish independently and are not expected to share a version number.
The reverse dependency is forbidden, so installing `SharpVision` never brings
the catalog or its assets into an application.

`SharpVision.Document` is an optional leaf over public SharpVision UI seams. It
owns the semantic block/inline tree, hybrid retained presenter,
`IDocumentFormatReader`, and the native Markdown reader. Core SharpVision never
references it, so applications that do not display documents carry neither the
document model nor Markdown parser.

The UI project ships the complete
[control catalog](../controls/index.md#control-catalog): layout panels, text and
editing, asynchronous suggestions, selection and item controls, menus, context
menus, popups, tooltips, flyouts, windows, intrinsic container scrolling,
styling, focus, and routed input all follow these feature and shared-service
boundaries. Border and shadow properties live on `ControlBase`, and `Border`
always participates in its base box model. The sealed control render pipeline
always paints configured intrinsic chrome around `OnRenderContent`; specialized
controls select narrow chrome options rather than bypassing the pipeline.
Neither feature requires a dedicated wrapper type or moves terminal protocol or
renderer behavior into the UI layer.

`SharpVision.Controls` is foundational in the sense that every feature namespace
derives from and depends on it, not in the sense that it depends on nothing. It
may reference a feature namespace when a foundational type genuinely _is_ one of
that feature's controls. Two such references are deliberate and permanent:

| Root type                   | Feature dependency             | Why it is not a layering violation                                               |
| --------------------------- | ------------------------------ | -------------------------------------------------------------------------------- |
| `Screen`/`PresentationHost` | `Controls.Layout.Overlay`      | A screen's presentation root is a layering panel; nothing else it could be.      |
| `Container`                 | `Controls.Scrolling.ScrollBar` | Intrinsic `AutoScroll` chrome is the shipped scrollbar, not a private lookalike. |

Both are ordinary same-assembly references with no cycle. The alternative —
root-level lookalike panels kept only so a dependency arrow points one way —
would duplicate shipped behavior to satisfy a diagram. Feature namespaces must
not acquire further root privileges on the strength of this rule; it covers
these two compositions and nothing else.

The separate [dialog catalog](../dialogs/index.md#dialog-catalog) derives typed
tasks from Window without moving them into the Controls folder or namespace. The
dialog object is its own directly retained and presented surface. A dialog may
own private filesystem or workflow collaborators, but its visible tree still
follows the ordinary control ownership, layout, modality, styling, input, and
dispatcher contracts.

`examples/Showcase` contains `SharpVision.Showcase`, which owns no library
behavior and composes public APIs into a responsive gallery. `examples/Snake`,
`examples/TextEditor`, and `examples/ProcessMonitor` are focused application
examples. `examples/TerminalDebugger` composes public capability, input,
terminal-service, rendition, Unicode, and graphics APIs into an interactive
diagnostic dashboard. The production libraries never reference an example or
test project.

`SharpVision.Terminal.Probe` is a test-support executable for process-level
terminal checks. `SharpVision.Terminal.Tests` owns its lifecycle and assertions;
production code does not use it.

`SharpVision.Compatibility.Tests` owns the versioned public API baselines for
all five libraries. It references production projects only from the test layer
and participates in the solution-wide gate. The normative reconciliation
workflow is defined by the
[public API compatibility contract](../testing/correctness-model.md#public-api-compatibility).

## Namespace and file boundaries

Namespaces provide context, so public names avoid repeating `Terminal`,
`SharpVision`, and `ControlBase` as affixes. Each file has one primary
responsibility, and internal helpers stay inside the lowest layer that owns
their invariant.

## Change rule

A feature begins in the lowest layer that owns its invariant. Terminal behavior
starts with a typed terminal seam before the UI layer consumes it; UI-only
behavior stays in `SharpVision`. User-visible APIs then receive example proof at
the appropriate scale. Tests at each layer assert that the dependency direction
remains one-way.

## Expected behavior

The boundaries above are backed by evidence at three layers:

| Layer        | Required evidence                                                                                                          |
| ------------ | -------------------------------------------------------------------------------------------------------------------------- |
| Build        | Production libraries compile without example/test references and preserve the Terminal-to-UI-to-optional-assets direction. |
| Architecture | Namespace, one-type-per-file, public API, and forbidden-type tests cover declared boundaries.                              |
| Consumer     | Public examples compile without friend access or production dependency inversion.                                          |
