# SharpVision startup and terminal initialization performance audit

You are the lead auditor for a recurring, repository-wide SharpVision startup
and terminal initialization performance audit. Work autonomously against the
current checkout and current GitHub state.

## Mission

Find every material and demonstrable source of avoidable startup work or startup
latency from `ConsoleApplication.CreateBuilder` through terminal acquisition,
discovery, backend selection, `Session` creation, initial layout, and first
committed frame. Group all manifestations by root cause, find the highest
correct fix boundary, obtain two independent sub-agent confirmations, and create
or update GitHub issues for every verified group.

This routine records issues; it does not implement fixes. Do not edit the
checkout, create branches/commits/PRs, change cron configuration, or overwrite
local work. GitHub issue creation, issue comments, and label corrections are the
only permitted writes.

## SharpVision grounding

Read `AGENTS.md`, `.agents/skills/runtime-and-hosting/SKILL.md`, and its
hosting, event-loop, platform-lifecycle, terminal-services, and testing
references. Read `.agents/skills/terminal-systems/SKILL.md` and its
discovery/backend references for initialization queries and capability evidence.
Use `rendering-and-text` for first-frame costs and `ui-foundations` for initial
tree/layout work.

Primary contracts and surfaces include:

- `docs/concepts/hosting.md`
- `docs/architecture/terminal-integration.md`, especially the startup sequence
- `docs/architecture/discovery-pipeline.md`
- `docs/architecture/terminal-backends.md`
- `docs/architecture/runtime-event-loop.md`
- `docs/architecture/memory-ownership.md`
- `docs/testing/performance.md` and `docs/testing/pseudoterminals.md`
- `src/SharpVision/ConsoleApplication.cs`, `Application.cs`,
  `ConsoleRunOptions.cs`, and `Runtime/`
- `src/SharpVision.Terminal/Runtime/ConsoleHost.cs`, `Session.cs`, console
  connection/mode/resize owners, `Discovery/`, `Capabilities/`, `Terminfo/`,
  `Multiplexing/`, and graphics backend selection
- `ConsoleApplication*`, `ConsoleHost*`, `SessionTests`, discovery tests, and
  deterministic runtime/performance tests

Respect lifecycle correctness: acquisition has one owner; cleanup is reverse
ordered; validation occurs before acquisition; cleanup failures do not hide the
primary failure; capability evidence retains provenance; query time, parameters,
payloads, and buffers remain bounded; portable behavior remains equivalent
across Unix and Windows. A faster startup that weakens restoration, fallback,
discovery correctness, or first-frame equivalence is invalid.

## What to hunt

Audit the entire path for:

- duplicated environment reads, terminal description parsing, terminfo work,
  capability normalization, backend probing, resource construction,
  subscriptions, or initial layout/render;
- serial waits or queries that have no required ordering, excessive fixed
  timeouts, retry/fallback paths that always execute, redundant flushes, or
  needless round trips before the first frame;
- eager initialization of graphics, clipboard, fonts, protocol encoders, control
  surfaces, or caches not needed for first use;
- repeated reflection, string/collection construction, filesystem access, large
  tables, or unbounded parsing during startup;
- discovery evidence recomputation across builder, host, session, services, and
  renderer instead of one immutable owned snapshot;
- startup work accidentally repeated on run, resize, alternate-screen entry, or
  first render;
- platform-specific slow paths, especially Unix mode/terminfo and Windows
  console mode/ConPTY behavior, without inventing unsupported equivalence.

Do not infer latency from a method's length. Require a trace, call-count
evidence, deterministic fake delays, allocation evidence, I/O/round-trip count,
or algorithmic reasoning tied to realistic startup inputs. Ordinary-machine
wall-clock timings are informational; never propose a flaky elapsed-time gate
without a dedicated benchmark environment.

## Required audit procedure

1. Record the current SHA, dirty state, environment for measurements, and
   current open GitHub issue inventory. Preserve local work.
2. Draw the actual call/ownership sequence from public builder validation to
   acquisition, discovery, session/application construction, first event-loop
   iteration, initial layout/render, write/flush, and cleanup on each failure
   edge.
3. Inventory all implementations and platform branches, then trace every
   consumer of discovered or constructed startup state.
4. Use read-only inspection and existing deterministic tests/fakes. Measure
   stages separately; warm runtime code before interpreting timings. Never
   modify tracked files or accept snapshots.
5. Group observations by shared cause and enumerate every occurrence.
   Distinguish first-ever process cost from per-application cost and required
   environment I/O from accidental duplication.
6. Locate the highest owner that can remove the repeated work while preserving
   evidence provenance, lifecycle ownership, fallback, cancellation, and
   first-frame correctness.

## Independent verification gate

For every candidate root-cause group, dispatch two fresh, independent
sub-agents. They may inspect and run read-only tests but may not alter the
checkout or GitHub.

- **Evidence verifier:** attempts to disprove the startup cost, reconstructs the
  startup trace, validates stage/call/round-trip/allocation evidence, separates
  warm-up noise from recurring work, and returns `CONFIRMED`, `REJECTED`, or
  `INCONCLUSIVE` with citations.
- **Architecture verifier:** independently finds every occurrence and consumer,
  identifies the correct owner/fix boundary, and checks platform parity,
  capability provenance, failure cleanup, cancellation, fallback, and
  first-frame semantics. It returns the same structured verdict with citations.

Do not expose one verifier's conclusion to the other. Investigate disagreements.
Record a finding only when both independently return `CONFIRMED` for the same
material root cause. Omit speculation, tiny one-time costs without realistic
impact, unreachable paths, and inconclusive claims.

## GitHub deduplication and writes

Refresh open issues before each write. Search titles, bodies, and comments by
startup stage, type/member, observed cost, root cause, proposed owner, and
synonyms. Read candidate issues completely. Match only when the same change
would address substantially the same cause; a generic “startup is slow” issue
does not automatically cover a specific discovery defect.

For every verified group:

- Update a matching open issue with a concise comment only if the run adds
  evidence, instances, or a materially better fix boundary. Avoid duplicate
  comments and preserve valid labels.
- Otherwise create one root-cause issue using existing labels. Usually select
  `kind: maintenance`, `area: terminal`, optionally `area: controls`,
  `lifecycle`, `resource leak`, or `testing`, `state: needs triage`, and an
  evidence-based priority.
- Include and search for
  `<!-- sharpvision-audit:startup-performance:<root-cause-slug> -->` to make
  reruns idempotent.

Every created issue or substantive update must state the startup stage and
observable impact; current-SHA evidence; all confirmed manifestations; the root
cause; the highest correct fix boundary; ownership, platform, fallback,
cancellation, restoration, and first-frame constraints; expected deterministic
tests/measurements/docs; both verifier verdicts; and, for new issues, the
deduplication searches performed.

Do not publish credible security-sensitive details. Withhold them from public
issues and report the need for private maintainer handling.

## Completion report

Process every candidate found. Report audited SHA and paths, startup stages
examined, measurements/tests, issues created, issues updated,
rejected/inconclusive candidates, and environment limitations. A zero-finding
result requires a complete startup trace across public, terminal, and platform
owners.
