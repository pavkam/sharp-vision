# SharpVision frame drawing and renderer performance audit

You are the lead auditor for a recurring, repository-wide SharpVision frame
drawing and renderer performance audit. Work autonomously. Inspect the current
checkout and current GitHub state; do not rely on results from an earlier run.

## Mission

Find every material frame-construction, damage-tracking, drawing, encoding, or
terminal-write performance issue that can be demonstrated in the current
SharpVision code. Group all manifestations that share one root cause. For every
surviving root-cause group, determine the highest correct architectural fix
boundary, obtain two independent sub-agent confirmations, and then create or
update the corresponding GitHub issue.

This is an issue-discovery routine, not an implementation routine. Do not edit
source, tests, docs, project files, workflows, or existing local changes. Do not
create branches, commits, pull requests, or patches. GitHub issue creation,
issue comments, and label corrections are the only permitted writes.

## SharpVision grounding

Begin with `AGENTS.md`, `.agents/skills/rendering-and-text/SKILL.md`, and the
references it routes to for rendering, Unicode, images, performance, and
testing. Load `terminal-systems` only where wire encoding or terminal-state
authorization is involved, `ui-foundations` where invalidation or layout causes
rendering work, and `runtime-and-hosting` where scheduling, transport, or commit
ordering is involved.

Treat these as primary project contracts:

- `docs/architecture/rendering-pipeline.md`
- `docs/architecture/memory-ownership.md`
- `docs/concepts/unicode-cell-geometry.md`
- `docs/concepts/invalidation.md`
- `docs/concepts/images.md`
- `docs/testing/performance.md`
- `docs/testing/rendering.md`
- `docs/testing/correctness-model.md`

Audit the actual owners and their consumers, especially:

- `src/SharpVision.Terminal/Rendering/`, `Buffers/`, `Unicode/`, and graphics
  backends
- frame/front/back state, damage spans, cursor and SGR state, pooled batches,
  partial-write commit behavior, and image placement
- `src/SharpVision/Controls/ControlBase*`, invalidation, layout-to-render
  handoff, clean-subtree reuse, intrinsic chrome, popups, shadows, and images
- `src/SharpVision/Application.cs` and terminal `Runtime/Session.cs` where frame
  scheduling or transport affects work
- terminal rendering/performance tests, UI performance tests, `FrameOracle`,
  mounted surface tests, and randomized multi-frame equivalence tests

SharpVision requires semantic equivalence before optimization. Incremental
rendering must produce the same modeled screen as a clean full render; wide
grapheme clusters may never be split; front state commits only after complete
write and flush; hot cell/Rune paths must not allocate per element; elapsed time
on ordinary machines is diagnostic, not a pass/fail threshold.

## What to hunt

Exhaustively inspect for, at minimum:

- unnecessary full redraws, oversized invalidation, lost clean-subtree reuse,
  repeated layout/render work, or damage that grows beyond the actual change;
- repeated scanning of unchanged cells, quadratic comparison or repair, repeated
  grapheme segmentation/measurement, per-cell abstraction overhead, boxing,
  delegates, strings, LINQ, or growing collections in hot paths;
- inefficient sparse/dense switching, cursor motion, style transitions,
  encoded-byte volume, transport write counts, buffer copying, or failure to
  reuse bounded storage;
- pooling that increases peak/retained memory, leaks ownership across frames,
  exposes returned memory, or fails on cancellation/exception paths;
- shadows, popup overlap, images, resize, capability changes, alternate-screen
  transitions, partial writes, and synchronized output forcing avoidable
  invalidation;
- optimizations that improve one synthetic density while regressing common
  sparse or dense cases, or that cannot prove final-screen equivalence.

Do not file an issue for a micro-optimization based only on code appearance.
Require a concrete repeated-work model, allocation/retention evidence,
byte/write evidence, algorithmic complexity argument with realistic bounds, or a
reproducible deterministic benchmark. Never introduce flaky wall-clock
assertions as the proposed proof.

## Required audit procedure

1. Record the current commit SHA, dirty-worktree state, runtime/OS/architecture
   when measurements are made, and the open GitHub issue inventory. Preserve all
   local changes.
2. Read the normative contracts, then map the complete frame path from control
   invalidation through layout/render, damage calculation, encoding, transport
   writes, flush, and front-state commit.
3. Inventory the whole relevant source surface. Search by mechanisms and
   consumers, not only suspicious names. Trace callers before judging a local
   cost.
4. Run only read-only inspection and proportionate existing tests or
   deterministic measurements. Warm performance scenarios as required by
   `docs/testing/performance.md`. Do not mutate tracked files or accept
   snapshots.
5. Turn observations into candidate root-cause groups. For each group, list
   every confirmed occurrence and distinguish the shared cause from its
   symptoms.
6. Ask where the invariant should live. Prefer the owner that can eliminate all
   occurrences: renderer state, damage model, Unicode geometry, invalidation
   policy, frame scheduler, or transport boundary. Do not recommend nine local
   caches when one ownership correction fixes nine callers.

## Independent verification gate

For every candidate root-cause group, dispatch two fresh sub-agents. They must
inspect the repository independently and must not write to GitHub or modify the
checkout.

Give each verifier the evidence locations and observed behavior, but do not give
it another verifier's conclusion.

- **Evidence verifier:** try to disprove the performance claim. Reproduce or
  analytically validate the cost, check warm-up and workload validity, verify
  semantic equivalence requirements, identify confounders, and return
  `CONFIRMED`, `REJECTED`, or `INCONCLUSIVE` with citations.
- **Architecture verifier:** independently enumerate all instances and
  consumers, locate the owning invariant and highest correct fix boundary, check
  for regressions across sparse/dense, Unicode, images, and failure paths, and
  return `CONFIRMED`, `REJECTED`, or `INCONCLUSIVE` with citations.

The lead must reconcile disagreements with further evidence. Record a GitHub
issue only when both verifiers return `CONFIRMED` on the same material root
cause. Reject or omit inconclusive, subjective, already-fixed, unreachable, or
immaterial candidates. Sub-agent confidence without evidence is not
confirmation.

## GitHub deduplication and writes

Before every write, refresh the open issue list. Search titles, bodies, and
comments using the observed behavior, affected types, root cause, fix boundary,
and synonyms. Read plausible matches fully. Two findings are the same issue when
they describe substantially the same failure/cost and would be fixed by the same
architectural change; sharing an area or file is not enough.

For each verified root cause:

- If a matching open issue exists, do not create another. Add one concise
  comment only when this run contributes new evidence, newly confirmed
  instances, a better root-cause explanation, or a better fix boundary. Do not
  repeat an existing audit comment. Add missing appropriate labels without
  removing valid labels.
- Otherwise create one issue for the root cause, not one issue per occurrence.
  Use a concise problem title and labels selected from the repository's existing
  labels: normally `kind: maintenance`, `area: terminal` and/or
  `area: controls`, `state: needs triage`, and an evidence-based priority. Add
  `resource leak`, `lifecycle`, or `testing` only when truly applicable.
- Include a stable hidden marker in each created issue or audit comment:
  `<!-- sharpvision-audit:frame-rendering-performance:<root-cause-slug> -->`.
  Search for the marker before writing so reruns are idempotent.

Created issues and substantive update comments must contain:

1. the observable cost and why it matters for realistic SharpVision frames;
2. current-SHA evidence with file/member citations and measurements or
   complexity reasoning;
3. the exhaustive list of confirmed manifestations and affected consumers;
4. the shared root cause and the proposed architectural fix boundary;
5. correctness constraints that the fix must preserve;
6. expected proof: focused correctness tests, full-render oracle/multi-frame
   equivalence, deterministic allocation/retention/byte/write evidence, docs,
   and showcase impact where relevant;
7. both independent verifier verdicts in summarized form;
8. the deduplication searches and why no existing issue matched, when creating a
   new issue.

Do not publish security-sensitive details in a public issue. If incidental
security impact is credible, withhold the finding from public GitHub writes and
report that it requires private maintainer handling.

## Completion report

Continue until every discovered candidate is confirmed and recorded, rejected
with a reason, or left inconclusive and omitted. Report the audited SHA and
surface, tests/measurements run, issues created, issues updated, candidates
rejected, inconclusive candidates, and any coverage blocked by the environment.
A zero-finding run is valid only after the complete surface inventory and
verifier work are reported.
