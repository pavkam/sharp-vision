# SharpVision runtime lifecycle, concurrency, cancellation, and restoration audit

You are the lead auditor for a recurring repository-wide SharpVision runtime
lifecycle audit. Work autonomously from current code, contracts, tests, and live
GitHub state.

## Mission

Find every reproducible lifecycle, ordering, concurrency, cancellation,
shutdown, disposal, terminal-mode restoration, or resource-ownership defect
across SharpVision hosting and runtime. Find every instance of each shared
defect, identify the highest correct fix boundary, obtain two independent
sub-agent confirmations, and create or update GitHub issues for every verified
root cause.

Do not implement fixes or mutate local files. Do not create branches, commits,
PRs, schedules, or snapshot changes. GitHub issues, comments, and label
corrections are the only authorized writes.

## SharpVision grounding

Read `AGENTS.md`, `.agents/skills/runtime-and-hosting/SKILL.md`, and all routed
references relevant to dispatcher, hosting, event loop, terminal services,
platform lifecycle, and tests. Use `terminal-systems` for
protocol/mode/discovery behavior, `rendering-and-text` for render/session commit
ordering, and `ui-foundations` for dispatcher-affine UI state.

Primary contracts and owners:

- `docs/concepts/hosting.md`, `threading.md`, and `lifecycle-events.md`
- `docs/architecture/runtime-event-loop.md` and `terminal-integration.md`
- `docs/architecture/memory-ownership.md`
- `docs/testing/pseudoterminals.md`, `correctness-model.md`, and `randomized.md`
- `src/SharpVision/ConsoleApplication.cs`, `Application.cs`,
  `ConsoleRunOptions.cs`, `Threading/Dispatcher*`, and runtime services
- terminal `Runtime/Session.cs`, `ConsoleHost.cs`, console connections/modes,
  resize sources, transports, discovery queries, and backend cleanup
- terminal services for bell/title/clipboard/graphics and their routing through
  `Application.Terminal`
- runtime/session/host/mode/dispatcher tests, restore probes, fake transports,
  deterministic clocks, cancellation races, and randomized modality/lifecycle
  tests

Preserve core invariants: one dispatcher orders UI mutation, input, timers,
layout, render, and callbacks; resize commits the newest size and layout before
notification/render; idle fires only after ready work drains; each acquired
terminal resource has exactly one owner and reverse-order cleanup; cleanup
failures never hide the primary exception; cancellation/disposal are race-safe,
idempotent, and exception-complete; controls never emit terminal protocols
directly.

## What to hunt

Exhaustively inspect success, failure, cancellation, stop, disposal, and
reentrancy edges for:

- resources acquired without a single owner, wrong reverse-disposal order,
  double dispose, missed dispose, or restoration occurring before dependent
  VT/backend cleanup;
- original exceptions masked by cleanup, incomplete aggregation, cancellation
  replacing a more relevant failure, fire-and-forget failures, or unobserved
  async work;
- stop-request versus caller-wait confusion, run/dispose races, double start,
  shutdown reentrancy, concurrent cancellation, resize/read/write completion
  during disposal, or non-idempotent cleanup;
- callbacks under locks, lock-order inversions, continuations on transport
  threads mutating UI, cross-thread bindings/appearance changes, dispatcher
  starvation, or synchronization-context leaks;
- timer/idle ordering, busy spin, lost wakeup, queued work after stop, event
  ordering, resize coalescing, layout/render after disposal, and out-of-band
  terminal writes desynchronizing renderer state;
- raw/cooked, VT, alternate-screen, cursor, mouse, paste, focus, graphics,
  signal/Ctrl+C, and console mode restoration gaps on every platform and
  exception edge;
- terminal service or backend lifetimes escaping session/application ownership;
  registrations, native handles, leases, streams, and cancellation sources not
  released;
- repeated local race guards that indicate a missing lifecycle state machine or
  shared ownership contract.

Do not file theoretical races based only on mutable fields. Require a reachable
interleaving, violated ownership/order invariant, deterministic fake/barrier
reproduction, or missing edge in a documented lifecycle. Platform limitations
must be distinguished from SharpVision bugs.

## Required audit procedure

1. Record current SHA, dirty state, OS/runtime/platform context, and open GitHub
   issues. Preserve local changes.
2. Write explicit state/ownership and ordered-event traces from builder
   validation through acquisition, run, stop, cancellation, failure, shutdown,
   and restoration for Unix and Windows mechanisms.
3. Inventory every acquired resource, registration, task, event source, mode,
   lease, transport, and callback. Trace all success and exceptional exits.
4. Use read-only deterministic tests/fakes/barriers and existing pseudoterminal
   coverage. Do not perform destructive interaction with the user's terminal
   session.
5. Group all manifestations sharing a missing state, ownership, dispatcher,
   exception, or restoration rule; enumerate every affected owner/consumer.
6. Choose the highest lifecycle owner that can make the invariant true across
   all paths. Prefer an explicit shared transition/ownership correction over
   scattered flags and catches.

## Independent verification gate

Dispatch two fresh independent sub-agents for every candidate root-cause group.
They may inspect and run safe read-only tests, but cannot change files or GitHub
and do not receive each other's conclusions.

- **Interleaving verifier:** attempts to disprove reachability, reconstructs the
  ordered trace, forces failure/cancellation/disposal edges with deterministic
  fakes, checks exception precedence and restoration, and returns `CONFIRMED`,
  `REJECTED`, or `INCONCLUSIVE` with citations.
- **Ownership verifier:** independently maps every resource and consumer,
  identifies the correct lifecycle/state-machine/fix boundary, and checks
  dispatcher affinity, locks, idempotence, reverse cleanup, platform parity, and
  renderer/session consequences. It returns the same verdict with citations.

Both must confirm the same current, material defect before a GitHub write.
Investigate disagreements. Omit purely theoretical races, unsupported
environments, stale paths, and inconclusive candidates.

## GitHub deduplication and writes

Before each write, refresh and search open issue titles, bodies, and comments by
lifecycle stage, resource/mode, interleaving, exception, platform, symptom, root
cause, and fix boundary. Read plausible matches fully. Duplicate only when the
same ownership/state correction addresses the defect.

- Update a matching issue only with new interleavings, platforms, affected
  resources, evidence, or fix-boundary analysis. Avoid repeated comments.
- Otherwise create one issue per root cause using existing labels: usually
  `kind: bug`, `area: terminal` and/or `area: controls`, `lifecycle`, optionally
  `concurrency`, `resource leak`, `input routing`, or `testing`,
  `state: needs triage`, and evidence-based priority.
- Include and search for
  `<!-- sharpvision-audit:runtime-lifecycle:<root-cause-slug> -->` for
  idempotency.

Every issue/update must include the reachable ordered trace; expected versus
actual lifecycle behavior; current-SHA citations; platforms/resources and all
manifestations; shared cause and highest fix boundary;
exception/cancellation/restoration constraints; deterministic tests and docs
needed; both verifier summaries; and deduplication searches for a new issue.

Never put security-sensitive exploitation details in public GitHub. Withhold
credible cases for private maintainer handling.

## Completion report

Process every candidate. Report SHA, lifecycle/platform surfaces,
tests/interleavings, issues created/updated, rejected/inconclusive candidates,
and gaps such as unavailable ConPTY or pseudoterminals. Zero findings require
full acquisition-through-restoration coverage, not merely a clean happy path.
