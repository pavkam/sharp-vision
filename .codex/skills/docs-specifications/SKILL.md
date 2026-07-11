---
name: docs-specifications
description: Use when creating, updating, reviewing, or auditing SharpVision protocol, architecture, concept, control, testing, API, coverage-matrix, diagram, source-attribution, or section-link documentation.
---

# Documentation Specifications

## Overview

Treat `docs/` as the normative product contract. Behavior is incomplete when
the specification, implementation, tests, XML docs, and showcase disagree.

## Workflow

1. Start at `docs/index.md` and the relevant category index. Find the single
   normative section before writing new prose.
2. For terminal behavior, verify a primary standard or terminal-author source
   and record its edition/version or access date.
3. Write the behavioral contract before implementation. Remove ambiguous words
   and specify units, limits, ordering, ownership, fallback, errors, security,
   threading, and examples.
4. Keep one focused file per protocol/extension and one API contract per public
   control. Link shared concepts inline to their exact section.
5. Add Mermaid only for ownership, dependency, sequence, state, or layout
   relationships that prose cannot express as clearly.
6. Update the protocol coverage matrix, XML docs, tests, and showcase page in
   the same behavior change.
7. Format and validate Markdown, local files, section anchors, and skill links.

## Required content

Protocol specs include source, wire grammar, parameters, bounds, detection,
typed surface, milestone support, fallback, multiplexer/platform quirks,
security, examples, and test obligations.

Control specs include purpose, inheritance, properties, events, defaults,
validation, exceptions, ownership, input/focus behavior, visual states,
layout/rendering, accessibility semantics, examples, and test obligations.

Coverage uses only these states:

- typed and implemented;
- decoded and observable;
- extension API with safe fallback; or
- unsupported with a specific reason.

Do not mark typed/implemented until tests prove typed observable behavior.

## Invariants

- Normative behavior has one home; other pages link rather than copy it.
- Inline links target the section where a dependency matters, not a detached
  “see also” list.
- Examples illustrate an already stated contract and never define hidden rules.
- Diagrams and prose agree; inconsistency blocks completion.
- Normative docs contain no TODO, TBD, “handle edge cases,” or unsupported
  claims disguised as future intent.
- Paths, public names, commands, and coverage states match the current tree.

## Example review

For a mouse-protocol change, update its grammar/detection section, link pixel
coordinates to cell geometry, revise the matrix state only after typed event
tests pass, and update XML event docs plus the showcase interaction example.

## Verification

```bash
npm run format
npm run lint:markdown
npm run lint:links
npm run test:docs
make lint
```

## Common mistakes

- Copying the same rule into protocol, architecture, and control pages.
- Linking only to a file when a precise section owns the contract.
- Claiming broad support from a parser that merely ignores unknown input.
- Updating prose without tests, XML docs, coverage, or showcase behavior.
