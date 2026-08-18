# Continuous integration

## Overview

The pull-request workflow verifies changes proposed to `main` by running the
shared build-and-test action on Linux, Windows, and macOS as three independent
jobs, alongside a fourth, parallel lint job; a summary job then fails the
workflow unless all three platforms and the lint gate pass. The package
publication workflow runs the same build-and-test action once on Linux for
pushes to `main` — cross-platform verification happens at the pull-request gate,
not again at publication — in parallel with the same lint job, and a final push
job that needs both before it uploads anything to NuGet. These workflows
reproduce the repository quality surface; they do not replace focused local
testing while developing.

Linting is a job, not a step. It inspects sources, Markdown, and links, none of
which vary by operating system, so running it inside the composite action meant
paying for it once per platform and paying for it serially ahead of the tests it
cannot affect. As its own job it costs about as much wall-clock time as the test
phase and now overlaps it entirely. `make lint` runs
`dotnet format --verify-no-changes`, `prettier --check`, `markdownlint-cli2`,
the generated-data freshness checks (Unicode tables, FIGlet font manifest), and
the documentation validators (local links, banned GitHub issue references,
test-class naming). A lint failure still cannot suppress the build, test,
coverage, or compatibility-snapshot results, because those run in different jobs
that neither wait for it nor observe it; and publication still cannot happen
while lint is failing, because the push job needs both gates.

The shared composite action runs the Release build, the tests with coverage,
coverage-report generation, and artifact publication, in that order. Tests run
on the Microsoft Testing Platform, enforce a discovery minimum, and produce
xUnit TRX plus Cobertura output. The action publishes the test-result check and
uploads both the raw TRX files and an HTML/Cobertura/badge coverage report as
workflow artifacts.

The target the action actually runs is `make test-ci`. It also runs
`npm run test:docs` — the Node unit suite covering the `scripts/` gate layer
itself, including the control-coverage floor validator — so that suite is not
exclusive to the local-only `make test` target.

`make test-ci` additionally requires at least 85 percent line coverage across
the instrumented UI classes under `src/SharpVision/Controls/`, `Dialogs/`,
`Menus/`, `Navigation/`, `Popups/`, and `Windows/`. This scoped floor
supplements the behavioral catalogs; it does not let line coverage stand in for
mounted pointer, keyboard, focus, hover, pressed-state, box-model, frame,
resize, or tiny-bound assertions. The coverage-instrumented UI run disables
collection parallelization so coverage stays complete and deterministic across
runners; the ordinary test target keeps the suite's normal parallel execution.
The terminal and UI coverage commands run one after the other, and a failure in
either stops the target. The workflow badge in the
[README](../../README.md#sharpvision) reflects that automation.

Both instrumented suites share one coverage settings file, selected by platform.
Linux uses
[`tests/coverage.dynamic.config`](../../tests/coverage.dynamic.config) and every
other platform uses
[`tests/coverage.static.config`](../../tests/coverage.static.config). Static
instrumentation rewrites each assembly and prefixes every basic block with a
store into a memory-mapped probe buffer in the temporary directory. On Linux the
coverage host rewrites that buffer in place while the test host still has it
mapped, and a probe that stores into the resulting unbacked page terminates the
process with an `AccessViolationException` attributed to whichever managed
method was executing — a roaming, unreproducible failure that has no
relationship to the blamed code. Dynamic instrumentation collects through the
profiler and creates no such buffer. It is confined to Linux because the
profiler ships only for a limited set of runtime identifiers, and on the ones it
does not cover, such as macOS arm64, it silently reports no coverage at all.

Dynamic instrumentation hooks every managed method, so an absolute wall-clock
budget measured under it gates the profiler instead of the product. Those
budgets skip while a profiler is attached, and `make test-ci` re-runs the whole
`SharpVision.Tests.Performance` namespace without instrumentation afterwards so
they stay enforced. Budgets expressed as a ratio between two measurements taken
in the same process are unaffected, because both sides carry the same overhead,
and keep running inside the instrumented pass.

## Local command mapping

Use the Makefile as the local command surface:

| Intent                                            | Command                                                                               |
| ------------------------------------------------- | ------------------------------------------------------------------------------------- |
| Restore .NET and Node dependencies                | `make restore`                                                                        |
| Apply formatting                                  | `make format`                                                                         |
| Verify formatting, analyzers, Markdown, and links | `make lint`                                                                           |
| Build in Release configuration                    | `make build`                                                                          |
| Run the full test suite                           | `make test`                                                                           |
| Run a focused test while iterating                | `dotnet test --project tests/SharpVision.Tests --filter-class "*Tests" --timeout 60s` |
| Verify the current public API baselines           | `dotnet test --project tests/SharpVision.Compatibility.Tests --timeout 60s`           |

`make test` requires a configured minimum number of discovered tests, so a run
that discovers zero — or unexpectedly few — tests fails instead of passing
vacuously.

The public API project participates in the solution-wide build and test gates.
Its [approval workflow](correctness-model.md#public-api-compatibility) requires
review of the affected library's own surface before a compatibility change can
go green; it does not require a version change on its own, and a change to one
library's surface never requires touching the other two.

`make restore` first packs the current `SharpVision.Terminal` and `SharpVision`
projects into an ignored local bootstrap feed. It then restores the full
solution into an isolated repository cache using that feed and nuget.org. This
lets `SharpVision.FigletFonts` exercise its real `SharpVision` PackageReference
before the current version is published and prevents a stale global NuGet cache
from masking package changes. The floor is derived from `SharpVisionVersion`
rather than written as a literal, because NuGet resolves a floor range to the
lowest satisfying version across every source: a literal that fell behind would
be satisfied by an older core on nuget.org, and the bootstrap feed would go
unused.

## Package publication

`SharpVision.Terminal`, `SharpVision`, and `SharpVision.FigletFonts` each own an
independent version (`SharpVisionTerminalVersion`, `SharpVisionVersion`, and
`SharpVisionFigletFontsVersion` in `Directory.Build.props`) and publish on their
own schedule; none of the three needs to move in lockstep with the others; only
`SharpVision.FigletFonts`'s dependency on `SharpVision` ties two of their
numbers together (see below).

The `sharpvision-publish.yml` workflow runs the same build-and-test action and
then reads each project's own `Version` independently. Publication accepts a
three-part semantic version with an optional prerelease suffix for each package;
the three packages are never required to agree.

The workflow independently checks whether `SharpVision.Terminal`, `SharpVision`,
and `SharpVision.FigletFonts` already exist at their own respective version. It
always packs and validates exactly three main packages and three symbol
packages, then publishes each missing package with its symbols in dependency
order: Terminal, UI, then the optional font catalog. An existing UI package
cannot suppress a missing Terminal or FigletFonts package. A main package that
already exists is not rebuilt or republished under the immutable version.

`SharpVision.FigletFonts` emits a minimum dependency on the `SharpVision` core's
own version (`SharpVisionVersion`), not on its own version - the two packages
publish independently and are not expected to share a version number. NuGet
serializes that open-ended range as that bare minimum version in the `.nuspec`.
The packed-consumer test asserts the packed dependency equals the packed core
package's own version, so a floor that drifted away from the core it ships
beside fails there rather than shipping.

That test restores the two packed artifacts from the local feed; nuget.org stays
in its generated `NuGet.config` because the packed nuspecs depend on
`JetBrains.Annotations`, which no local feed provides. It verifies that core has
no FIGfont resources, verifies the optional assembly has 19 individual font
resources and no ZIP, and renders `Classy` through the transitive dependency
graph.

`Directory.Build.targets` refuses to generate a NuGet manifest for a packable
project when any required public metadata is empty: identity, version, title,
authors, description, tags, license, project and repository links, icon, README,
release notes, copyright, or the license-acceptance policy. The deprecated NuGet
fields `owners` and `summary` are intentionally not emitted.

## Failure handling

Do not retry a flaky result until it passes. Preserve the failing command and
its diagnostics, reduce the failure to a focused test or deterministic fixture,
and commit that regression proof together with the fix. The
[testing specifications](index.md#test-map) define the proof ladder required for
terminal, Unicode, rendering, and control behavior.

## Required evidence

| Gate            | Pass condition                                                                                                            |
| --------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Format and lint | No C# formatting/analyzer, Markdown formatting/lint, or local-link violations; runs as its own job beside build and test. |
| Build           | Zero warnings/errors across production, examples, showcase, tests, and XML documentation.                                 |
| Test            | Minimum discovery is met and every discovered test passes without retries.                                                |
| Package         | All three packages and symbols use the approved version and validated metadata; dependencies publish before dependents.   |
