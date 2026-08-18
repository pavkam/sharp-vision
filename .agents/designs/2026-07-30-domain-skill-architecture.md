# Domain Skill Architecture

## Objective

Replace the overlapping repository skills with six stable domain entry points.
Each entry point must make ownership obvious from its metadata, load only the
topic references required by the current task, and route every behavioral claim
back to the normative project documentation.

The skill system guides repository work. It does not become a second product
specification.

## Domain entry points

| Skill                 | Ownership                                                                                                                                                     |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `terminal-systems`    | Protocols, input decoding, capabilities, discovery, terminal descriptions, backends, multiplexers, clipboard, and protocol-side graphics.                     |
| `rendering-and-text`  | Cells, Canvas, frames, damage, terminal emission, Unicode geometry, text layout, image placement, FIGlet, and rendering performance.                          |
| `ui-foundations`      | Control-tree ownership, layout, scrolling, invalidation, styling, themes, data binding, routed input, focus, and modality infrastructure.                     |
| `ui-components`       | Concrete controls, collections, navigation, menus, popups, windows, dialogs, text editing, showcase pages, and mounted component proof.                       |
| `runtime-and-hosting` | Dispatcher execution, application and session hosting, transport, event-loop ordering, resize, timers, terminal services, shutdown, and platform restoration. |
| `project-quality`     | Documentation validation, shared test infrastructure, API compatibility, packaging, publishing, repository tooling, and skill maintenance.                    |

The implementation owner determines the primary skill. Consuming another
domain's public API does not trigger that domain's skill. A task loads a second
skill only when it changes implementation owned by both domains.

Feature-specific documentation and evidence remain with the product-domain
skill. `project-quality` owns the infrastructure and policies that validate,
package, and publish them.

## Boundary decisions

- Modality infrastructure belongs to `ui-foundations`; Window, Popup, Dialog,
  and Menu behavior belongs to `ui-components`.
- Unicode geometry, cell ownership, and text layout belong to
  `rendering-and-text`; `TextInput` editing state belongs to `ui-components`.
- Graphics grammar, capability negotiation, and backend protocol encoding belong
  to `terminal-systems`; image placement and frame composition belong to
  `rendering-and-text`.
- Dispatcher execution, event ordering, hosting, and terminal restoration belong
  to `runtime-and-hosting`; UI domains consume their public contracts.
- Application-surface composition belongs to `ui-components`; layout-engine
  algorithms belong to `ui-foundations`.

## Skill structure

Every domain uses the same shallow structure:

```text
skill-name/
├── SKILL.md
├── agents/
│   └── openai.yaml
└── references/
    ├── topic.md
    └── testing.md
```

`SKILL.md` remains a concise domain gateway. It contains:

1. YAML metadata with a comprehensive trigger description.
2. The domain purpose and ownership boundary.
3. The core orientation and implementation workflow.
4. Non-negotiable invariants shared across the domain.
5. Cross-domain routing rules.
6. A table mapping task signals to references and normative documentation.

Detailed topic guidance lives one level below `SKILL.md`. References never link
to deeper skill-owned reference layers.

## Reference contract

Each `references/*.md` file uses this structure when the sections apply:

1. `Load this reference when`
2. `Normative documentation`
3. `Code map`
4. `Test and showcase map`
5. `Workflow`
6. `Project-specific traps`
7. `Focused verification`

Every reference links directly to the exact owning sections under `docs/`,
including the relevant testing specification. An index link may supplement but
never replace a contract-section link.

References may describe repository navigation, ownership, workflows, commands,
and known failure modes. They must not duplicate normative defaults, public API
contracts, algorithms, or support claims. When code and documentation disagree,
the reference instructs the worker to expose and reconcile the discrepancy
rather than silently selecting one source.

Every domain has `references/testing.md`. It is loaded before changing tests or
claiming behavioral completion. A domain receives `references/design.md` only
when it has genuine cross-topic design decisions; symmetry alone is not a reason
to add a file.

## Reference inventory

### `terminal-systems`

- `protocols.md`
- `input.md`
- `discovery-and-backends.md`
- `graphics-protocols.md`
- `testing.md`

### `rendering-and-text`

- `rendering.md`
- `unicode.md`
- `images.md`
- `figlet.md`
- `performance.md`
- `testing.md`

### `ui-foundations`

- `control-tree.md`
- `layout-and-scrolling.md`
- `input-focus-and-modality.md`
- `styling-and-themes.md`
- `data-binding.md`
- `testing.md`

### `ui-components`

- `controls.md`
- `collections-and-navigation.md`
- `floating-surfaces.md`
- `text-editing.md`
- `design.md`
- `showcase.md`
- `testing.md`

### `runtime-and-hosting`

- `dispatcher.md`
- `hosting.md`
- `event-loop.md`
- `terminal-services.md`
- `platform-lifecycle.md`
- `testing.md`

### `project-quality`

- `documentation.md`
- `testing.md`
- `api-compatibility.md`
- `packaging-and-release.md`
- `skill-maintenance.md`

## Migration

The migration is a clean replacement. The existing ten skill directories are
removed in the same change that adds the six domain skills. There are no alias
skills, compatibility stubs, or duplicate trigger descriptions.

Existing material is redistributed as follows:

| Existing material           | Destination                                                                                               |
| --------------------------- | --------------------------------------------------------------------------------------------------------- |
| `terminal-protocols`        | `terminal-systems` protocol and input references.                                                         |
| `terminal-rendering`        | `rendering-and-text` rendering and performance references.                                                |
| `unicode-cell-geometry`     | `rendering-and-text/references/unicode.md`.                                                               |
| `figlet-fonts`              | `rendering-and-text/references/figlet.md`.                                                                |
| `layout-input-events`       | Split among UI foundation and runtime references according to ownership.                                  |
| `ui-controls`               | `ui-components` control and component references.                                                         |
| `designing-user-interfaces` | Split between UI foundation layout guidance and UI component design and showcase guidance.                |
| `ui-control-testing`        | `ui-components/references/testing.md`.                                                                    |
| `testing-quality`           | Domain evidence moves to each domain testing reference; shared harness policy moves to `project-quality`. |
| `docs-specifications`       | `project-quality/references/documentation.md`.                                                            |

Stale material is rewritten against current documentation, declarations, tests,
and scripts rather than copied mechanically. Useful invariants receive an
explicit destination before their old skill is removed.

The canonical skill location remains `.agents/skills`. Existing `.claude` and
`.codex` directory symlinks remain unchanged.

## Validation

`project-quality` owns deterministic skill validation. Validation must check:

- exactly six domain skill directories exist;
- every skill has valid `SKILL.md` frontmatter and matching `agents/openai.yaml`
  metadata;
- skill and reference links resolve, including section anchors where practical;
- referenced source paths, public type names, scripts, and test projects exist;
- old skill names and retired API terminology are absent;
- focused test commands use supported filter grammar and
  `--minimum-expected-tests`;
- representative focused commands discover at least the declared minimum;
- references contain direct normative-documentation links;
- no reference duplicates a product contract or claims unsupported behavior.

Mechanical checks belong in a repository script when textual review would be
fragile or repeatedly rewritten. Semantic checks remain explicit review criteria
in `project-quality/references/skill-maintenance.md`.

## Verification strategy

Validation proceeds from narrow to broad:

1. Validate each skill folder and metadata file.
2. Check repository-relative links and stale skill names.
3. Execute the skill-maintenance validator.
4. Execute representative focused commands from every domain and prove nonzero
   discovery.
5. Run documentation structure and link checks.
6. Run `make format`, `make lint`, `make build`, and `make test`.

The migration is complete only when all six skills are independently
discoverable, every old entry point is gone, direct documentation routing is
present, and the repository quality gates pass.

## Out of scope

- Changing product behavior, public APIs, or normative contracts.
- Adding aliases for old skill names.
- Recreating `docs/superpowers` or storing implementation plans in normative
  documentation.
- Duplicating general coding rules already enforced by repository instructions.
