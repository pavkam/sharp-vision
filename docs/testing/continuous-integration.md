# Continuous integration

## Overview

The pull-request workflow verifies changes proposed to `main` by running the
shared build-and-test action on Linux, Windows, and macOS as three independent
jobs; a summary job then fails the workflow unless all three platforms pass. The
package publication workflow runs the same build-and-test action once on Linux
for pushes to `main`, before it packs or publishes anything — cross-platform
verification happens at the pull-request gate, not again at publication. These
workflows reproduce the repository quality surface; they do not replace focused
local testing while developing.

The shared composite action runs `make lint`, the Release build, the tests with
coverage, coverage-report generation, and artifact publication, in that order.
`make lint` is exactly four commands: `dotnet format --verify-no-changes`,
`prettier --check`, `markdownlint-cli2`, and the local-link validator. A lint
failure does not skip the later gates: the lint step runs with
`continue-on-error`, every later step still runs, and the action fails the job
at the end if lint failed. A formatting violation therefore cannot suppress the
build, test, coverage, or compatibility-snapshot results. Tests run on the
Microsoft Testing Platform, enforce a discovery minimum, and produce xUnit TRX
plus Cobertura output. The action publishes the test-result check and uploads
both the raw TRX files and an HTML/Cobertura/badge coverage report as workflow
artifacts.

The target the action actually runs is `make test-ci`. It also runs
`npm run test:docs` — the Node unit suite covering the `scripts/` gate layer
itself, including the control-coverage floor validator — so that suite is not
exclusive to the local-only `make test` target.

`make test-ci` additionally requires at least 85 percent line coverage across
the instrumented UI classes under `src/SharpVision/Controls/`, `Dialogs/`,
`Menus/`, `Navigation/`, `Popups/`, and `Windows/`. This scoped floor
supplements the behavioral catalogs; it does not let line coverage stand in for
mounted pointer, keyboard, focus, hover, pressed-state, box-model, frame,
resize, or tiny-bound assertions. The coverage-instrumented UI run uses static
managed instrumentation and disables collection parallelization so coverage
stays complete and deterministic across runners; the ordinary test target keeps
the suite's normal parallel execution. The terminal and UI coverage commands run
one after the other, and a failure in either stops the target. The workflow
badge in the [README](../../README.md#sharpvision) reflects that automation.

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
Its [versioned approval workflow](correctness-model.md#public-api-compatibility)
requires an intentional package-version change, plus review of both library
surfaces, before a compatibility change can go green.

## Package publication

The `sharpvision-publish.yml` workflow runs the same build-and-test action and
then reads `OverallVersion` from `SharpVision`. Publication accepts a three-part
semantic version with an optional prerelease suffix.

The workflow checks whether that `SharpVision` package version already exists.
If it does not, the workflow packs exactly one main package and one symbol
package and pushes both with duplicate skipping. If the main package already
exists, the workflow skips packing and both pushes — which means it cannot
repair a missing symbol package on its own.

> [!IMPORTANT]
>
> `SharpVision.Terminal` is currently non-packable, while `SharpVision` declares
> an exact package dependency on it. NuGet contains `SharpVision`
> `0.5.0-alpha.1` but no matching `SharpVision.Terminal` package, so package
> installation cannot resolve the dependency. Repository builds and project
> references remain usable. Publication must ship the terminal dependency before
> the UI package can be considered installable.

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

| Gate            | Pass condition                                                                                                           |
| --------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Format and lint | No C# formatting/analyzer, Markdown formatting/lint, or local-link violations; failure here does not skip build or test. |
| Build           | Zero warnings/errors across production, examples, showcase, tests, and XML documentation.                                |
| Test            | Minimum discovery is met and every discovered test passes without retries.                                               |
| Package         | The UI package and symbols use the approved version and validated metadata; its terminal dependency is published first.  |
