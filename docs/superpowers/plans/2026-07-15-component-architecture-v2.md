# Component Architecture v2 Execution Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use
> `superpowers:subagent-driven-development` or `superpowers:executing-plans`.
> Every behavior change is test-first, receives a specification review and a
> code-quality review, and is committed only after its focused verification
> passes.

**Goal:** Replace SharpVision's misleading `Control`/`Container`/`View`
inheritance with a truthful third-party extension contract, role-correct control
bases, central owned-control semantics, and orthogonal styling, focus, part, and
accessibility contracts.

**Normative design:**
`docs/superpowers/specs/2026-07-15-component-architecture-v2-design.md`.

**Tech stack:** .NET 10, C# 14, xUnit v3, Shouldly, Microsoft Testing Platform,
Markdown specifications, and the existing terminal frame/cell test harnesses.

## Baseline

The active implementation worktree is `.worktrees/component-architecture-v2` on
branch `codex/component-architecture-v2`, based on commit `779208c`.

The baseline proof run completed before any change:

- Release build: zero warnings, zero errors;
- tests: 1,377 passed, zero failed, zero skipped;
- the later `text-markup-merge` tip was intentionally not used because it has
  unrelated analyzer failures.

## Plan set and dependency order

1. [Foundation and external contract](2026-07-15-component-foundation.md)
   - correctness regressions;
   - one `ChangeImpact` abstraction;
   - unfriended consumer project;
   - protected extension kernel;
   - central owned-control registry and `Parent : Control?`;
   - cross-cutting traversal migration.
2. [Role hierarchy and built-in migration](2026-07-15-component-role-migration.md)
   - `ContentControl`;
   - `CompositeControl` and `Screen`;
   - `ItemsControl`;
   - concrete control migrations;
   - removal of `View`, meaningless `Children`, and hidden scroll APIs;
   - internal extraction of container scrolling/chrome responsibilities.
3. [Orthogonal contracts](2026-07-15-component-orthogonal-contracts.md)
   - open visual-state keys;
   - typed named parts and part styling;
   - independent tab/pointer semantics;
   - framework-neutral accessibility snapshots/actions;
   - bounded versioned theme files;
   - package-consumer proof.

The order is mandatory. Role bases cannot be correct until ownership is central,
and named parts/semantic traversal must use the same registry rather than
inventing a second tree.

## Global constraints

- Preserve the user's dirty main checkout. Work only in the isolated worktree.
- Use one named C# type per same-named file, file-scoped namespaces, explicit
  constructors, `var` locals, and complete XML documentation.
- Validate every public/protected argument before state changes.
- Do not expose internal transaction flags, managers, or raw transaction methods
  to make a test compile.
- Do not add `InternalsVisibleTo("SharpVision.Consumer.Tests")` or restore
  showcase friendship after its removal.
- Keep controls retained and mutable. Construction never happens from layout or
  rendering.
- Keep `AutoSize` and `AutoScroll` intrinsic to true `Container` panels; there
  is no `ScrollView` type.
- Keep every existing exact-cell, Unicode, scrolling, focus, capture, and
  application-order guarantee unless the normative design explicitly replaces
  the API shape.
- When a public name changes, update source, tests, docs, showcase, XML docs,
  and reflection/API assertions in the same verified task.

## Per-task verification rhythm

1. Add the smallest public-observable failing test.
2. Run the focused test and record the expected failure.
3. Implement the smallest coherent behavior.
4. Run the focused test to green.
5. Run the nearest existing control/style/layout/input suites.
6. Run `make format`, `make lint`, and `make build` before committing.
7. Request specification review, then code-quality review.

## Phase completion gate

At the end of each linked plan, run:

```bash
make format
make lint
make build
make test
```

The phase is incomplete with any warning, error, test shortfall, Markdown
failure, stale link, public undocumented member, friend-access shortcut, hidden
`new` member, or uncommitted generated formatting change.

## Final acceptance

- A real external assembly implements leaf, layout, content, composite, item,
  interactive, themed-part, and semantic controls using only public/protected
  APIs.
- Only true panels derive from `Container`.
- No semantic control exposes a bypassable arbitrary `Children` collection.
- No public control hides an inherited member with `new`.
- First layout of a composite performs no construction or ownership mutation.
- Every owned edge carries dispatcher, theme, Unicode policy, focus/capture,
  lifecycle, rendering, hit testing, navigation, and disposal consistently.
- Theme mutation, cascade ordering, open states, named parts, semantic actions,
  and bounded theme loading have focused and integration proof.
- The showcase uses no friend access and demonstrates the public authoring
  surface.
- The full repository quality gates pass from a clean worktree.
