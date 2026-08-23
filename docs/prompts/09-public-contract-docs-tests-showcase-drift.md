# SharpVision public API, documentation, tests, and showcase contract-drift audit

You are the lead auditor for a recurring repository-wide SharpVision
contract-coherence audit. Use current implementation, normative docs, generated
compatibility evidence, tests, showcase, packages, and live GitHub state.

## Mission

Find every material mismatch among shipped behavior, public/protected API, XML
documentation, normative specifications, protocol coverage claims, tests,
package-consumer proof, and showcase examples. Find all related drift produced
by the same missing update or ownership error, determine the correct source of
truth and fix boundary, obtain two independent sub-agent confirmations, and
create or update GitHub issues for every verified root cause.

Do not repair drift in this routine. Do not edit docs/code/tests/snapshots,
accept generated baselines, create branches/commits/PRs, or configure schedules.
Preserve all local work. GitHub issue creation, issue comments, and label
corrections are the only allowed writes.

## SharpVision grounding

Read `AGENTS.md`, `.agents/skills/project-quality/SKILL.md`, and its
documentation, testing, API-compatibility, and packaging references. For every
candidate, also read the owning product-domain skill so that feature evidence
remains in its proper domain.

Use these source-of-truth surfaces:

- `docs/index.md`, `docs/documentation-guide.md`, architecture/concept/protocol
  documents, `docs/controls/index.md`, and each public control spec
- protocol `coverage-matrix.md` and focused protocol documents
- public/internal XML documentation in both production assemblies
- `tests/SharpVision.Compatibility.Tests/` versioned Verify API snapshots and
  its `FirstPartyPackageVersionTests` first-party version-derivation check - the
  only package-consumption proof this repository keeps; packing and building
  external unprivileged-consumer mini-projects per control was retired as not
  worth its CI cost, so do not flag its absence as drift
- terminal/UI correctness, integration, mounted surface, randomized, and
  performance tests
- `examples/Showcase/` gallery, pages, interactions, and responsive rendered
  proof
- project files, NuGet metadata, CI/local command mapping, and publication
  contracts when package surface is involved

Normative docs describe supported public behavior and may not claim support
without typed implementation and tests. Every shipped concrete control needs
aligned API docs, behavioral/rendering tests, and a showcase page. Public API
shape is frozen by snapshots; do not propose standalone reflection/shape tests.
Internal seams used for tests remain non-public and are documented by the
invariant they prove. Public docs must be reader-facing, not agent plans or test
obligations.

## What to hunt

Audit all public types/features and declared coverage for:

- documented behavior, defaults, validation, exceptions, ordering, units,
  threading, ownership, fallback, or examples that contradict implementation;
- public/protected API added, removed, renamed, moved, or changed without
  coherent snapshot/version/migration handling;
- missing or misleading XML docs, especially undocumented exceptions,
  ownership/lifetime, dispatcher affinity, units, side effects, or validation;
- a documented protocol/control/feature marked supported without typed
  implementation and correct tests, or implementation that exists while
  coverage/docs deny or misstate it;
- concrete controls missing normative spec, behavioral tests, mounted rendering
  proof, showcase page, representative states, or responsive examples;
- tests proving helpers/private details rather than public behavior,
  zero-discovery risks, shape assertions duplicating snapshots, snapshots
  accepted without deliberate review, or project-reference consumers
  masquerading as packed-package proof;
- showcase code using obsolete APIs, bypassing public hosting/terminal services,
  demonstrating behavior different from docs, or omitting important shipped
  states;
- package metadata, dependencies, target framework, external-consumer
  accessibility, CI commands, and publication/version evidence drifting apart;
- a single feature migration partially applied across folders, namespaces, docs,
  tests, snapshots, examples, and links.

Do not file an issue for wording preference or merely sparse prose. Require a
concrete false claim, missing required proof, inaccessible/broken consumer
surface, stale example, compatibility risk, or project rule violation. If
runtime behavior is itself wrong, record it as a bug with the relevant
behavioral category; contract drift may be a manifestation in the same issue
rather than a duplicate.

## Required audit procedure

1. Record current SHA, dirty state, package/version context, and open GitHub
   issue inventory. Preserve local work.
2. Build a bidirectional inventory: implementation/public API to
   docs/tests/showcase/coverage, and every normative claim/catalog entry back to
   typed implementation and proof.
3. Inspect compatibility snapshots and consumer projects without accepting or
   regenerating tracked baselines. Verify actual test discovery and project
   inclusion from current configuration.
4. Pair every concrete control and protocol coverage entry with its required
   proof. Follow links and examples; distinguish intentional implementation-gap
   callouts that already reference issues.
5. Group all drift caused by one incomplete feature change or ownership error.
   Enumerate every stale file, API, test, example, snapshot, and link.
6. Identify the correct source of truth and fix boundary. Do not “fix” truthful
   docs to match an implementation bug when the normative contract is clearly
   intended, and do not demand code for a doc claim that should be removed.

## Independent verification gate

Dispatch two fresh independent sub-agents for every candidate group. Neither may
alter files, snapshots, or GitHub, and neither sees the other's result.

- **Contract verifier:** tries to disprove the mismatch by tracing the normative
  claim, public/XML API, actual public behavior, versioned snapshot, tests,
  package consumer, and showcase. It returns `CONFIRMED`, `REJECTED`, or
  `INCONCLUSIVE` with citations.
- **Ownership verifier:** independently enumerates all drift instances, decides
  which artifact owns the intended truth, locates the complete correction
  boundary, checks compatibility/migration/coverage implications, and returns
  the same structured verdict with citations.

Both must confirm the same material drift and source-of-truth decision before
recording it. Investigate disagreements. Omit stylistic documentation
preferences, deliberate documented gaps with matching open issues, stale
generated artifacts not shipped or gated, and inconclusive candidates.

## GitHub deduplication and writes

Refresh open issues before each write. Search titles, bodies, comments, and
linked implementation-gap references by feature/type/member, claimed behavior,
protocol/control, snapshot/version, consumer failure, showcase page, root cause,
and correction boundary. Read plausible matches fully.

- Update a matching issue only with new drift locations, proof gaps,
  compatibility evidence, or a clearer source-of-truth analysis. Avoid repeated
  comments and never create a second issue solely for docs/tests/showcase
  fallout already in a behavioral issue.
- Otherwise create one issue per root cause with existing labels:
  `kind: documentation` for purely reader-facing false/missing material,
  `kind: maintenance` for evidence/tooling/compatibility coherence, or
  `kind: bug` for broken shipped behavior; add relevant area labels, `testing`
  when applicable, `state: needs triage`, evidence-based priority, and
  `breaking change` only when correction changes the documented public contract.
- Include and search for
  `<!-- sharpvision-audit:contract-drift:<root-cause-slug> -->` for idempotency.

Every issue/update must include the conflicting artifacts and exact
contradiction; current-SHA citations; user/consumer impact; exhaustive drift
locations; chosen source of truth and why; complete fix boundary across
code/docs/XML/tests/snapshots/showcase/coverage/packages; required verification
commands/evidence without auto-accepting snapshots; both verifier summaries; and
deduplication searches for a new issue.

Never disclose credible security-sensitive details publicly. Withhold them for
private maintainer handling.

## Completion report

Process every candidate. Report SHA, inventory coverage,
compatibility/consumer/test/showcase checks, issues created/updated,
rejected/inconclusive candidates, and blocked evidence. Zero findings require
bidirectional coverage of all three assemblies, every declared control/protocol
family, snapshots, consumers, and showcase—not a spot check.
