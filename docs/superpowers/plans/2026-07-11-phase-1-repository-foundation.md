# Phase 1 Repository Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:executing-plans` to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish the .NET 10 solution, strict quality gates, normative
documentation, agent guardrails, domain skills, and verified project shells.

**Architecture:** Six SDK-style projects form a one-way dependency graph from
terminal core to UI toolkit to showcase, with one matching test project per
production project. Repository-wide configuration is installed before source
implementation so later phases inherit the same warnings, docs, naming,
formatting, and testing rules.

**Tech Stack:** .NET SDK 10.0.203, C# 14, xUnit v3 3.2.2, Shouldly 4.3.0,
Moq 4.20.72, Microsoft.NET.Test.Sdk 18.7.0, Prettier 3, Markdownlint CLI 2

---

## File map

- `SharpVision.slnx`: solution membership.
- `Directory.Build.props`: compiler, analyzer, XML docs, and package policy.
- `Directory.Packages.props`: centrally managed test dependency versions.
- `src/`: terminal library, UI library, and showcase executable.
- `tests/`: one xUnit v3 project per production project.
- `AGENTS.md`: repository-wide implementation contract.
- `.codex/skills/*/SKILL.md`: domain routing and invariants.
- `docs/`: normative protocol, architecture, concept, control, and test specs.
- `scripts/validate-doc-links.mjs`: local file and section-anchor validation.
- Root config, Makefile, workflow, and editor files: adapted from the requested
  nostalgia emulator repository.

### Task 1: Create the solution and dependency graph

**Files:**

- Create: `SharpVision.slnx`
- Create: `src/SharpVision.Terminal/SharpVision.Terminal.csproj`
- Create: `src/SharpVision/SharpVision.csproj`
- Create: `src/SharpVision.Showcase/SharpVision.Showcase.csproj`
- Create: `tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj`
- Create: `tests/SharpVision.Tests/SharpVision.Tests.csproj`
- Create: `tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj`

- [ ] **Step 1: Generate projects and solution**

```bash
dotnet new sln --name SharpVision --format slnx
dotnet new classlib -n SharpVision.Terminal -o src/SharpVision.Terminal -f net10.0 --no-restore
dotnet new classlib -n SharpVision -o src/SharpVision -f net10.0 --no-restore
dotnet new console -n SharpVision.Showcase -o src/SharpVision.Showcase -f net10.0 --no-restore
dotnet new xunit -n SharpVision.Terminal.Tests -o tests/SharpVision.Terminal.Tests -f net10.0 --no-restore
dotnet new xunit -n SharpVision.Tests -o tests/SharpVision.Tests -f net10.0 --no-restore
dotnet new xunit -n SharpVision.Showcase.Tests -o tests/SharpVision.Showcase.Tests -f net10.0 --no-restore
dotnet sln SharpVision.slnx add src tests
```

Expected: the solution lists three production and three test projects.

- [ ] **Step 2: Add one-way project references**

```bash
dotnet add src/SharpVision/SharpVision.csproj reference src/SharpVision.Terminal/SharpVision.Terminal.csproj
dotnet add src/SharpVision.Showcase/SharpVision.Showcase.csproj reference src/SharpVision/SharpVision.csproj
dotnet add tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj reference src/SharpVision.Terminal/SharpVision.Terminal.csproj
dotnet add tests/SharpVision.Tests/SharpVision.Tests.csproj reference src/SharpVision/SharpVision.csproj
dotnet add tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj reference src/SharpVision.Showcase/SharpVision.Showcase.csproj
```

Expected: the terminal library has no project reference and the showcase
reaches it transitively through `SharpVision`.

- [ ] **Step 3: Replace template test packages**

Each test project contains unversioned central references and no xUnit v2
packages:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" />
  <PackageReference Include="Moq" />
  <PackageReference Include="Shouldly" />
  <PackageReference Include="xunit.v3" />
</ItemGroup>
```

- [ ] **Step 4: Add assembly smoke tests**

Delete template placeholder types. Add an internal `AssemblyMarker` per
production project and one test per matching test project:

```csharp
[Fact]
public void Assembly_WhenLoaded_HasExpectedName()
{
    var name = typeof(AssemblyMarker).Assembly.GetName().Name;

    name.ShouldBe("SharpVision.Terminal");
}
```

Grant internal visibility only to the matching test assembly and adjust the
expected name in the other two tests.

- [ ] **Step 5: Restore and prove the test harness**

```bash
dotnet restore SharpVision.slnx
dotnet test SharpVision.slnx --no-restore --verbosity minimal
```

Expected: three tests pass without xUnit assembly collisions.

- [ ] **Step 6: Commit the project graph**

```bash
git add SharpVision.slnx src tests
git commit -m "build: create SharpVision solution structure"
```

### Task 2: Install strict .NET and repository policy

**Files:**

- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Create: `.gitattributes`
- Create: `.gitignore`

- [ ] **Step 1: Pin the SDK feature band**

```json
{
  "sdk": {
    "version": "10.0.203",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

- [ ] **Step 2: Add central package versions**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add shared build policy**

`Directory.Build.props` sets `net10.0`, C# 14, nullable, implicit usings,
deterministic builds, warnings as errors, XML documentation, latest stable
analyzers, repository metadata, and package metadata. Test and executable
projects may suppress missing XML diagnostics; libraries may not.

- [ ] **Step 4: Adapt the exemplar style policy**

Copy the complete C# formatting and naming discipline from
`/Users/alex/Development/nostalgia-es-1841-emulator/.editorconfig`. Add XML
documentation diagnostics while preserving `_camelCase`, file-scoped
namespaces, collection expressions, `var`, pattern matching, and async suffixes.

- [ ] **Step 5: Adapt attributes and ignore rules**

Copy `.gitattributes` and `.gitignore` from the exemplar. Preserve generated
snapshot rules, ignore local caches and build output, and do not ignore
`.codex/skills/`.

- [ ] **Step 6: Verify strict policy**

```bash
dotnet build SharpVision.slnx --configuration Release
dotnet format SharpVision.slnx --verify-no-changes --verbosity diagnostic
```

Expected: zero warnings, zero errors, and no formatting changes.

- [ ] **Step 7: Commit policy**

```bash
git add global.json Directory.Build.props Directory.Packages.props .editorconfig .gitattributes .gitignore src tests
git commit -m "build: enforce strict repository policy"
```

### Task 3: Add Markdown, Makefile, editor, and CI tooling

**Files:**

- Create: `.markdownlint.jsonc`
- Create: `.prettierignore`
- Create: `.prettierrc`
- Create: `package.json`
- Create: `package-lock.json`
- Create: `scripts/validate-doc-links.mjs`
- Create: `Makefile`
- Create: `.github/workflows/ci.yml`
- Create: `.vscode/*.json`

- [ ] **Step 1: Adapt Markdown configuration**

Copy the exemplar Markdownlint and Prettier settings. Exclude
`.codex/skills/**/SKILL.md` from Prettier because skill frontmatter is owned by
the skill format.

- [ ] **Step 2: Add deterministic Node tooling**

Create private package `sharpvision-docs` with pinned Prettier and
Markdownlint CLI 2 dependencies and these scripts:

```json
{
  "format": "prettier --write \"**/*.md\"",
  "format:check": "prettier --check \"**/*.md\"",
  "lint:markdown": "markdownlint-cli2 \"**/*.md\" \"#node_modules\"",
  "lint:links": "node scripts/validate-doc-links.mjs"
}
```

Run `npm install` once to create `package-lock.json`.

- [ ] **Step 3: Test-drive Markdown link validation**

Add one valid and one invalid local section link fixture. Verify the script
fails for the invalid fragment. Implement the validator with Node built-ins,
covering URL decoding, GitHub-style heading anchors, external URL exclusion,
code-fence exclusion, missing files, and missing fragments. Remove the invalid
fixture and verify success.

- [ ] **Step 4: Adapt root commands**

Create `restore`, `build`, `run`, `test`, `test-ci`, `lint`, `format`,
`format-check`, `clean`, `watch`, and `help` Make targets using
`SharpVision.slnx` and `src/SharpVision.Showcase`. `lint` runs .NET formatting,
Prettier, Markdownlint, and link validation.

- [ ] **Step 5: Adapt CI and editor settings**

Use .NET `10.0.x`, Node 24, `npm ci`, clean restore, all lint checks, Release
build, and Release tests with hang blame and TRX output. Point VS Code launch
and task paths at the new solution and showcase.

- [ ] **Step 6: Run and commit the tooling gate**

```bash
npm ci
make format
make lint
git add .github .vscode scripts Makefile package.json package-lock.json .markdownlint.jsonc .prettierignore .prettierrc
git commit -m "build: add documentation and CI quality gates"
```

Expected: every formatter, Markdown rule, and local link check passes.

### Task 4: Write repository and domain guardrails

**Files:**

- Create: `AGENTS.md`
- Create: `.codex/skills/terminal-protocols/SKILL.md`
- Create: `.codex/skills/unicode-cell-geometry/SKILL.md`
- Create: `.codex/skills/terminal-rendering/SKILL.md`
- Create: `.codex/skills/ui-controls/SKILL.md`
- Create: `.codex/skills/layout-input-events/SKILL.md`
- Create: `.codex/skills/testing-quality/SKILL.md`
- Create: `.codex/skills/docs-specifications/SKILL.md`

- [ ] **Step 1: Write root `AGENTS.md`**

Cover the repository map, dependency rule, docs-first workflow, argument
validation, `Debug.Assert`, contextual naming, logical whitespace, important
algorithm comments, Rune/span/memory preference, single-thread UI ownership,
terminal cleanup, and XML docs for public and internal members with examples
and exception contracts.

Mandate xUnit v3, Shouldly, Moq only at interaction boundaries,
Arrange/Act/Assert, `MethodName_WhenThis_ThatIsExpected`, parser fragmentation,
final-byte renderer proof, randomized invariants, and hang-blame commands.

- [ ] **Step 2: Write seven focused skills**

Every skill has valid `name` and `description` frontmatter, a precise trigger,
links to normative docs, domain invariants, focused verification commands, and
a requirement to update docs with behavior. Skills route; they do not duplicate
specifications.

- [ ] **Step 3: Validate and commit skills**

Run the skill-authoring validation process plus Markdown and link checks.
Confirm each approved domain has exactly one skill.

```bash
git add AGENTS.md .codex/skills
git commit -m "docs: add implementation guardrails and domain skills"
```

### Task 5: Create the normative documentation tree

**Files:**

- Create: `docs/index.md`
- Create: `docs/protocols/*.md`
- Create: `docs/architecture/*.md`
- Create: `docs/concepts/*.md`
- Create: `docs/controls/**/*.md`
- Create: `docs/testing/*.md`

- [ ] **Step 1: Write navigation and protocol coverage**

Create root/category indexes and a protocol matrix with explicit states:
typed/implemented, decoded/observable, extension/fallback, and unsupported.
Every matrix entry links inline to a protocol section.

- [ ] **Step 2: Write protocol specifications**

Create sourced files for ECMA-48, ANSI/VT, CSI, OSC, DCS/string commands, DEC
private modes, xterm, SGR, mouse, paste/focus, Kitty keyboard, Kitty clipboard,
Kitty graphics, iTerm2, sixel, tmux, and GNU screen. Each defines grammar,
limits, detection, milestone behavior, fallback, security, and tests. Distinguish
OSC 52 from Kitty OSC 5522.

- [ ] **Step 3: Write architecture and concept specifications**

Document project structure, event loop, rendering, capabilities, memory,
errors, Unicode geometry, styling, layout, scrolling, focus, routing,
threading, lifecycle, and degradation. Embed Mermaid diagrams where they make
dependencies, sequences, states, or ownership clearer.

- [ ] **Step 4: Write one contract per initial control**

Group by display, input, layout, collections, menus, and windows. Every control
documents purpose, inheritance, properties, events, validation, exceptions,
input/focus behavior, visual states, layout/rendering, examples, and tests.
Include `RichText`, every panel, scrollbars, scroll view, popup, menu, and window.

- [ ] **Step 5: Write testing specifications**

Document fixtures, correctness levels, parser fragmentation, Unicode/render
cases, randomized testing, pseudoterminals, performance gates, and showcase
screen tests.

- [ ] **Step 6: Validate and commit normative docs**

```bash
npm run format
npm run lint:markdown
npm run lint:links
git add docs
git commit -m "docs: establish SharpVision product specifications"
```

Expected: no Markdown, file-link, or section-anchor errors.

### Task 6: Prove Phase 1

**Files:**

- Create: `README.md`
- Modify: `src/SharpVision.Showcase/Program.cs`
- Modify: files reported by formatters

- [ ] **Step 1: Add an honest showcase entrypoint and README**

The entrypoint states that the repository foundation is installed and points to
`docs/index.md`; it does not simulate unimplemented controls. The README lists
the projects, current phase, SDK, root commands, documentation, and guardrails
without claiming later-phase functionality.

- [ ] **Step 2: Run workflow-equivalent verification**

```bash
make format
make lint
make build
make test
```

Expected: all commands exit zero; Release build has zero warnings and errors;
all three smoke tests pass.

- [ ] **Step 3: Inspect and commit Phase 1 completion**

```bash
git status --short
git diff --check
git add --all
git commit -m "chore: complete SharpVision repository foundation"
```

Expected: the working tree is clean and Phase 1 is ready for the terminal
protocol engine plan.
