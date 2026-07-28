# SharpVision Agent Instructions

## Product

SharpVision is a .NET 10 terminal user interface library. Correct terminal
behavior, deterministic UI state, Unicode fidelity, bounded memory use, and
observable proof outrank convenience shortcuts.

The normative product specification begins at `docs/index.md`. Treat docs as
part of the implementation: behavior is incomplete until its specification, API
documentation, tests, and showcase example agree.

## Repository map

- `src/SharpVision.Terminal/` contains terminal protocols, transport, input,
  capabilities, Unicode cell geometry, buffers, and rendering.
- `src/SharpVision/` contains the dispatcher, mutable controls, layout, input
  routing, focus, styling, scrolling, menus, popups, and windows.
- `src/SharpVision.Showcase/` demonstrates every shipped control and state.
- `tests/` contains the terminal and UI suites plus the unprivileged consumer
  contract suite. The showcase is compiled as a production example and has no
  dedicated test project.
- `docs/` contains normative architecture, protocol, concept, control, and test
  specifications.
- `.codex/skills/` contains focused routing and invariants for each domain.

`SharpVision` may reference `SharpVision.Terminal`. The showcase may reference
both libraries. Dependencies must never point from a lower layer to a higher
layer or from production code to tests.

## Orientation workflow

1. Read the relevant domain skill before changing behavior.
2. Read the linked normative docs and the nearest existing tests.
3. Write a focused failing test that demonstrates the desired public behavior.
4. Implement the smallest correct change and verify the focused test.
5. Update affected docs and showcase examples in the same change.
6. Run the focused checks, then the repository quality gates.

Do not infer terminal behavior from memory when a primary standard or current
terminal specification is available. Record the source and the supported version
in the protocol document.

## C# and API rules

- Target .NET 10 and C# 14.
- Use file-scoped namespaces and `var` for local variables.
- Place `using` directives after the file-scoped `namespace` declaration, not
  before it. Put shared standard and project-wide imports in each project's
  `GlobalUsings.cs` instead of repeating them in individual source files.
  Assembly attribute files may keep top-level `using` directives before
  `[assembly:]` metadata when required.
- Put every named C# type in its own file named exactly after the type, with the
  generic arity omitted: `Button` belongs in `Button.cs`. This applies to
  classes, structs, records, interfaces, enums, delegates, production code,
  generated source, tests, and test helpers. Do not declare nested named types
  or place two types in one file. There are no generated-source exceptions.
- Declare immutable value types as `readonly struct`, `readonly record struct`,
  or `readonly ref struct`. Use a mutable struct only when mutation is intrinsic
  to its role, such as an enumerator, parser cursor, accumulator, or native
  interop buffer; keep that mutability internal where possible.
- Prefer readonly structs over classes for small immutable wrapper values when
  copying is cheap and the default value is valid. Use a class when reference
  identity, polymorphism, shared mutable state, disposal, weak-table storage, or
  a large copy cost is part of the contract.
- Never use primary constructors or positional record declarations. Declare
  every constructor explicitly, validate all arguments before assigning state,
  and document its validation and exceptions. Preserve explicit `Deconstruct`
  members when a value type intentionally offers tuple-style consumption.
- Use named `#region` and `#endregion` blocks in substantial C# files that have
  genuinely distinct responsibility areas, such as lifecycle, input handling,
  layout, rendering, or protocol families. Name regions by responsibility, avoid
  trivial or deeply nested regions, and split the file instead when a region
  boundary reveals a separate type or unrelated responsibility.
- Prefer `Rune`, `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, `ReadOnlyMemory<T>`,
  and `IBufferWriter<T>` in text, protocol, and rendering paths. Do not reduce
  Unicode input to `char` or allocate strings in hot loops.
- Prefer contextual identifiers. Use `Capabilities`, `Cell`, and `Button`, not
  `TerminalCapabilities`, `TerminalCell`, or `ButtonControl` when the namespace
  already provides context.
- Avoid repeated prefixes and suffixes across related members.
- Validate every argument received by a public method, constructor, indexer, or
  property setter before changing observable state.
- Document all thrown exceptions in public XML documentation.
- Add XML documentation to every public and internal type and member. Explain
  purpose, ownership, units, threading, side effects, examples, and exceptions
  where relevant. Do not write comments that merely repeat the signature.
- Use `Debug.Assert` for internal invariants that should be impossible after
  public validation. Assertions do not replace runtime validation.
- Put empty lines between logical blocks.
- Comment blocks that implement non-obvious protocol, Unicode, layout, damage,
  lifetime, or concurrency rules. Explain why the rule exists.
- Keep files focused. Split by responsibility rather than accumulating broad
  utility classes.
- Use deterministic, culture-independent encoding and parsing.
- Never expose pooled memory after return, disposal, frame completion, or the
  documented ownership boundary.

## Terminal correctness

- Keep protocol parsing incremental and bounded across arbitrary read
  fragmentation.
- Treat malformed, oversized, unknown, and interrupted sequences as explicit
  recovery cases.
- Degrade unsupported environmental features safely by default. Strict mode may
  promote diagnostics to exceptions, but it must not change valid output.
- Restore terminal modes in `finally` paths. Cleanup failures must not hide the
  original exception.
- Preserve cell and pixel mouse coordinates when supplied.
- Segment extended grapheme clusters before measuring cells. Never draw or clear
  half of a wide cluster.
- Controls render to the cell canvas; control code must not emit escape bytes.

## UI correctness

- Controls are traditional mutable objects. Do not introduce virtual trees,
  function components, reconciliation, or hook-style state.
- All control mutation is dispatcher-affine. Background work returns through the
  dispatcher.
- Property changes invalidate only the required measure, arrange, or render
  phase.
- A child has at most one parent. Reject null children, duplicates, cycles, and
  cross-parent insertion.
- Input uses documented preview/bubble routing, focus, and pointer capture.
- Resize, idle, lifecycle, transport, and frame events remain ordered on the
  dispatcher.
- Percentage sizing inside unbounded measure and automatic scrollbar feedback
  follow their normative algorithms; do not invent local exceptions.
- Scrolling and grow/shrink are intrinsic `Container` properties (`AutoScroll`,
  `AutoSize`, `AutoSizeMode`), not a dedicated scroll container. There is no
  `ScrollView` type; any container becomes scrollable or content-sized by
  setting those properties directly.
- Border and shadow are intrinsic `Control` properties (`Border`,
  `BorderGlyphs`, `HasShadow`, and the related style properties), not wrapper
  controls. There are no `Border` or `Shadow` types. Use an ordinary container
  when chrome needs a distinct layout, styling, ownership, or routed-ancestry
  node. The sealed render pipeline paints intrinsic chrome around
  `OnRenderContent`; custom controls override that content hook and do not call
  a chrome helper.
- Build a composite control by deriving from `CompositeControl`, creating its
  retained root in the concrete constructor, and calling `InitializeContent`
  exactly once. `View` and measure-time `Build()` do not exist. Use
  `ContentControl` for caller-replaceable single content and `ItemsControl` for
  typed semantic collections with private presentation hosts; the layout/render
  override seams are `MeasureOverride`/`ArrangeOverride`/`OnRender`.

## Hosting

- Host an interactive console through `SharpVision.Runtime.ConsoleApplication`
  (`CreateBuilder`/`RunAsync`) and its fluent `ConsoleApplicationBuilder`, not
  by hand-wiring `ConsoleHost`, transport, and terminal `Options`.
- Reach implemented output protocols only through `Application.Terminal`
  (`ITerminalServices`): `Bell.Ring()` for the audible alert, `SetTitle` for OSC
  2, and `Clipboard` for capability-gated OSC 52/Kitty clipboard access. Do not
  emit bell, title, or clipboard bytes directly from control code.
- `TreatControlCAsInput` (on `ConsoleRunOptions`/the builder) delivers Ctrl+C as
  decoded input instead of cooperative shutdown; a host that sets it owns its
  own exit path.

## Tests

- Use xUnit v3, Shouldly, and Arrange/Act/Assert.
- Use Moq only for genuine interaction boundaries. Prefer deterministic fakes
  for transports, clocks, dispatchers, terminals, and frame sinks.
- Name tests `MethodName_WhenThis_ThatIsExpected`.
- Watch each new test fail for the expected reason before implementation.
- Test observable output and state rather than private calls.
- Protocol encoders require exact-byte tests.
- Streaming decoders require every possible read-fragment boundary for each
  representative sequence.
- Rendering tests compare the final virtual screen and emitted terminal bytes
  across frames.
- Unicode tests cover combining marks, variation selectors, ZWJ sequences,
  ambiguous width, clipping, wrapping, and wide-cell repair.
- Layout tests cover fixed, percentage, automatic, proportional, min/max,
  margin, padding, alignment, resize, and overflow interactions.
- Scrolling tests cover wheel/pixel delta, keyboard, track, thumb, nested
  propagation, resize, content changes, and both-bar feedback.
- Add randomized/property-style tests for parsers, geometry, layout invariants,
  and frame-diff equivalence.
- End-to-end tests drive terminal input through the dispatcher and controls to
  final output bytes.
- Every control requires a showcase page and representative behavioral and
  rendering tests in the UI suite before it is complete.

Focused tests use Microsoft Testing Platform filters, for example:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*DecoderTests" --timeout 60s
```

## Documentation

- Keep one focused file per terminal protocol or extension.
- Keep one public API specification per control.
- Link to the relevant section inline where a concept affects another spec.
- Include Mermaid diagrams only when they clarify ownership, sequence, state, or
  dependency relationships.
- Never claim support without typed implementation and tests. Use the coverage
  matrix states exactly.
- Avoid placeholders such as TODO, TBD, “handle edge cases,” or “implement
  later” in normative specs.

## Verification

Run focused tests during development. Before declaring a phase complete, run:

```bash
make format
make lint
make build
make test
```

The result must contain zero build warnings, zero build errors, discovered tests
at or above the configured minimum, and no Markdown or link failures.

## Git discipline

- Preserve unrelated user work.
- Stage only intentional files.
- Keep commits small and aligned to a verified task.
- Do not use destructive reset, checkout, restore, clean, or force-push
  operations without explicit user authorization.
