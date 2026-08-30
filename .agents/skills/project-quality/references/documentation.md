# Documentation

## Load this reference when

Changing normative documentation, section structure, API tables, implementation
gap callouts, protocol sources, coverage claims, links, diagrams, or validators.

## Normative documentation

- [Documentation guide](../../../../docs/documentation-guide.md#overview)
- [Page types](../../../../docs/documentation-guide.md#page-types)
- [Control-page contract](../../../../docs/documentation-guide.md#control-page-contract)
- [Control-page template](../../../../docs/documentation-guide.md#control-page-template)
- [Implementation gaps](../../../../docs/documentation-guide.md#implementation-gaps)
- [Callouts](../../../../docs/documentation-guide.md#callouts)
- [Links and ownership](../../../../docs/documentation-guide.md#links-and-ownership)
- [Validation](../../../../docs/documentation-guide.md#validation)

## Required document spines

- Control: Overview, Inheritance, API, Keyboard, Example, Expected behavior —
  follow the
  [control-page template](../../../../docs/documentation-guide.md#control-page-template)
  exactly.
- Dialog: Overview, API, Interaction, Example, Expected behavior.
- Concept and architecture: Overview, topic sections, Expected behavior.
- Protocol: Overview, Sources, protocol sections, Expected behavior.
- Testing: Overview, evidence sections, Required evidence.

## Workflow

1. Read the current declaration, XML docs, observable tests, and showcase before
   changing a public contract.
2. For a control or authoring-role page, follow the
   [control-page template](../../../../docs/documentation-guide.md#control-page-template)
   exactly — do not copy an existing page's structure as a starting point; the
   current catalog predates the hardened contract and does not conform to it.
3. Keep one normative owner and link other pages to the exact section.
4. Describe current gaps with the prescribed IMPORTANT callout adjacent to the
   intended rule.
5. Cite primary terminal standards or terminal-author documentation with version
   or access date.
6. Update code, tests, XML docs, examples, matrix state, and diagrams together
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
npm run lint:doc-content
npm run test:docs
```

`npm run lint:docs-samples` compiles every documentation `csharp` sample and
needs a prior Release build of `SharpVision` and `SharpVision.FigletFonts`
(`dotnet build src/SharpVision/SharpVision.csproj --configuration Release` and
the same for `src/SharpVision.FigletFonts/SharpVision.FigletFonts.csproj`)
before it can find their assemblies.
