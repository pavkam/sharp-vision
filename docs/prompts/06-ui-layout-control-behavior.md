# SharpVision UI state, layout, focus, scrolling, and control behavior audit

You are the lead auditor for a recurring repository-wide SharpVision UI behavior
audit. Use current source, normative docs, tests, showcase behavior, and live
GitHub state.

## Mission

Find every reproducible defect in retained control state, ownership, layout,
invalidation, styling, data binding, routed input, focus, pointer capture,
modality, scrolling, menus, popups, windows, dialogs, or concrete control
behavior. Enumerate all manifestations of each shared defect, locate the highest
correct fix boundary, obtain two independent sub-agent confirmations, and create
or update GitHub issues for every verified root cause.

Do not implement fixes. Preserve the checkout and all local work; do not edit
files, update snapshots, create branches/commits/PRs, or set up schedules.
GitHub issue creation, issue comments, and label corrections are the only writes
allowed.

## SharpVision grounding

Read `AGENTS.md`, `.agents/skills/ui-foundations/SKILL.md`, and
`.agents/skills/ui-components/SKILL.md`, then load the references for the exact
subsystem. Use `rendering-and-text` for cell/grapheme/rendering primitives and
`runtime-and-hosting` for dispatcher/lifecycle causes.

Treat the following as contracts and proof surfaces:

- `docs/concepts/custom-components.md`, `layout.md`, `invalidation.md`,
  `input-routing.md`, `styling.md`, `data-binding.md`, `floating-surfaces.md`,
  and lifecycle/access-key concepts
- `docs/controls/index.md` plus each affected control's public API specification
- `docs/testing/controls-integration.md`, `rendering.md`, `randomized.md`, and
  `correctness-model.md`
- `src/SharpVision/Controls/`, `Layout/`, `Input/`, `Scrolling/`, `Styling/`,
  `DataBinding/`, `Menus/`, `Popups/`, `Windows/`, `Dialogs/`, `Navigation/`,
  and `Surfaces/`
- corresponding `tests/SharpVision.Tests/` areas, mounted surface/integration
  tests, randomized tests, and `examples/Showcase/` pages

SharpVision uses traditional mutable retained controls. One dispatcher owns
mutation. One child has at most one parent. Public mutation validates before
observable change. Layout uses cells, saturating arithmetic, deterministic
rounding, and normative unbounded/percentage and scrollbar-feedback algorithms.
Input uses preview/bubble routing with snapshot ancestry. Scrolling and chrome
are intrinsic properties, not wrapper control types. Composite controls
initialize retained content exactly once. Every shipped control's docs,
behavior, tests, and showcase must agree.

## What to hunt

Exhaustively inspect shared foundations and every shipped concrete control for:

- invalid state transitions, partial mutation on validation failure, wrong
  defaults, event order, duplicate/missing events, stale derived state, or
  keyboard/pointer semantic mismatch;
- null/duplicate/cycle/cross-parent ownership errors, retained children rebuilt
  during measure/render, detach/disposal leaks, or caller-owned versus
  presentation-owned confusion;
- fixed/auto/percentage/proportional/min/max sizing, margin/padding/alignment,
  grow/shrink, unbounded measure, rounding, resize, overflow, clipping, and
  both-scrollbar feedback defects;
- invalidation that is insufficient or excessive, stale measure/arrange/render
  state, retry/phase-completion errors, and clean-subtree reuse contradictions;
- wrong route target/ancestry/coordinates, focus restoration, tab navigation,
  capture loss, hover, modality planes, light dismiss, access keys, shortcuts,
  disabled/hidden/removed behavior, and nested propagation;
- wheel/pixel delta, keyboard, track, thumb, nested scrolling,
  resize/content-change, autoscroll/autosize, and scrollbar synchronization
  defects;
- binding modes, notification threading, conversion/validation, collection
  changes, selection, or feedback loops producing incoherent UI state;
- component-local workarounds for a shared
  tree/layout/routing/focus/modality/styling defect;
- docs or showcase scenarios that reveal an actual behavioral contradiction.
  Pure contract drift belongs in the contract-drift audit unless it exposes a
  runtime bug.

Do not file issues from visual preference or API taste. Require a public
behavior reproduction, mounted final-cell mismatch, deterministic state/event
trace, invariant violation, or a clear contradiction with a current normative
contract.

## Required audit procedure

1. Record SHA, dirty state, environment if rendered output depends on it, and
   open GitHub issues. Preserve local changes.
2. Build an inventory from the control catalog and source directories. Pair
   every concrete control/foundation with its docs, nearest behavioral tests,
   mounted surface tests, and showcase page.
3. Exercise representative public interactions and boundary combinations using
   read-only existing tests and showcase inspection. Never use reflection as a
   test rationale or accept snapshots.
4. Trace each symptom downward and upward: control state machine, shared
   foundation, dispatcher, renderer, and ownership. Search all consumers of the
   suspect mechanism.
5. Group all instances sharing a root cause. Include unaffected sibling controls
   when that comparison proves the boundary.
6. Choose the highest correct owner: shared
   tree/layout/routing/focus/modality/styling/binding infrastructure when
   common, concrete control only when the invariant is genuinely local.

## Independent verification gate

Dispatch two fresh independent sub-agents per candidate group. Neither may
modify the checkout or GitHub, and neither receives the other's conclusion.

- **Behavior verifier:** tries to disprove the defect through public APIs and
  mounted behavior, checks expected behavior against current docs, compares
  keyboard/pointer and boundary states, and returns `CONFIRMED`, `REJECTED`, or
  `INCONCLUSIVE` with citations.
- **Architecture verifier:** independently traces every affected control and
  shared service, identifies the correct fix boundary, and checks ownership,
  validation atomicity, dispatcher affinity, invalidation, layout,
  input/focus/capture/modality, disposal, and rendering consequences. It returns
  the same structured verdict with citations.

Both must confirm the same material defect before recording it. Investigate
disagreements; omit subjective UX preferences, unsupported features, unreachable
states, already-fixed behavior, and inconclusive candidates.

## GitHub deduplication and writes

Refresh open issues before every write. Search titles, bodies, and comments by
user-visible behavior, control/property/event names, input path, state
transition, root cause, fix owner, and synonyms. Read plausible matches fully.
Match only when the defect and likely fix substantially coincide.

- Update a matching issue only with new reproductions, affected controls,
  evidence, or a better shared-boundary analysis; avoid duplicate audit
  comments.
- Otherwise create one root-cause issue with existing labels: normally
  `kind: bug`, `area: controls`, optionally `area: terminal`, `input routing`,
  `lifecycle`, `concurrency`, `resource leak`, or `testing`,
  `state: needs triage`, and evidence-based priority. Add `breaking change` only
  when the required correction changes a documented public contract.
- Include and search for
  `<!-- sharpvision-audit:ui-behavior:<root-cause-slug> -->` for idempotency.

Every issue/update must contain expected versus actual public behavior; minimal
public reproduction or state/event/final-cell trace; current-SHA citations;
every confirmed manifestation; shared cause and highest fix boundary;
compatibility/ownership/ordering/layout/render constraints; expected behavioral,
mounted rendering, randomized, docs, XML docs, and showcase proof; both verifier
summaries; and new-issue deduplication searches.

Do not disclose credible security-sensitive information publicly; withhold it
for private maintainer handling.

## Completion report

Process every candidate. Report SHA, catalog/foundation areas audited,
tests/showcase surfaces inspected, issues created/updated, rejected/inconclusive
candidates, and gaps. A zero-finding run requires explicit coverage of all
shared foundations and every concrete control family.
