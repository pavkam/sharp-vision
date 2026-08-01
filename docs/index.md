# SharpVision documentation

SharpVision is a retained, mutable .NET 10 terminal UI library. Its
documentation is both a user guide and the product specification: a behavior is
complete only when specification, implementation, tests, XML documentation, and
showcase evidence agree.

## Choose a path

| Goal                               | Start here                                                                            | Continue with                                                                          |
| ---------------------------------- | ------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Build a first application          | [First application](walkthroughs/first-application.md#build-your-first-application)   | [Layout and controls](walkthroughs/layout-and-controls.md#compose-layout-and-controls) |
| React to input and update state    | [State and events](walkthroughs/state-and-events.md#state-input-and-events)           | [Input routing](concepts/input-routing.md#overview)                                    |
| Bind controls to model state       | [Data binding](concepts/data-binding.md#overview)                                     | [State and events](walkthroughs/state-and-events.md#state-input-and-events)            |
| Add keyboard access keys           | [Access keys](concepts/access-keys.md#overview)                                       | [Focus](concepts/focus.md#overview)                                                    |
| Choose existing files              | [File picker](dialogs/file-picker-dialog.md#overview)                                 | [Dialogs](dialogs/index.md#dialog-catalog)                                             |
| Display terminal-safe images       | [Image control](controls/display/image.md#overview)                                   | [ImageSource ownership](concepts/images.md#overview)                                   |
| Update the UI from background work | [Background work](walkthroughs/background-work.md#background-work-and-the-dispatcher) | [Threading](concepts/threading.md#overview)                                            |
| Use terminal capabilities safely   | [Terminal services](walkthroughs/terminal-services.md#use-terminal-services)          | [Coverage matrix](protocols/coverage-matrix.md#coverage)                               |
| Build a reusable component         | [Custom controls](walkthroughs/custom-controls.md#build-a-custom-control)             | [Control catalog](controls/index.md#control-catalog)                                   |
| Understand floating UI             | [Floating surfaces](concepts/floating-surfaces.md#overview)                           | [Modality](concepts/modality.md#overview)                                              |
| Understand the implementation      | [Architecture map](architecture/index.md#architecture-map)                            | [Terminal backends](architecture/terminal-backends.md#overview)                        |
| Verify or contribute behavior      | [Testing map](testing/index.md#test-map)                                              | [Contributing](../CONTRIBUTING.md)                                                     |

```mermaid
flowchart LR
    Start["First application"] --> Layout["Layout and controls"]
    Layout --> Events["State and routed events"]
    Events --> Async["Dispatcher and background work"]
    Async --> Terminal["Terminal services and capabilities"]
    Terminal --> Custom["Custom controls"]

    Layout -. reference .-> Controls["Control reference"]
    Events -. reference .-> Concepts["Shared concepts"]
    Terminal -. reference .-> Protocols["Protocol specifications"]
    Custom -. internals .-> Architecture["Architecture"]
    Architecture -. proof .-> Testing["Testing specifications"]
```

The solid path is the recommended learning order. Dotted links lead from a
task-oriented walkthrough to the reference section that owns the details.

## Documentation sets

- [Walkthroughs](walkthroughs/index.md#walkthroughs) teach complete tasks with
  C# examples and then link to the relevant reference pages.
- [Control API specifications](controls/index.md#control-catalog) explain each
  component's purpose, properties, defaults, validation, events, layout,
  interaction, example, and expected behavior.
- [Dialog API specifications](dialogs/index.md#dialog-catalog) define complete
  modal tasks, typed options and results, ownership, interaction, and cleanup.
- [Shared concepts](concepts/index.md#concept-map) define layout, focus, input,
  styling, threading, Unicode geometry, scrolling, hosting, and lifecycle.
- [Architecture](architecture/index.md#architecture-map) explains dependency
  direction, ownership, runtime ordering, rendering, capabilities, memory, and
  failure boundaries.
- [Terminal protocols](protocols/index.md#protocol-families) define wire
  grammar, bounds, recovery, detection, fallback, security, and typed surfaces.
- [Feature support](features/index.md#feature-support) is the reader-facing map
  from common capabilities to their authoritative support evidence.
- [Testing](testing/index.md#test-map) defines what counts as proof for control,
  protocol, Unicode, rendering, integration, performance, and pseudoterminal
  behavior.

## Where the core rules live

The [protocol coverage matrix](protocols/coverage-matrix.md#coverage) states
what the current code proves.
[Runtime routing](protocols/runtime-routing.md#overview) owns how parsed input,
replies, and bounded extension strings reach their consumers.
[Project structure](architecture/project-structure.md#overview) owns dependency
direction, while [terminal backends](architecture/terminal-backends.md#overview)
separates physical connections, fixed emulator identity, protocol extensions,
capability authorization, and renderer-owned graphics, and the
[discovery pipeline](architecture/discovery-pipeline.md#overview) owns evidence
precedence and backend resolution.
[Invalidation](concepts/invalidation.md#overview) owns phase dependencies,
propagation, coalescing, update scheduling, and retry. The
[rendering pipeline](architecture/rendering-pipeline.md#overview) owns cell
drawing, damage, and terminal output.

Cross-document references target the section that owns a rule. Walkthroughs and
examples illustrate those rules; they do not silently create new behavior.

## Documentation rules

- The [documentation guide](documentation-guide.md#overview) defines the
  required sections, API tables, proof formatting, ownership, and validation
  rules for every document kind.
- Protocol documents define sources, grammar, limits, detection, fallback,
  security, typed surfaces, support state, and expected behavior.
- Architecture documents define ownership and cross-layer flow.
- Concept documents define behavior shared by several controls or layers.
- Control documents define one public control API or authoring role each.
- Dialog documents define one complete modal task and its typed result.
- Testing documents define acceptable correctness evidence.
- Walkthroughs assemble public APIs into complete, copyable tasks.

Repository participation is defined by [Contributing](../CONTRIBUTING.md),
[Security](../SECURITY.md), [Support](../SUPPORT.md), and the
[Code of Conduct](../CODE_OF_CONDUCT.md).
[Continuous integration](testing/continuous-integration.md#overview) defines the
public quality gate.
