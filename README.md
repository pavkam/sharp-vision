# SharpVision

SharpVision is a high-performance .NET 10 terminal UI library under active,
specification-first development.

## Current status

The repository foundation is complete: solution boundaries, strict compiler and
format policy, CI, normative specifications, agent guardrails, and test harness
are installed. Terminal protocol and control implementations begin in the next
phase. The [coverage matrix](docs/protocols/coverage-matrix.md#coverage) is the
authoritative support statement; the project does not claim implemented
protocols or controls before their typed tests pass.

## Projects

- `SharpVision.Terminal` is the low-level terminal protocol, input, Unicode cell
  geometry, rendering, capability, and transport library.
- `SharpVision` is the traditional mutable-control UI toolkit.
- `SharpVision.Showcase` is the responsive interactive control gallery.
- The terminal and UI libraries have matching xUnit v3 suites under `tests/`;
  the unprivileged consumer suite verifies public extension contracts. The
  showcase is compiled as a production example and has no dedicated test
  project.

The
[project structure](docs/architecture/project-structure.md#project-structure-contract)
defines the dependency rules.

## Requirements

- .NET SDK 10.0.203 or a compatible latest patch in that feature band.
- Node.js 22 or later for documentation tooling.
- Make for the repository command interface.

## Build and verify

```bash
make format
make lint
make build
make test
```

`make test` requires at least the configured number of discovered tests, so an
empty test run cannot masquerade as success. Run the current showcase shell with
`make run`.

## Specifications

Start at the [documentation contract](docs/index.md#documentation-contract).
Terminal wire behavior lives in the
[protocol index](docs/protocols/index.md#protocol-families), shared UI behavior
in the [concept map](docs/concepts/index.md#concept-map), public controls in the
[control catalog](docs/controls/index.md#control-catalog), and correctness proof
in the [test map](docs/testing/index.md#test-map).

## Contributing

Read [AGENTS.md](AGENTS.md#orientation-workflow) and the relevant domain skill
under `.codex/skills/` before changing behavior. Documentation, implementation,
tests, XML comments, and showcase examples must stay synchronized.
