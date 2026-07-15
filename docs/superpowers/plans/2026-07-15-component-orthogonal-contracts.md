# Component Orthogonal Contracts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use
> `superpowers:subagent-driven-development` or `superpowers:executing-plans`.
> Begin after the role-migration plan is green.

**Goal:** Make styling, visual states, named parts, focus traversal,
accessibility, and theme loading as extensible and deterministic as the new
control hierarchy.

## Task 1: Replace closed state flags with registered keys

**Production:**

- Create `VisualStateKey.cs`, `VisualStateSet.cs`, `VisualStateSelector.cs`,
  `VisualStateRegistry.cs`, and `States.cs` under `src/SharpVision/Styling/`.
- Modify `ControlStyle`, snapshots, resolver, and `Control` state management.
- Delete `State.cs` and `VisualStates.cs` after migration.

**Tests:** expand state-model/resolver tests and the consumer custom control.

- [ ] Define deterministic registration validation: non-empty stable name,
      declaring control type, unique name per type, and explicit precedence.
- [ ] Register hovered, focused, checked, pressed, disabled, selected,
      indeterminate, read-only, and expanded standard keys.
- [ ] Add a protected control API that toggles a registered applicable key,
      invalidates state-specific geometry correctly, and publishes no change for
      an equivalent assignment.
- [ ] Store immutable sorted active snapshots; no mutable collection escapes.
- [ ] Match style selectors by subset scan, highest cardinality, registered
      precedence, then stable registration order. Do not generate every subset.
- [ ] Add a consumer-defined `busy` key and combined busy+focused style proof.
- [ ] Migrate every built-in style/test/theme recipe and remove the enum.
- [ ] Run all styling, control-state, rendering, and performance suites.

## Task 2: Add typed named parts and exact part styles

**Production:**

- Create `PartKey.cs`, `PartExposure.cs`, `PartRegistry.cs`, and part-style
  snapshot support under `src/SharpVision/Styling/`.
- Extend owned-edge metadata and `Theme`/`ThemeResolver`.

**Tests:** create `PartStyleTests.cs` and consumer named-part specimens.

- [ ] Register `PartKey<TOwner, TPart>` by validated stable name and exposure.
- [ ] Reject duplicate keys, wrong owner/target types, foreign controls, and
      duplicate part assignment before mutation.
- [ ] Add protected part registration for owned controls and drawn regions.
- [ ] Implement `Theme.SetPartStyle`/remove/query, old/new impact publication,
      cloning, freezing, and snapshots.
- [ ] Resolve exact part style after the ordinary type theme chain and before
      ancestor resources, instance style, and local values; resolve state inside
      the part layer.
- [ ] Prove themes cannot replace, bind, or rearrange a part.
- [ ] Migrate initial keys for container/editor bars, scrollbar regions, item
      hosts, combo popup/indicator/list, checkbox mark, button face/shadow, and
      window frame/title/shadow.
- [ ] Add exact-cell tests showing two same-type parts receive different styles
      without public exposure of their controls.

## Task 3: Separate explicit focus, tab stops, and pointer-state ownership

**Production:** modify `Control`, `FocusManager`, `CaptureManager`, and
`Pressable`.

- [ ] Add `IsTabStop` with default true and phase-none property semantics.
- [ ] Make traversal require both `CanFocus` and `IsTabStop`; explicit/pointer
      focus requires only `CanFocus`.
- [ ] Add a protected pointer-state ownership hook independent of focusability.
- [ ] Prove a focusable non-tab-stop can be explicitly/pointer focused but is
      skipped in both traversal directions.
- [ ] Prove a non-focusable semantic component may own hover without entering
      keyboard focus.
- [ ] Run focus, pointer, routing, pressable, popup, window, menu, and
      integration suites.

## Task 4: Add a framework-neutral semantic tree

**Production:** create one named type per file under
`src/SharpVision/Accessibility/`:

- `SemanticRole`;
- `SemanticState`;
- `SemanticAction`;
- `SemanticRange`;
- `SemanticsBuilder`;
- `SemanticsSnapshot`;
- `SemanticsNode`;
- `SemanticsTree`.

Modify `Control` and the owned registry for accessibility visibility and part
exposure.

**Tests:** create mirrored accessibility tests and consumer semantic specimens.

- [ ] Add validated `AccessibleName`, `AccessibleDescription`, and semantic
      visibility properties to `Control` with protected snapshot/action seams.
- [ ] Build an immutable deterministic tree from the owned registry, flattening
      or hiding parts according to `PartExposure` while retaining semantic
      controls.
- [ ] Make semantic action invocation dispatcher-affine and route through the
      same control behavior methods as keyboard/pointer/public APIs.
- [ ] Implement snapshots/actions for Button, CheckBox, RadioButton, Text,
      TextInput, List/ListItem, ComboBox, ScrollBar, Menu/MenuItem, Popup, and
      Window.
- [ ] Redact password text and borrowed paste/input buffers from snapshots.
- [ ] Test names, values, ranges, selected/checked/focused/expanded/disabled/
      read-only states, action parity, hidden decoration, flattened parts,
      disposal, and tree mutation.
- [ ] Add monochrome/default-color rendering checks proving semantic state is
      not communicated only by color.

## Task 5: Version and bound theme files

**Production:** modify `ThemeDefinition`, `ThemeFile`, `ThemeLoader`, embedded
theme JSON, catalog code, and package resources. Add a public immutable limits
value only if callers need configured user-file limits; otherwise use documented
fixed safe limits.

- [ ] Add schema `version: 1` to every embedded and documented theme.
- [ ] Reject missing/unsupported versions and unknown root/palette/role fields.
- [ ] Bound input bytes before retention, JSON depth, palette and role entries,
      key length, string/metadata length, and aggregate decoded content.
- [ ] Stream through bounded buffering; do not call unbounded `ReadToEnd` or
      `ReadAllText` for user files.
- [ ] Add boundary-minus-one/boundary/boundary-plus-one tests for every limit,
      non-seekable streams, malformed UTF-8/JSON, duplicate fields, unknown
      fields, and caller-owned stream lifetime.
- [ ] Preserve all curated theme attribution, role fallbacks, ordering, and
      exact colors.

## Task 6: Expand public third-party and package proof

- [ ] Extend `SharpVision.Consumer.Tests` with custom state, named-part,
      independent tab/pointer, semantics, and action specimens.
- [ ] Add a deterministic pack-and-consume script under `scripts/` that packs
      `SharpVision` into a temporary local feed, creates a temporary project,
      restores only from configured feeds, and builds the external specimens.
- [ ] Wire the script into the existing lint/test automation without leaving
      temporary output in the repository.
- [ ] Assert the packed assembly contains XML docs and no consumer/showcase
      friendship.

## Task 7: Showcase and normative documentation

- [ ] Expand the theming showcase with one custom state and two differently
      styled private named parts.
- [ ] Add a semantic inspector page driven by `SemanticsTree`, including
      password redaction and action invocation.
- [ ] Update styling, themes, theming-new-controls, focus, input, control,
      custom-components, lifecycle, project-structure, testing, and every
      affected control specification.
- [ ] Add `docs/concepts/accessibility.md` and link it from concept/control/test
      indexes.
- [ ] Document that the semantic tree is framework-neutral and does not claim a
      platform accessibility bridge.
- [ ] Run exact showcase screen/interaction tests and all four repository gates.
