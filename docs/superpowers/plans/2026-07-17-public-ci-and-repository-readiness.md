# Public CI and Repository Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver reproducible three-platform CI, measured coverage with
regression enforcement, dry-run release artifacts, security automation, and
public contribution files without publishing NuGet packages or documentation.

**Architecture:** Repository-local tools and Node validators extend the Makefile
as the single local and CI command surface. Reusable composite actions feed
dedicated CI, release-readiness, CodeQL, dependency-review, and Scorecard
workflows; public repository files and README badges expose only automation that
actually exists.

**Tech Stack:** .NET 10.0.203, C# 14, Microsoft Testing Platform 1.9.1,
`Microsoft.Testing.Extensions.CodeCoverage` 18.0.6, ReportGenerator 5.5.10,
Microsoft SBOM Tool 4.1.5, Node.js 24, GitHub Actions, Codecov OIDC, CodeQL,
OpenSSF Scorecard, Dependabot.

---

## File map

- `.config/dotnet-tools.json`, `.config/coverage-baseline.json`, and
  `eng/CodeCoverage.runsettings`: pinned tools and coverage policy.
- `scripts/validate-coverage.mjs`, `scripts/validate-action-pins.mjs`,
  `scripts/create-release-manifest.mjs`, and matching tests: local quality
  oracles.
- `Directory.Build.props`, `Directory.Packages.props`, `package.json`,
  `package-lock.json`, and `Makefile`: shared packages, metadata, and commands.
- `.github/actions/*` and `.github/workflows/*`: reusable quality, CI,
  release-readiness, and security automation.
- `.github/dependabot.yml`, `.github/CODEOWNERS`, templates, and root community
  files: public maintenance policy.
- `docs/testing/continuous-integration.md`, `.github/REPOSITORY_SETTINGS.md`,
  and `README.md`: public quality contract and backed badges.

### Required baseline behavior

Revision `dda008f` fixes `Runtime.Session` so an early negotiation timer
completion is rearmed against the same finite deadline after the first query is
written. Its deterministic regression in `SessionTests`, supporting runtime
fakes, production change, and capability documentation are required baseline
behavior and must remain intact throughout this plan.

### Task 2: Add local GitHub Actions validation

**Files:**

- Create: `scripts/validate-action-pins.mjs`
- Create: `scripts/validate-action-pins.test.mjs`
- Modify: `package.json`
- Modify: `package-lock.json`

- [ ] **Step 1: Write the failing tests**

Create tests proving local actions and complete 40-character commit SHAs pass,
while tags, branches, and shortened SHAs fail with file and line details:

```javascript
import assert from "node:assert/strict";
import test from "node:test";

import { validateActionPins } from "./validate-action-pins.mjs";

test("validateActionPins_WhenReferencesAreImmutable_ReturnsNoErrors", () => {
  const yaml =
    "steps:\n  - uses: ./github/actions/setup\n  - uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0\n";
  assert.deepEqual(validateActionPins(yaml, "ci.yml"), []);
});

test("validateActionPins_WhenReferencesFloat_ReportsEachReference", () => {
  const yaml =
    "steps:\n  - uses: actions/checkout@v7\n  - uses: owner/action@1234abc\n";
  const errors = validateActionPins(yaml, "ci.yml");
  assert.equal(errors.length, 2);
  assert.match(errors[0], /actions\/checkout@v7/);
  assert.match(errors[1], /owner\/action@1234abc/);
});
```

Run `node --test scripts/validate-action-pins.test.mjs`; expect a missing-module
failure.

- [ ] **Step 2: Implement the pin validator**

Export `validateActionPins(text, file)`. Match every YAML `uses:` value, allow
`./` local actions and `docker://IMAGE@sha256:DIGEST`, and require
`owner/repository@revision` revisions to match `/^[0-9a-f]{40}$/`. The CLI
recursively checks `.github/workflows/*.yml` and `.github/actions/*/action.yml`,
prints `file:line` errors, and exits nonzero when any exist.

- [ ] **Step 3: Install schema linters and add npm commands**

```bash
npm install --save-dev --save-exact github-actionlint@1.7.12 @action-validator/cli@0.6.0
```

Add scripts:

```json
"lint:actions": "github-actionlint",
"lint:action-pins": "node scripts/validate-action-pins.mjs"
```

Do not add pin validation to `make lint` while the existing workflow still
floats action tags. Task 6 wires both scripts into the gate immediately after
replacing that workflow.

- [ ] **Step 4: Verify and commit**

```bash
node --test scripts/validate-action-pins.test.mjs
npm run lint:actions
git add scripts/validate-action-pins.mjs scripts/validate-action-pins.test.mjs package.json package-lock.json
git commit -m "chore(ci): validate workflow syntax and action pins"
```

### Task 3: Add MTP coverage and regression enforcement

**Files:**

- Create: `.config/dotnet-tools.json`
- Create: `.config/coverage-baseline.json`
- Create: `eng/CodeCoverage.runsettings`
- Create: `scripts/validate-coverage.mjs`
- Create: `scripts/validate-coverage.test.mjs`
- Modify: `Directory.Build.targets`
- Modify: `Directory.Packages.props`
- Modify: `Makefile`

- [ ] **Step 1: Write failing coverage tests**

Use temporary files to prove: record writes exact `lineRate` and `branchRate`;
equal or improved values pass; either regression fails; missing, nonnumeric, or
out-of-range Cobertura rates reject. Run
`node --test scripts/validate-coverage.test.mjs` and observe the missing-module
failure.

- [ ] **Step 2: Implement the coverage CLI**

Export `readCoverage`, `recordCoverageBaseline`, and `validateCoverage`. Support
exactly:

```text
node scripts/validate-coverage.mjs record REPORT BASELINE
node scripts/validate-coverage.mjs validate REPORT BASELINE
```

Parse the root Cobertura `line-rate` and `branch-rate` as finite values in
`[0,1]`. `record` writes stable two-space JSON with a trailing newline.
`validate` compares without rounding, prints actual and required percentages,
exits nonzero on regression, and never edits the baseline.

- [ ] **Step 3: Pin compatible packages and tools**

Add `Microsoft.Testing.Extensions.CodeCoverage` version `18.0.6` to
`Directory.Packages.props` and a `PrivateAssets="all"` test-project reference
under `Condition="'$(IsTestProject)' == 'true'"` in `Directory.Build.targets`.
The target file is imported after each project declares `IsTestProject`;
`Directory.Build.props` is imported too early for that condition.

Create `.config/dotnet-tools.json` containing:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-reportgenerator-globaltool": {
      "version": "5.5.10",
      "commands": ["reportgenerator"],
      "rollForward": false
    },
    "microsoft.sbom.dotnettool": {
      "version": "4.1.5",
      "commands": ["sbom-tool"],
      "rollForward": false
    }
  }
}
```

- [ ] **Step 4: Define the coverage boundary**

Create `eng/CodeCoverage.runsettings` with `IncludeTestAssembly` false; include
only `SharpVision.dll` and `SharpVision.Terminal.dll`; exclude test and showcase
assemblies, generated-code attributes, `.g.cs`, `.generated.cs`, and
`AssemblyInfo.cs`.

- [ ] **Step 5: Add Make targets**

Introduce `CONFIGURATION ?= Release`, use it from build, test, coverage, and
pack targets, and add `tools`, `test-ci`, `coverage-collect`, `coverage`,
`coverage-report`, `coverage-check`, and `coverage-record`. Both CI test targets
enable long-running-test detection plus warning-level diagnostics under
`TestResults/Diagnostics`. `coverage-collect` clears prior test and coverage
output, runs the following test command, and then invokes `coverage-report`:

```bash
dotnet test --solution SharpVision.slnx --configuration Release --no-build \
  --minimum-expected-tests 3 --timeout 900s --report-xunit-trx \
  --long-running 30 --diagnostic --diagnostic-verbosity Warning \
  --diagnostic-output-directory TestResults/Diagnostics \
  --coverage --coverage-output-format cobertura \
  --coverage-settings eng/CodeCoverage.runsettings
```

ReportGenerator consumes `**/TestResults/**/*.cobertura.xml`, writes
`artifacts/coverage`, and emits `HtmlInline;Cobertura;MarkdownSummary;Badges`.
`coverage` depends on `coverage-collect` and `coverage-check`. `coverage-check`
validates `artifacts/coverage/Cobertura.xml` against
`.config/coverage-baseline.json`; only `coverage-record` may rewrite the
baseline. Extend `make clean` to remove `artifacts/` as well as `TestResults/`.

- [ ] **Step 6: Record and verify the first green baseline**

```bash
dotnet tool restore
make coverage-collect
make coverage-record
make coverage-check
node --test scripts/validate-coverage.test.mjs
make coverage
```

Expected: nonzero line and branch rates, a committed exact baseline, and a
passing regression check.

- [ ] **Step 7: Commit**

```bash
git add .config eng scripts/validate-coverage.mjs scripts/validate-coverage.test.mjs Directory.Build.targets Directory.Packages.props Makefile
git commit -m "test(ci): enforce measured coverage baseline"
```

### Task 4: Build deterministic release-readiness artifacts

**Files:**

- Create: `scripts/create-release-manifest.mjs`
- Create: `scripts/create-release-manifest.test.mjs`
- Modify: `Directory.Build.props`
- Modify: `Makefile`

- [ ] **Step 1: Write the failing manifest tests**

Test that generation recursively sorts relative POSIX paths, omits its own
`manifest.json`, records exact byte lengths and lowercase SHA-256 values, and
produces byte-identical output on repeated runs. Run the test and observe the
missing-module failure.

- [ ] **Step 2: Implement `create-release-manifest.mjs`**

Export `createReleaseManifest(root)` and provide a one-argument CLI. Reject a
missing or non-directory root. Write this stable schema with a trailing newline:

```json
{
  "schemaVersion": 1,
  "files": [
    {
      "path": "packages/SharpVision.0.0.0-ci.local.nupkg",
      "bytes": 123,
      "sha256": "64-lowercase-hex-characters"
    }
  ]
}
```

- [ ] **Step 3: Add public package metadata**

Add to `Directory.Build.props`:

```xml
<PackageProjectUrl>https://github.com/pavkam/sharp-vision</PackageProjectUrl>
<RepositoryUrl>https://github.com/pavkam/sharp-vision.git</RepositoryUrl>
<RepositoryType>git</RepositoryType>
<PackageTags>terminal tui console unicode ansi dotnet</PackageTags>
<PublishRepositoryUrl>true</PublishRepositoryUrl>
<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
<EmbedUntrackedSources>true</EmbedUntrackedSources>
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
```

- [ ] **Step 4: Add `pack` and `release-artifacts` targets**

Use `VERSION ?= 0.0.0-ci.local` and `RELEASE_DIR ?= artifacts/release`. Pack
only `SharpVision.Terminal` and `SharpVision` into `$(RELEASE_DIR)/packages`.
Generate SPDX 2.2 with:

```bash
DOTNET_ROLL_FORWARD=Major dotnet sbom-tool generate \
  -b artifacts/release/packages -bc . -m artifacts/release/sbom \
  -pn SharpVision -pv "$VERSION" -ps SharpVision \
  -nsb https://github.com/pavkam/sharp-vision \
  -nsu "${GITHUB_SHA:-$VERSION}" -mi SPDX:2.2 -D true
```

Then run `node scripts/create-release-manifest.mjs artifacts/release`. Add no
registry source, API-key input, tag, release, or push command.

- [ ] **Step 5: Verify and commit**

```bash
node --test scripts/create-release-manifest.test.mjs
make release-artifacts VERSION=0.0.0-ci.local
scripts/verify-package-consumer.sh
if rg -n "nuget push|NUGET_.*KEY|api\.nuget\.org" Makefile scripts .github; then exit 1; fi
git add scripts/create-release-manifest.mjs scripts/create-release-manifest.test.mjs Directory.Build.props Makefile
git commit -m "chore(release): build verifiable package artifacts"
```

Expected: two `.nupkg` files, two `.snupkg` files, SPDX output, a deterministic
manifest, and no publication path.

### Task 5: Add reusable setup and quality actions

**Files:**

- Create: `.github/actions/setup/action.yml`
- Create: `.github/actions/build-and-test/action.yml`

- [ ] **Step 1: Create the setup action**

Inputs are `dotnet-version` defaulting to `10.0.x` and `node-version` defaulting
to `24`. Use:

```yaml
uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
uses: actions/setup-node@820762786026740c76f36085b0efc47a31fe5020 # v7.0.0
uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
```

Cache NuGet/tool packages from `.config/dotnet-tools.json`,
`Directory.Packages.props`, and project files; setup-node owns npm caching.
Finish with `dotnet tool restore`, `dotnet restore SharpVision.slnx`, and
`npm ci`.

- [ ] **Step 2: Create the build-and-test action**

Inputs are `configuration` (`Release`), required `artifact-suffix`, and
`canonical-coverage` (`false`). Run, in order:

```yaml
- run: make format-check
- run: make lint
- run: make build CONFIGURATION=${{ inputs.configuration }}
- run: make coverage CONFIGURATION=${{ inputs.configuration }}
- run: make package-consumer
```

Upload `TestResults/**` and `artifacts/coverage/**` with `if: always()` and:

```yaml
uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
```

For canonical coverage only, upload `artifacts/coverage/Cobertura.xml` through:

```yaml
uses: codecov/codecov-action@e53489f4d376d79066609109e7a95a29eb3740b1 # v7.0.0
with:
  files: artifacts/coverage/Cobertura.xml
  fail_ci_if_error: true
  use_oidc: true
  disable_search: true
```

- [ ] **Step 3: Validate and commit**

```bash
npx action-validator .github/actions/setup/action.yml
npx action-validator .github/actions/build-and-test/action.yml
npm run lint:action-pins
git add .github/actions
git commit -m "ci: add reusable cross-platform quality actions"
```

### Task 6: Replace CI with three-platform verification

**Files:**

- Modify: `.github/workflows/ci.yml`
- Modify: `package.json`
- Modify: `Makefile`

- [ ] **Step 1: Define triggers, least privilege, and concurrency**

Run for pull requests and pushes targeting `main`, plus manual dispatch. Default
to `contents: read`. Cancel superseded runs using:

```yaml
concurrency:
  group:
    ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

Checkout with:

```yaml
uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0
with:
  persist-credentials: false
```

- [ ] **Step 2: Add three explicit platform jobs**

Names are exactly `CI / Ubuntu`, `CI / macOS`, and `CI / Windows`. Each invokes
`./.github/actions/build-and-test` with a unique artifact suffix. macOS and
Windows retain `contents: read` only. Ubuntu additionally receives
`id-token: write`, `checks: write`, and `pull-requests: write`; canonical
Codecov upload is true only for pushes and same-repository pull requests.

- [ ] **Step 3: Publish trusted Linux test annotations**

Under `if: always()` and only for trusted events:

```yaml
uses: EnricoMi/publish-unit-test-result-action@d0a4676d0e0b938bc201470d88276b7c74c712b3 # v2.24.0
with:
  check_name: Test results / Ubuntu
  files: "**/TestResults/**/*.trx"
```

- [ ] **Step 4: Add the summary job**

The job needs all three platforms, runs with `if: always()`, and writes their
exact `needs.*.result` values to a Markdown table. Exit nonzero when any result
is not `success`; summary generation must not mask a failed platform.

- [ ] **Step 5: Validate and commit**

Expand `lint:actions` to validate both composite `action.yml` files after
`github-actionlint`, and add `npm run lint:actions` plus
`npm run lint:action-pins` to `make lint`. At this point every referenced file
exists and every remote action is immutable.

```bash
npm run lint:actions
npm run lint:action-pins
git add .github/workflows/ci.yml package.json Makefile
git commit -m "ci: verify pull requests across three platforms"
```

### Task 7: Add release readiness without publication

**Files:**

- Create: `.github/workflows/release-readiness.yml`

- [ ] **Step 1: Create the workflow**

Trigger on pushes to `main` and manual dispatch; use `contents: read`. A
`quality` job runs the Ubuntu reusable action with canonical coverage false. Its
dependent `artifacts` job runs setup and:

```bash
make release-artifacts VERSION="0.0.0-ci.${GITHUB_RUN_NUMBER}"
```

Upload `artifacts/release/**` for 30 days using upload-artifact v7.0.1. Add an
always-running summary of package, SBOM, and manifest status.

- [ ] **Step 2: Prove publication is absent, validate, and commit**

```bash
if rg -n "nuget push|NUGET_.*KEY|api\.nuget\.org|gh release|create-release" .github/workflows/release-readiness.yml; then exit 1; fi
npm run lint:actions
npm run lint:action-pins
git add .github/workflows/release-readiness.yml
git commit -m "ci(release): stage packages without publication"
```

### Task 8: Add security and dependency automation

**Files:**

- Create: `.github/workflows/codeql.yml`
- Create: `.github/workflows/dependency-review.yml`
- Create: `.github/workflows/scorecard.yml`
- Create: `.github/dependabot.yml`

- [ ] **Step 1: Add CodeQL**

Run on pull requests and pushes to `main`, Monday at `03:17 UTC`, and manual
dispatch. Grant `contents: read`, `packages: read`, and
`security-events: write`. Initialize C# with `build-mode: none` and use this
commit for both init and analyze:

```yaml
uses: github/codeql-action/init@bb16b9baa2ec4010b29f5c606d57d01190139edd # v4.37.1
```

- [ ] **Step 2: Add dependency review**

Run only on pull requests with `contents: read` and `pull-requests: write`:

```yaml
uses: actions/dependency-review-action@a1d282b36b6f3519aa1f3fc636f609c47dddb294 # v5.0.0
with:
  fail-on-severity: high
  deny-licenses: AGPL-3.0, GPL-3.0
  comment-summary-in-pr: always
```

- [ ] **Step 3: Add OpenSSF Scorecard**

Run on pushes to `main`, Wednesday at `04:23 UTC`, and manual dispatch. Use
checkout with credentials disabled, then:

```yaml
uses: ossf/scorecard-action@99c09fe975337306107572b4fdf4db224cf8e2f2 # v2.4.3
with:
  results_file: results.sarif
  results_format: sarif
  publish_results: true
```

Upload SARIF with `github/codeql-action/upload-sarif` at
`bb16b9baa2ec4010b29f5c606d57d01190139edd`, and retain the raw file with
upload-artifact v7.0.1. Grant only `contents: read`, `security-events: write`,
and `id-token: write`.

- [ ] **Step 4: Configure Dependabot**

Create weekly `nuget`, `npm`, and `github-actions` entries rooted at `/`,
staggered by weekday and hour in UTC, with `open-pull-requests-limit: 10`. Group
minor and patch updates per ecosystem; keep majors and security updates
independent. Apply `dependencies` and the ecosystem-specific label.

- [ ] **Step 5: Validate and commit**

```bash
npm run lint:actions
npm run lint:action-pins
npm run format:check
git add .github/workflows/codeql.yml .github/workflows/dependency-review.yml .github/workflows/scorecard.yml .github/dependabot.yml
git commit -m "ci(security): add dependency and supply-chain checks"
```

### Task 9: Add public contribution and support files

**Files:**

- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`
- Create: `SUPPORT.md`
- Create: `CODE_OF_CONDUCT.md`
- Create: `.github/CODEOWNERS`
- Create: `.github/pull_request_template.md`
- Create: `.github/ISSUE_TEMPLATE/bug.yml`
- Create: `.github/ISSUE_TEMPLATE/feature.yml`
- Create: `.github/ISSUE_TEMPLATE/config.yml`

- [ ] **Step 1: Write community health documents**

`CONTRIBUTING.md` states prerequisites, `make restore`, docs-first behavior,
focused Microsoft Testing Platform filters, red-green proof, all four Make
gates, conventional commits, XML documentation, public API review, and
terminal/control layer boundaries.

`SECURITY.md` supports only latest `main` before a stable release, routes
reports through GitHub private vulnerability reporting, prohibits public exploit
details, and defines acknowledgment and coordinated disclosure without promising
a fixed response time.

`SUPPORT.md` routes defects and proposals to issue forms, usage questions to
GitHub Discussions when enabled, and vulnerabilities to the private security
path.

`CODE_OF_CONDUCT.md` adopts Contributor Covenant 2.1, links its canonical text,
uses private GitHub reporting for enforcement contact, and includes the standard
correction, warning, temporary-ban, and permanent-ban ladder.

- [ ] **Step 2: Add ownership and pull-request proof**

Create `.github/CODEOWNERS`:

```text
* @pavkam
/.github/ @pavkam
/docs/ @pavkam
/src/ @pavkam
/tests/ @pavkam
```

The pull-request template checks linked intent, normative docs/API/showcase
agreement, focused red-green evidence, `make format/lint/build/test`, coverage
impact, public API review, and absence of unrelated changes.

- [ ] **Step 3: Add structured issue forms**

The bug form requires environment, terminal or multiplexer, exact
version/commit, reproduction, expected/actual behavior, and redacted logs. The
feature form requires problem, observable contract, alternatives,
terminal/platform impact, and proof strategy. Disable blank issues and link
security reports to
`https://github.com/pavkam/sharp-vision/security/advisories/new`.

- [ ] **Step 4: Validate and commit**

```bash
npm run format
npm run lint:markdown
npm run lint:links
git add CONTRIBUTING.md SECURITY.md SUPPORT.md CODE_OF_CONDUCT.md .github/CODEOWNERS .github/pull_request_template.md .github/ISSUE_TEMPLATE
git commit -m "docs: add public contribution and support policies"
```

### Task 10: Document CI and add backed README badges

**Files:**

- Create: `docs/testing/continuous-integration.md`
- Create: `.github/REPOSITORY_SETTINGS.md`
- Modify: `docs/testing/index.md`
- Modify: `README.md`

- [ ] **Step 1: Write the normative CI contract**

Document the local command mapping, three-platform matrix, test discovery,
retained diagnostics, coverage boundary and baseline update
procedure, Codecov OIDC trust boundary, artifact retention, security workflows,
release-readiness no-publication rule, and prohibition on retrying flakes. Link
it from the test map as `Continuous integration`.

- [ ] **Step 2: Document live repository settings**

List the post-merge ruleset with exact required checks `CI / Ubuntu`,
`CI / macOS`, `CI / Windows`, `codecov/project`, `codecov/patch`, and CodeQL;
require current branches, resolved conversations, and linear history; block
force pushes and deletion; set approvals to zero; retain administrator bypass.
List Codecov repository activation with GitHub OIDC, secret scanning, push
protection, Dependabot alerts, and private vulnerability reporting. State
explicitly that the document does not mutate GitHub settings.

- [ ] **Step 3: Add only backed badges**

Insert below the README title:

```markdown
[![CI](https://github.com/pavkam/sharp-vision/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/pavkam/sharp-vision/actions/workflows/ci.yml)
[![Coverage](https://codecov.io/gh/pavkam/sharp-vision/branch/main/graph/badge.svg)](https://codecov.io/gh/pavkam/sharp-vision)
[![CodeQL](https://github.com/pavkam/sharp-vision/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/pavkam/sharp-vision/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/pavkam/sharp-vision/badge)](https://scorecard.dev/viewer/?uri=github.com/pavkam/sharp-vision)
[![License](https://img.shields.io/github/license/pavkam/sharp-vision)](LICENSE)
[![Issues](https://img.shields.io/github/issues/pavkam/sharp-vision)](https://github.com/pavkam/sharp-vision/issues)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)
```

Replace the stale future-implementation statement with accurate
active-development language anchored to the protocol coverage matrix. Link
contributing, security, and support policies. Add no Pages, NuGet, download, or
release badge.

- [ ] **Step 4: Validate and commit**

```bash
npm run format
npm run lint:markdown
npm run lint:links
git add docs/testing/continuous-integration.md docs/testing/index.md .github/REPOSITORY_SETTINGS.md README.md
git commit -m "docs(ci): publish quality and governance contract"
```

### Task 11: Run the complete quality and security audit

**Files:**

- Modify only files that fail a demonstrated check.

- [ ] **Step 1: Run every local gate from a clean artifact state**

```bash
make clean
make format
make lint
make build
make test
make coverage
make release-artifacts VERSION=0.0.0-ci.audit
```

Expected: zero warnings and errors, the configured minimum discovered tests,
valid Markdown/links/actions, coverage at or above baseline, package-consumer
success, and complete release artifacts.

- [ ] **Step 2: Audit permissions and forbidden publication paths**

```bash
npm run lint:actions
npm run lint:action-pins
rg -n "permissions:|uses:" .github/workflows .github/actions
if rg -n "dotnet nuget push|NUGET_.*KEY|api\.nuget\.org|deploy-pages|upload-pages-artifact" .github Makefile scripts; then exit 1; fi
```

Expected: every remote action uses a full SHA, every workflow declares
permissions, and publication/Page patterns are absent.

- [ ] **Step 3: Review against the approved design**

```bash
git status --short
git diff --check
git log --oneline --decorate origin/main..HEAD
```

Map every acceptance criterion in
`docs/superpowers/specs/2026-07-17-public-ci-and-repository-readiness-design.md`
to file and command evidence. Confirm no live GitHub rules or security settings
changed.

- [ ] **Step 4: Commit demonstrated audit corrections only**

If verification required edits, stage only those files and commit
`chore(ci): complete public readiness audit`. If the tree is already clean,
create no empty commit.
