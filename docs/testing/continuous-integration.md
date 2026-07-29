# Continuous integration contract

## Continuous integration contract

The pull-request workflow verifies changes proposed to `main`. The alpha
publication workflow repeats the same build-and-test action on pushes to `main`
before it packs or publishes anything. These workflows reproduce the repository
quality surface; they do not replace focused local proof while developing.

The shared composite action runs `make lint`, the Release build, tests with
coverage, coverage-report generation, and artifact publication in sequence.
`make lint` covers C# formatting and analyzers, C# source-structure checks,
external resource checks, GitHub Actions schema and immutable-pin validation,
Markdown formatting and linting, local-link validation, and
documentation-tooling tests. Microsoft Testing Platform tests enforce a
discovery minimum and produce xUnit TRX plus Cobertura output. The action
publishes the test-result check and uploads both the raw TRX files and an
HTML/Cobertura/badge coverage report as workflow artifacts.
`make test-binding-coverage` additionally isolates binding production files and
requires 95% line plus 90% branch coverage. It fails when binding files are
absent from the report.

`make test-ci` additionally requires at least 90 percent line coverage across
instrumented UI classes under `src/SharpVision/Controls/`, `Dialogs/`, `Menus/`,
`Navigation/`, `Popups/`, and `Windows/`. The scoped floor supplements the
behavioral catalogs; it does not allow line coverage to replace mounted pointer,
keyboard, focus, hover, pressed-state, box-model, frame, resize, or tiny-bound
assertions. The coverage-instrumented UI run uses static managed instrumentation
and disables collection parallelization so coverage remains complete and
deterministic across runners; the ordinary test target retains the suite's
normal parallel execution. Neither job waits for the other; a workflow succeeds
only when both complete successfully. The workflow badge in the
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

## Alpha package publication

The `sharpvision-publish.yml` workflow runs the same build-and-test action
before reading the shared `OverallVersion`. Publication accepts only versions in
`major.minor.patch-alpha.number` form and requires the terminal and UI projects
to resolve the same value.

The workflow packs `SharpVision.Terminal` and `SharpVision` together, verifies
that each produced exactly one NuGet package and one symbol package, then
publishes the terminal dependency before the UI package. Existing versions are
detected independently, so a partially completed publication can safely resume
without skipping its missing package. Every run repacks both projects and
retries all package and symbol pushes with duplicate skipping, so a failed
symbol upload is repaired even when its main package already exists.

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

| Gate            | Pass condition                                                                            |
| --------------- | ----------------------------------------------------------------------------------------- |
| Format and lint | No C#, Markdown, link, structure, workflow, or external-resource violations.              |
| Build           | Zero warnings/errors across production, examples, showcase, tests, and XML documentation. |
| Test            | Minimum discovery is met and every discovered test passes without retries.                |
| Package         | Both packages and symbols use one approved version and validated metadata.                |
