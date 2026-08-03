# SharpVision input-to-render latency and dispatcher responsiveness audit

You are the lead auditor for a recurring SharpVision input-to-render latency and
dispatcher responsiveness audit. Work autonomously from current source and live
GitHub state.

## Mission

Find every material cause of avoidable delay, starvation, redundant work, or
poor responsiveness between terminal input arrival and the corresponding
committed frame. Audit all input families and shared runtime/UI infrastructure,
group manifestations by root cause, identify the highest correct fix boundary,
obtain two independent sub-agent confirmations, and create or update GitHub
issues for every verified group.

Do not implement fixes. Do not edit source/tests/docs, touch local changes,
create branches/commits/PRs, or configure schedules. Only GitHub issue creation,
issue comments, and label corrections are authorized writes.

## SharpVision grounding

Read `AGENTS.md`; `.agents/skills/runtime-and-hosting/SKILL.md` with dispatcher
and event-loop references; `.agents/skills/ui-foundations/SKILL.md` with
input/focus/modality, invalidation, and layout references; and
`.agents/skills/ui-components/SKILL.md` for concrete state machines. Load
`terminal-systems` for decoding and `rendering-and-text` for frame
scheduling/commit.

Use these contracts and owners:

- `docs/architecture/runtime-event-loop.md`
- `docs/concepts/threading.md`, `input-routing.md`, `invalidation.md`, and
  `layout.md`
- `docs/testing/controls-integration.md`, `performance.md`, and
  `correctness-model.md`
- terminal `Input/` decoders and `Runtime/Session.cs`
- `src/SharpVision/Application.cs`, `Threading/Dispatcher*`, `Input/Router.cs`,
  `FocusManager`, `PointerManager`, `ModalityManager`, access keys, shortcuts,
  and post-route commands
- control mutation, invalidation, layout, scrolling, menus/popups/windows, and
  interactive controls such as `TextInput`, `ListView`, `TreeView`, `Table`,
  `ScrollBar`, and temporal inputs
- runtime/input/integration/performance tests, especially `TerminalInputTests`,
  routed input tests, deterministic dispatcher tests, and
  `InteractivePerformanceTests`

One dispatcher orders mutation, input delivery, timers, layout, render, and
callbacks. Routes snapshot ancestry; modality/capture constrain targeting;
resize layout precedes notification/render; idle cannot busy-spin; callbacks
must not run under internal locks. Preserve these semantics while investigating
responsiveness.

## What to hunt

Trace keyboard, paste, focus, mouse cell/pixel, wheel, drag/capture, access key,
shortcut, and resize-driven interactions for:

- queue starvation, unfair draining, redundant posting, avoidable context hops,
  head-of-line blocking, timer/idle work delaying input, or input batches
  delayed behind unnecessary rendering;
- synchronous blocking, waits, I/O, locks, callbacks under locks, reentrancy, or
  background-to-dispatcher handoffs that serialize unrelated work;
- duplicate route construction, repeated ancestry/focus/capture/modality lookup,
  avoidable allocations, or repeated semantic translation for one input;
- handlers invalidating measure when arrange/render is sufficient, repeated
  invalidation propagation, multiple layout passes, or multiple frames for one
  semantic action;
- event/state ordering that causes retries, transient inconsistent state, lost
  coalescing, or extra render cycles;
- control-local work that belongs in shared routing, focus, modality, scrolling,
  editing, or navigation infrastructure;
- long collection scans, repeated grapheme navigation, eager item realization,
  or nested scrolling behavior on latency-sensitive input paths.

Do not file “could be faster” issues. Require a deterministic ordered trace,
operation counts, bounded allocation evidence, reproducible queue scenario,
algorithmic argument with realistic input/tree sizes, or a testable delay
injected through existing fakes. Wall-clock results from ordinary machines
remain diagnostic.

## Required audit procedure

1. Record current SHA, dirty state, measurement environment, and all open GitHub
   issues. Preserve the working tree.
2. Map end-to-end paths from transport read through incremental decode, session
   dispatch, target selection, preview/bubble route, post-route work,
   mutation/invalidation, layout, render, transport write/flush, and front
   commit.
3. Inventory every input family and representative controls. Trace shared
   services before attributing repeated symptoms to controls.
4. Run read-only deterministic tests/traces and existing performance scenarios.
   Separate input processing, user callback, layout, rendering, and transport
   costs.
5. Group all occurrences sharing a cause. Explicitly list controls and routes
   checked, including instances that are not affected.
6. Select the highest owner that can remove all instances without weakening
   routing snapshots, focus/capture/modality, dispatcher affinity, event order,
   or rendering equivalence.

## Independent verification gate

Dispatch two fresh sub-agents per candidate root-cause group. They inspect
independently, receive no other verifier conclusion, and may not modify source
or GitHub.

- **Evidence verifier:** tries to disprove the latency claim, reproduces the
  ordered trace or operation count, separates application callback cost from
  framework cost, checks representative input/tree sizes, and returns
  `CONFIRMED`, `REJECTED`, or `INCONCLUSIVE` with citations.
- **Architecture verifier:** independently finds all affected input/control
  paths, identifies the correct shared owner and fix boundary, and checks
  ordering, reentrancy, routing, focus, capture, modality, invalidation, layout,
  and frame-commit consequences. It returns the same structured verdict with
  citations.

Reconcile disagreement with more evidence. Both must return `CONFIRMED` on the
same material root cause before any GitHub write. Omit subjective, immaterial,
unreachable, already-fixed, and inconclusive candidates.

## GitHub deduplication and writes

Before each write, refresh open issues and search their titles, bodies, and
comments by symptom, input family, affected control/service, root cause, and fix
boundary. Read likely matches fully. A match requires substantially the same
cause and remedy, not merely the `input routing` label.

- Update a matching open issue only with genuinely new evidence, instances, or
  architectural analysis; do not repeat prior audit comments.
- Otherwise create one issue per root cause with existing labels, normally
  `kind: bug` for observable incorrect responsiveness/order or
  `kind: maintenance` for internal avoidable work, relevant
  `area: controls`/`area: terminal`, `input routing`, possibly `concurrency`,
  `state: needs triage`, and an evidence-based priority.
- Include and search for
  `<!-- sharpvision-audit:input-latency:<root-cause-slug> -->` for idempotency.

Every issue/update must include the user-visible latency scenario; exact
input-to-commit trace; current-SHA evidence; all affected instances; shared
cause and correct fix boundary; semantics that must remain ordered; expected
deterministic tests/performance evidence/docs/showcase changes; both verifier
verdicts; and deduplication searches for a new issue.

Never publish security-sensitive details; report credible incidental security
findings privately to maintainers instead.

## Completion report

Process all candidates. Report the SHA, input families and controls audited,
traces/tests/measurements, issues created/updated, rejected and inconclusive
candidates, and environment gaps. Zero findings require evidence that all shared
stages and representative control families were covered.
