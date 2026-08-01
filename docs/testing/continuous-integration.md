# Continuous integration

## Overview

The pull-request workflow verifies changes proposed to `main` by running the
shared build-and-test action on Linux, Windows, and macOS in three independent
jobs, followed by a summary job that fails the workflow unless all three
platforms pass. The package publication workflow repeats the same build-and-test
action once on Linux for pushes to `main`, before it packs or publishes
anything; cross-platform verification happens at the pull-request gate, not
again at publication. These workflows reproduce the repository quality surface;
they do not replace focused local proof while developing.

The shared composite action runs `make lint`, the Release build, tests with
coverage, coverage-report generation, and artifact publication in sequence.
`make lint` is exactly four commands: `dotnet format --verify-no-changes`,
`prettier --check`, `markdownlint-cli2`, and the local-link validator. A lint
failure does not skip the later gates: the lint step runs with
`continue-on-error`, every later step in the action still runs, and the action
fails the job at the end if lint failed, so a formatting violation cannot
suppress build, test, coverage, or the compatibility snapshot. Microsoft Testing
Platform tests enforce a discovery minimum and produce xUnit TRX plus Cobertura
output. The action publishes the test-result check and uploads both the raw TRX
files and an HTML/Cobertura/badge coverage report as workflow artifacts.

`make test-ci` — the target the action actually runs — also runs
`npm run test:docs`, the Node unit suite covering the `scripts/` gate layer
itself (including the control-coverage floor validator); this is not exclusive
to the local-only `make test` target.

`make test-ci` additionally requires at least 85 percent line coverage across
instrumented UI classes under `src/SharpVision/Controls/`, `Dialogs/`, `Menus/`,
`Navigation/`, `Popups/`, and `Windows/`. The scoped floor supplements the
behavioral catalogs; it does not allow line coverage to replace mounted pointer,
keyboard, focus, hover, pressed-state, box-model, frame, resize, or tiny-bound
assertions. The coverage-instrumented UI run uses static managed instrumentation
and disables collection parallelization so coverage remains complete and
deterministic across runners; the ordinary test target retains the suite's
normal parallel execution. The terminal and UI coverage commands run
sequentially, and either failure stops the target. The workflow badge in the
[README](../../README.md#sharpvision) reflects that automation.

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

`make test` requires a configured minimum number of discovered tests. A zero or
otherwise unexpectedly small test run is therefore a failure, not evidence of
success.

The public API project participates in the solution-wide build and test gates.
Its [versioned approval workflow](correctness-model.md#public-api-compatibility)
requires an intentional package-version change and review of both library
surfaces before a compatibility change becomes green.

## Package publication

The `sharpvision-publish.yml` workflow runs the same build-and-test action
before reading `OverallVersion` from `SharpVision`. Publication accepts a
three-part semantic version with an optional prerelease suffix.

The workflow checks whether the `SharpVision` package version already exists.
When it does not, it packs exactly one main package and one symbol package and
pushes both with duplicate skipping. When the main package already exists, the
workflow skips packing and both pushes; it therefore cannot repair a missing
symbol package independently.

> [!IMPORTANT] `SharpVision.Terminal` is currently non-packable, while
> `SharpVision` declares an exact package dependency on it. NuGet contains
> `SharpVision` `0.5.0-alpha.1` but no matching `SharpVision.Terminal` package,
> so package installation cannot resolve the dependency. Repository builds and
> project references remain usable. Publication must ship the terminal
> dependency before the UI package can be considered installable.

`Directory.Build.targets` rejects a packable project before NuGet manifest
generation when any required public metadata is empty: identity, version, title,
authors, description, tags, license, project and repository links, icon, README,
release notes, copyright, or license-acceptance policy. Deprecated NuGet fields
such as `owners` and `summary` are intentionally not emitted.

## Failure handling

Do not retry a flaky result into legitimacy. Preserve the failing command and
diagnostics, reduce the failure with a focused test or deterministic fixture,
and commit the regression proof with the correction. The
[testing specifications](index.md#test-map) define the required proof ladder for
terminal, Unicode, rendering, and control behavior.

## Required evidence

| Gate            | Pass condition                                                                                                           |
| --------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Format and lint | No C# formatting/analyzer, Markdown formatting/lint, or local-link violations; failure here does not skip build or test. |
| Build           | Zero warnings/errors across production, examples, showcase, tests, and XML documentation.                                |
| Test            | Minimum discovery is met and every discovered test passes without retries.                                               |
| Package         | The UI package and symbols use the approved version and validated metadata; its terminal dependency is published first.  |
