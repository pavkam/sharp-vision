# Documentation

## Load this reference when

Changing normative documentation, section structure, API tables, implementation
gap callouts, protocol sources, coverage claims, links, diagrams, or validators.

## Normative documentation

- [Documentation contract](../../../../docs/documentation-contract.md#documentation-contract)
- [Document kinds](../../../../docs/documentation-contract.md#document-kinds)
- [Implementation gaps](../../../../docs/documentation-contract.md#implementation-gaps)
- [Links and ownership](../../../../docs/documentation-contract.md#links-and-ownership)
- [Validation](../../../../docs/documentation-contract.md#validation)

## Required document spines

- Control: contract, API, Example, Expected behavior.
- Dialog: contract, API, Interaction, Example, Expected behavior.
- Concept and architecture: contract, topic sections, Expected behavior.
- Protocol: contract, Sources, protocol sections, Expected behavior.
- Testing: contract, evidence sections, Required evidence.

## Workflow

1. Read the current declaration, XML docs, observable tests, and showcase before
   changing a public contract.
2. Keep one normative owner and link other pages to the exact section.
3. Describe current gaps with the prescribed IMPORTANT callout adjacent to the
   intended rule.
4. Cite primary terminal standards or terminal-author documentation with version
   or access date.
5. Update code, tests, XML docs, examples, matrix state, and diagrams together
   when behavior changes.

## Project-specific traps

- Normative docs contain no TODO, TBD, internal milestones, delivery phases,
  acceptance plans, or agent instructions.
- Examples demonstrate a stated rule; they never define a hidden default.
- Coverage uses only the states defined by the protocol coverage matrix.
- `docs/superpowers` is an internal artifact location and is forbidden.

## Focused verification

```bash
npm run format:check
npm run lint:markdown
npm run lint:links
npm run test:docs
```
