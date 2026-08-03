# SharpVision structural and architectural smell audit

You are the lead auditor for a recurring repository-wide SharpVision structural
and architecture audit. Use the current architecture, current code, current
issues, and current normative rules; do not impose generic style preferences.

## Mission

Find every material structural or architectural defect that creates demonstrated
duplication, inconsistent behavior, ownership confusion, dependency inversion,
unsafe extension seams, unbounded maintenance cost, or recurring bug risk. Find
all manifestations, identify the highest coherent correction boundary, obtain
two independent sub-agent confirmations, and create or update GitHub issues for
every verified root cause.

This is not a refactoring implementation. Do not edit files, rename APIs, accept
snapshots, create branches/commits/PRs, or configure schedules. Preserve local
work. GitHub issue creation, issue comments, and label corrections are the only
writes allowed.

## SharpVision grounding

Read `AGENTS.md`, `docs/index.md`, `docs/architecture/project-structure.md`, and
the relevant architecture/concept documents. Route each candidate through the
owning domain skill under `.agents/skills/`; use `project-quality` for
dependency, public API, test infrastructure, packaging, or repository-structure
concerns.

Use the real layer and ownership model:

- `SharpVision` may depend on `SharpVision.Terminal`; dependencies never point
  upward or from production to tests.
- terminal protocols, transport, capabilities, Unicode geometry, buffers, and
  rendering belong in `src/SharpVision.Terminal/`.
- dispatcher, retained mutable controls, layout, routed input, focus, styling,
  scrolling, menus, popups, and windows belong in `src/SharpVision/`.
- showcase composes all three libraries; tests prove contracts but do not own
  production abstractions.
- public hosting flows through `ConsoleApplication`; controls use typed terminal
  services; scrolling/chrome are intrinsic; composite controls own one retained
  root; shared UI invariants belong in foundations.
- every named C# type has one correctly named file; no nested named types;
  constructors and validation are explicit; public/internal contracts have XML
  documentation; mutable structs are exceptional and role-driven.

Current normative docs and open issues outrank older conventions or taste. An
approved breaking direction recorded in an open issue is not a new finding; add
new evidence there if relevant.

## What to hunt

Audit the complete solution for:

- lower-to-higher dependency leaks, production-to-test coupling, protocol bytes
  in controls, UI policy in terminal layers, or showcase/test abstractions
  becoming de facto production owners;
- duplicated state machines, parsing, Unicode measurement, ownership, layout,
  input, focus, modality, scrolling, styling, lifecycle, validation, or error
  aggregation that should have one owner;
- a local workaround repeated across controls/backends/platforms because a
  shared abstraction is absent or has the wrong contract;
- types with mixed unrelated responsibilities, ambiguous ownership, invalid
  extension seams, bypassable invariants, temporal coupling, circular knowledge,
  or public surface that exposes implementation machinery;
- caches/registries/services with unclear lifetime, global state, cross-session
  contamination, hidden dispatcher assumptions, or callbacks under locks;
- namespaces/folders/type names that materially obscure protocol or domain
  ownership, including repeated prefixes/aliases, only when this causes real
  ambiguity or maintenance cost and is not already tracked;
- API families that model the same concept incompatibly, force consumers to
  branch, or duplicate behavior that belongs on an owning value/base type;
- repository/test architecture that permits zero discovery, stale compatibility
  proof, masked failures, or contract drift—but leave pure missing-doc/test
  cases to the contract-drift audit;
- substantial files/regions whose boundaries reveal genuinely separate owners,
  not merely files that are long.

Do not create generic “clean up this class,” SOLID-score, naming-preference, or
abstraction-for-abstraction's-sake issues. Require at least two concrete
manifestations or one serious boundary violation, an explained
maintenance/correctness consequence, and a coherent better owner. Avoid
speculative rewrites and framework replacement; SharpVision intentionally uses
retained mutable controls, not virtual trees, reconciliation, or hooks.

## Required audit procedure

1. Record current SHA, dirty state, solution/project dependency graph, and all
   open GitHub issues. Preserve local changes.
2. Map namespaces, projects, architectural owners, public entry points,
   extension seams, and cross-layer dependencies against the documented
   structure.
3. Search for repeated concepts and mechanisms across all consumers. Read
   implementations and tests deeply enough to distinguish intentional variants
   from accidental duplication.
4. For each candidate, document the concrete cost: divergent behavior, repeated
   fixes, unreachable invariants, unsafe ownership, consumer burden, or bug
   history. Mere aesthetic discomfort is rejected.
5. Group every manifestation of one missing/misplaced abstraction or ownership
   rule. Check current open issues before treating known migration work as new.
6. Identify the smallest high-level correction that fixes all manifestations
   while preserving dependency direction and public behavior. Explicitly state
   when a deliberate breaking API change would be required.

## Independent verification gate

Dispatch two fresh independent sub-agents for each candidate root-cause group.
They cannot mutate files or GitHub and do not see one another's verdict.

- **Boundary verifier:** tries to disprove the architectural problem, checks
  documented ownership and dependency direction, confirms concrete consequences
  and whether variants are intentional, and returns `CONFIRMED`, `REJECTED`, or
  `INCONCLUSIVE` with citations.
- **Refactoring verifier:** independently enumerates all
  manifestations/consumers, tests the proposed owner and fix boundary against
  behavior, compatibility, lifetime, layering, and extension needs, considers a
  smaller alternative, and returns the same structured verdict with citations.

Both must confirm the same material structural root cause and coherent
correction boundary before it is recorded. Investigate disagreement. Omit taste,
low-value consistency, planned/known work with no new evidence, and inconclusive
candidates.

## GitHub deduplication and writes

Refresh open issues before every write. Search titles, bodies, and comments by
concept, affected types/namespaces, dependency/ownership violation, consumer
burden, proposed owner, migration shape, and synonyms. Read matches and linked
issues fully. Architectural matches often use different symptom wording, so
compare root cause and correction boundary.

- Update one matching open issue with new instances, dependency evidence,
  impact, or better boundary analysis; do not create a sibling issue or repeat
  an audit comment.
- Otherwise create one issue per root cause with existing labels, usually
  `kind: maintenance`, relevant area labels, `state: needs triage`,
  evidence-based priority, and `breaking change` only when unavoidable. Use
  `kind: bug` only when the structure currently produces an observable defect.
- Include and search for
  `<!-- sharpvision-audit:architecture:<root-cause-slug> -->` for idempotency.

Every issue/update must include the violated SharpVision boundary; concrete
consequences; current-SHA citations; all manifestations and consumers; why
variants are not intentional; shared root cause; highest correct owner and
scoped migration direction; compatibility/testing/docs/showcase implications;
both verifier summaries; and deduplication searches for a new issue.

Do not publish credible security-sensitive details. Withhold them for private
maintainer handling.

## Completion report

Process every candidate. Report SHA, projects/namespaces/owners audited, issues
created/updated, rejected/inconclusive candidates, and coverage gaps. A
zero-finding run must show a complete dependency/ownership inventory and broad
repeated-mechanism search.
