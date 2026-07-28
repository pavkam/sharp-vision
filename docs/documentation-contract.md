# Documentation structure

## Documentation contract

SharpVision documentation is a versioned product surface. It serves readers,
contributors, and automated agents from the same Markdown source. A behavior is
documented only when its normative owner, public API names, validation,
fallback, and proof obligations agree with the current implementation.

Every normative page has one H1 title and a stable H2 spine. Additional H2 and
H3 sections may explain domain-specific behavior, but they do not replace the
required sections below.

## Document kinds

| Kind         | Location               | Required H2 sections, in order                                                                           | Owns                                                        |
| ------------ | ---------------------- | -------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| Control      | `docs/controls/**`     | `<Type> contract`, `API`, `Example`, `Test obligations`                                                  | One public control or authoring role.                       |
| Dialog       | `docs/dialogs/**`      | `<Type> contract`, `API`, `Interaction`, `Example`, `Test obligations`                                   | One modal task and its typed result.                        |
| Concept      | `docs/concepts/**`     | `<Topic> contract`, topic sections, `Test obligations`                                                   | Behavior shared by multiple public types.                   |
| Architecture | `docs/architecture/**` | `<Topic> contract`, ownership/flow sections, `Test obligations`                                          | Cross-layer ownership and ordering.                         |
| Protocol     | `docs/protocols/**`    | `<Protocol> contract`, `Sources`, protocol-specific grammar/detection/typed sections, `Test obligations` | One wire protocol or terminal-description family.           |
| Testing      | `docs/testing/**`      | `<Topic> contract`, `Required evidence`                                                                  | What constitutes acceptable proof.                          |
| Walkthrough  | `docs/walkthroughs/**` | Task-oriented imperative sections                                                                        | One complete public workflow; never new normative behavior. |

Category index pages are catalogs and are exempt from the per-page spine. They
must link directly to each page's contract anchor.

## Contract sections

A contract section answers, in this order:

1. What value, component, protocol, or workflow owns the behavior.
2. What a caller may rely on.
3. What the caller owns and what SharpVision retains or copies.
4. Which units, bounds, ordering, threading, and lifetime rules apply.
5. How invalid, unsupported, malformed, or unavailable input behaves.

Use RFC 2119-style uppercase words only for actual requirements. Ordinary
descriptive prose uses “must” sparingly and never hides a future intention.

## API sections

Public properties, methods, constructors, events, and typed values use inline
code and their exact C# spelling. A public surface is summarized with a table:

| Member    | Type   | Default | Contract                                                 |
| --------- | ------ | ------- | -------------------------------------------------------- |
| `Example` | `bool` | `false` | Describes the observable behavior, units, and ownership. |

Follow the table with numbered algorithms, validation bullets, or lifecycle
rules when a table cell would become a paragraph. Code examples illustrate an
already stated contract; they never define an otherwise undocumented default.

Before changing an API table:

1. Inspect the current declaration and XML documentation.
2. Confirm defaults from initializers and constructors.
3. Confirm validation and exception types from the public mutation boundary.
4. Confirm state, layout, rendering, or protocol effects from observable tests.
5. Link shared behavior to its single normative concept or architecture owner.

## Test obligations

Test sections describe observable proof, not private implementation calls. Use a
table whenever several proof layers apply:

| Layer                  | Required evidence                                                            |
| ---------------------- | ---------------------------------------------------------------------------- |
| Unit                   | Public validation, defaults, state changes, and deterministic output.        |
| Surface or integration | Cross-component behavior through the real ownership and routing boundary.    |
| End to end             | Final cells, bytes, lifecycle ordering, cleanup, or pseudoterminal behavior. |

After the table, bullets list feature-specific edge cases and a numbered list
describes any required multi-step scenario. Exact-byte, fragmentation,
randomized, Unicode, and allocation obligations remain in their dedicated
[testing specifications](testing/index.md#test-map).

## Links and ownership

- One page owns each normative rule; other pages link to its exact section.
- Relative links include the section anchor when the target is a contract.
- The [coverage matrix](protocols/coverage-matrix.md#coverage) is the only
  terminal-support summary.
- Protocol pages cite primary standards or terminal-author documentation and
  record an edition, version, or access date.
- Paths, commands, type names, and member names match the current repository.
- Normative pages contain no TODO, TBD, “handle edge cases,” or speculative
  support claim.

## Validation

Documentation changes run, in increasing scope:

1. `npm run format:check`
2. `npm run lint:markdown`
3. `npm run lint:links`
4. `npm run lint:docs-structure`
5. `npm run test:docs`
6. `make lint`

The repository gate also compiles XML documentation, examples, and the showcase,
so prose-only success is not evidence that a public surface is coherent.
