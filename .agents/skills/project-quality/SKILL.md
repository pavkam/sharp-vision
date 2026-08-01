---
name: project-quality
description:
  Use when changing SharpVision normative documentation rules, documentation
  validators, shared test infrastructure, CI gates, public API compatibility
  snapshots, package consumers, NuGet metadata, publishing workflows, repository
  scripts, Make targets, or repository skill structure and validation.
---

# Project Quality

## Overview

Keep repository evidence trustworthy from normative contracts through test
discovery, compatibility, packaging, publication, and skill routing. This domain
owns shared policy and infrastructure, not feature-specific behavior.

## Workflow

1. Route the task to the smallest matching quality references.
2. Read the linked normative contracts and inspect the live workflow, project,
   script, fixtures, and tests before changing a gate.
3. Add a failing validator, compatibility, package-consumer, or workflow test.
4. Implement deterministic checks that fail visibly and cannot pass on zero
   discovery, masked commands, stale artifacts, or partial publication.
5. Keep feature evidence in its product domain; update shared policy only when
   the cross-repository contract changes.
6. Run the narrow validator, then the full repository gates.

## Reference routing

<!-- markdownlint-disable MD013 -->

| Task signal                                                                   | Read                                                            | Normative starting point                                                                        |
| ----------------------------------------------------------------------------- | --------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| Normative docs, API tables, section spines, links, gaps, coverage claims      | [documentation.md](references/documentation.md)                 | [Documentation guide](../../../docs/documentation-guide.md#overview)                            |
| Test framework, discovery, fixtures, CI commands, coverage, randomized policy | [testing.md](references/testing.md)                             | [Correctness model](../../../docs/testing/correctness-model.md#overview)                        |
| Public API snapshots, baselines, compatibility project, breaking changes      | [api-compatibility.md](references/api-compatibility.md)         | [Public API compatibility](../../../docs/testing/correctness-model.md#public-api-compatibility) |
| Pack, external consumers, NuGet metadata, versioning, publish workflow        | [packaging-and-release.md](references/packaging-and-release.md) | [Package publication](../../../docs/testing/continuous-integration.md#package-publication)      |
| `.agents/skills`, metadata, progressive disclosure, skill links and commands  | [skill-maintenance.md](references/skill-maintenance.md)         | [Project boundaries](../../../docs/architecture/project-structure.md#overview)                  |

<!-- markdownlint-enable MD013 -->

## Boundaries

- Product-domain skills own feature docs, tests, showcase behavior, and
  implementation-specific evidence.
- This skill owns the validators, harnesses, compatibility gates, packaging, CI,
  and conventions that make that evidence enforceable.
- A feature test change does not trigger this skill unless shared infrastructure
  or policy changes.

## Invariants

- Normative docs describe public behavior, never internal plans or agent
  workflow.
- Zero test discovery, skipped product gates, masked failures, and stale
  baselines cannot appear green.
- Public API compatibility and packed external-consumer proof remain independent
  evidence unless their contract is deliberately changed.
- CI and local commands exercise the same required projects and validators.
- Publishing is version-coherent, permission-minimal, and failure-visible.
- Repository skills have one canonical location, direct normative links, valid
  metadata, and executable focused commands.

## Common mistakes

- Treating a project-reference consumer as proof that packed NuGet artifacts
  work.
- Auto-accepting public API snapshots in CI.
- Copying product contracts into skills or contributor workflow pages.
- Adding a validator without a failing fixture and repository gate wiring.
