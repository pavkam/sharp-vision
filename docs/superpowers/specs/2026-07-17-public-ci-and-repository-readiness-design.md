# Public CI and Repository Readiness Design

## Goal

Prepare SharpVision for public contributions with reproducible cross-platform
continuous integration, enforceable coverage, supply-chain checks, dry-run
release artifacts, and complete community health files. The automation adapts
the useful workflow structure from Sharpie without importing its native-curses
jobs or older .NET assumptions.

## Source and adaptation boundary

The workflow exemplar is [`pavkam/sharpie`](https://github.com/pavkam/sharpie)
at commit `cd30d6754f46af3a4003e943182bf6b563de373d`, inspected on 2026-07-17.
Its reusable setup and build actions, three-platform pull-request validation,
coverage reports, job summaries, dependency updates, and release validation
inform this design.

SharpVision does not copy Sharpie's NCurses, PDCurses, or PDCursesMod refresh
workflows because it owns no equivalent native dependency. It also does not copy
the Sharpie README coverage image, which is a workflow-status badge rather than
a measured coverage percentage.

Microsoft Testing Platform coverage follows the .NET 10
[code coverage extension contract](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-extensions-code-coverage).
Codecov uploads use its
[GitHub OIDC mode](https://github.com/codecov/codecov-action#using-oidc) rather
than a long-lived repository secret.

## Baseline

The `codex/ci-prep` branch starts from committed SharpVision revision `fe1130b`.
That revision builds in Release with zero warnings and zero errors. Existing
project rules remain authoritative: .NET 10, C# 14, warnings as errors, latest
recommended analyzers, deterministic output, exact file/type rules, normative
documentation validation, minimum test discovery, and the isolated
package-consumer proof.

## Workflow architecture

### Local tools and commands

A repository-local .NET tool manifest pins ReportGenerator. Test projects
reference a centrally versioned `Microsoft.Testing.Extensions.CodeCoverage`
package compatible with their Microsoft Testing Platform version. No global tool
installation or developer-machine state is required.

The Makefile remains the public command surface and gains focused targets for:

- restoring repository-local tools;
- producing TRX and Cobertura test output;
- generating HTML, Cobertura, Markdown, and SVG coverage reports;
- validating coverage against the committed baseline;
- packing all publishable projects without upload; and
- assembling release-readiness artifacts.

Existing `format`, `lint`, `build`, `test`, and package-consumer targets retain
their current meaning. CI calls the same targets contributors run locally.

### Reusable actions

`.github/actions/setup/action.yml` prepares .NET 10 and Node.js 24, restores
NuGet, npm, and repository-local .NET tools, and caches only dependency data.

`.github/actions/build-and-test/action.yml` runs formatting and repository
linters, builds Release, executes all tests with hang diagnostics and TRX
output, verifies the isolated package consumer, collects Cobertura coverage,
generates the human-readable report, validates the baseline, and uploads
platform-specific reports. Composite action inputs select the platform,
configuration, and whether the caller owns the canonical Codecov upload.

### Pull-request and main CI

`.github/workflows/ci.yml` runs for pull requests targeting `main`, pushes to
`main`, and manual dispatch. Concurrency cancels superseded runs for the same
workflow and ref.

The matrix contains Ubuntu, macOS, and Windows jobs. Each job builds and tests
the real solution. Linux owns the canonical coverage comparison and Codecov
upload; the other platforms retain their own TRX and coverage artifacts for
diagnosis. A final job always summarizes every platform without concealing a
failed prerequisite.

Fork pull requests receive read-only permissions and still generate and validate
local coverage. OIDC upload runs only when GitHub can issue a trusted identity
for this repository. Absence of privileged upload on an untrusted fork never
weakens the local coverage gate.

### Release readiness without publication

`.github/workflows/release-readiness.yml` runs manually and on pushes to `main`.
It first runs the reusable quality action, then creates Release NuGet packages,
SPDX SBOM output, SHA-256 checksums, and a manifest describing every artifact.
The workflow uploads these files with retention and produces a job summary.

There is no `dotnet nuget push`, registry credential, GitHub Release creation,
tag creation, or other publication side effect. Enabling NuGet publishing is a
future, separately reviewed design change.

### Security workflows

Dedicated workflows provide:

- CodeQL analysis on pull requests, `main`, and a weekly schedule;
- dependency review on pull requests;
- OpenSSF Scorecard analysis on `main` and a weekly schedule; and
- validation of GitHub Actions syntax and immutable third-party action pins.

Every workflow declares `permissions`. The default is `contents: read`; only the
job that needs checks, security events, pull-request annotations, or OIDC
receives the corresponding permission. Third-party actions use complete commit
SHAs with a nearby version comment. Workflow dependencies are updated through
Dependabot rather than floating tags.

## Coverage contract

Coverage includes the three production projects and excludes test assemblies,
generated files, and compiler-generated members where the collector can identify
them. The canonical Linux run produces Cobertura input and a merged
ReportGenerator report containing line and branch coverage.

The first fully passing canonical run on the approved baseline records its
measured line and branch percentages in a committed machine-readable baseline.
Validation fails when either metric drops below that recorded value. Updating
the baseline requires an intentional file change and a pull-request rationale;
ordinary CI never rewrites it. This prevents regression without inventing an
arbitrary percentage that the current repository has not demonstrated.

Codecov receives the canonical Cobertura report through OIDC and provides the
public measured-coverage badge. Codecov availability does not replace local
baseline enforcement. Coverage reports remain downloadable GitHub artifacts on
every run, including when the external upload fails.

## Test and diagnostic contract

CI preserves the repository's no-retry rule. A flaky test is a failure to
diagnose, not an invitation to rerun until green.

Each platform retains:

- TRX test results;
- Cobertura and HTML coverage reports;
- blame-hang, crash, and sequence diagnostics produced by the test runner; and
- package-consumer or packing logs when those stages fail.

Artifact upload and the final summary use `always()` while preserving the
original failing job status. Test publication may annotate a pull request but
cannot convert a failed test command into success.

## Dependency maintenance

`.github/dependabot.yml` checks NuGet, npm, and GitHub Actions weekly. Updates
are grouped by ecosystem where compatible, carry dependency labels, and limit
open pull requests to prevent notification floods. Security updates remain
independent so a broad version group cannot delay them.

## Public project files

The repository gains:

- `CONTRIBUTING.md` with environment setup, documentation-first expectations,
  focused-test commands, full gates, commit guidance, and pull-request proof;
- `SECURITY.md` with supported-version policy and private reporting path;
- `SUPPORT.md` distinguishing usage questions, defects, and vulnerabilities;
- `CODE_OF_CONDUCT.md` using the Contributor Covenant;
- `.github/CODEOWNERS` assigning repository ownership to `@pavkam`;
- issue forms for defects and feature proposals plus a configuration that
  directs security reports away from public issues; and
- a pull-request template requiring linked intent, behavioral/docs alignment,
  test evidence, coverage impact, and public API review.

The README header gains badges for CI, Codecov measured coverage, CodeQL,
OpenSSF Scorecard, license, open issues, and .NET 10. It intentionally has no
Pages, documentation-deployment, NuGet-version, or download badge.

## Repository rules

After the workflows land on GitHub and their check names exist, `main` should
receive a repository ruleset with:

- pull requests required;
- successful required CI, coverage, and CodeQL checks;
- branches required to be current before merge;
- resolved review conversations;
- linear history;
- force pushes and branch deletion blocked; and
- an administrator bypass to prevent a solo-maintainer deadlock.

Required approval count starts at zero because GitHub does not allow a pull
request author to approve their own change. `CODEOWNERS` still requests review.
The count becomes one when another maintainer can supply an independent
approval.

Secret scanning, push protection, Dependabot alerts, and private vulnerability
reporting should be enabled. Applying these live GitHub settings happens only
after the workflows are present and after a separate explicit authorization;
repository files document the intended state but do not mutate remote settings.

## Non-goals

This phase does not:

- publish documentation or configure GitHub Pages;
- publish NuGet packages or create releases;
- copy Sharpie's native-library refresh automation;
- lower existing analyzers, formatting rules, or test-discovery requirements;
- hide platform failures behind retries or allowed-failure matrix entries; or
- apply remote branch rules before their required checks exist.

## Acceptance criteria

The implementation is complete when:

1. local quality, build, test, coverage, report, pack, and package-consumer
   commands are reproducible from a clean checkout;
2. all workflow and community files pass local formatting, Markdown, link, and
   GitHub Actions validation;
3. Ubuntu, macOS, and Windows jobs use the same committed quality contract and
   retain platform-specific evidence;
4. coverage is collected through Microsoft Testing Platform, enforced against
   the committed baseline, uploaded to Codecov through OIDC, and represented by
   a measured badge;
5. release-readiness artifacts contain packages, SBOM data, checksums, and a
   manifest without any publication command or credential;
6. CodeQL, dependency review, Scorecard, and Dependabot are configured with
   least privilege and immutable action references;
7. the README and public project files accurately describe the repository; and
8. `make format`, `make lint`, `make build`, and `make test` complete with zero
   warnings, zero errors, valid documentation links, and the configured minimum
   discovered tests.
