# Component architecture v2: honest inheritance and protected composition

## Status

Approved direction from the 2026-07-15 architecture review. Implementation is
split across four executable plans:

1. component foundation and external extension contract;
2. intrinsic border/shadow migration and wrapper removal;
3. role-correct hierarchy and built-in control migration;
4. open styling, named parts, focus semantics, accessibility, and theme-file
   hardening.

This is a deliberate pre-1.0 source and binary break. The target API is kept
small and coherent instead of preserving misleading inheritance through obsolete
aliases.

## Problem

`Control` is documented as SharpVision's universal extensible element, but a
consumer outside the product assembly cannot implement a correct derived
control:

- ordinary property mutation, invalidation, child measure/arrange, current
  Unicode policy, focus, capture, and child clipping are internal or
  `private protected`;
- the test and showcase assemblies compile apparent custom controls only because
  `SharpVision` grants them friend access;
- `Container` is used for public multi-child layout, single content, private
  chrome, item realization, composition, automatic sizing, and scrolling;
- every `Container` subclass exposes public mutable `Children`, allowing callers
  to bypass `Popup.Content`, `Menu.Items`, `Table.Rows`, and `View.Build()`;
- `List`, `TextInput`, and `ComboBox` hide inherited scrolling members with
  `new`, so an upcast exposes a second independent scroll state;
- `View` mutates ownership during first measure, exposes its supposedly private
  root through inherited `Children`, and retries failed construction;
- ownership, focus traversal, style scopes, disposal, popup traversal, and
  navigation assume only a `Container` can own controls;
- the style system cannot express arrange-only impact, arbitrary third-party
  states, or a style for a named private part;
- there is no framework-neutral accessibility tree.

The result is inheritance that advertises capabilities the subtype does not mean
and hides capabilities a third party actually needs.

## Goals

- Make `Control` a truthful primitive extension point.
- Make every public base class correspond to one substitutable authoring role.
- Support reusable custom components through encapsulated retained composition.
- Keep true layout panels mutable and multi-child.
- Make all owned controls participate uniformly in lifecycle, dispatcher, theme,
  Unicode, focus, capture, rendering, hit testing, and disposal.
- Preserve intrinsic `Container.AutoSize` and `Container.AutoScroll` without
  forcing unrelated controls to inherit them.
- Make third-party styles, states, named parts, focus behavior, and semantics
  first-class and testable without friend access.
- Preserve deterministic layout, Unicode fidelity, bounded resource use, and
  exact terminal rendering.

## Non-goals

- No virtual tree, reconciliation, function component, hook state, binding
  engine, or render-time component construction.
- No WPF-style control-template engine. Themes cannot create, replace, bind, or
  rearrange controls.
- No public access to dispatcher internals, focus/capture managers, pending
  phase flags, or raw layout/render transaction methods.
- No fake common `IList<object>` on every item control. `List.Items`,
  `Menu.Items`, and `Table.Rows` retain domain-appropriate types.
- No claim of platform accessibility integration. The library first supplies a
  deterministic semantic tree that a later terminal or platform bridge can
  consume.

## Principles

### Inheritance represents substitutability

A type derives from `Container` only when callers may add arbitrary child
controls and the type lays them out as its public purpose. Owning one child,
private chrome, a popup, or realized item visuals is not a reason to be a
`Container`.

Concrete controls remain sealed. Third parties extend the small abstract role
bases or compose sealed controls.

### Composition is retained and explicit

A composite constructs its subtree once in its constructor and transfers one
root through `InitializeContent`. Layout never calls user construction code and
never changes ownership just because a first measure occurred.

### Orthogonal context follows every owned edge

There is one ownership engine in `Control`. A visual edge cannot opt out of
dispatcher affinity, theme context, Unicode policy, inherited enabled/visible
state, focus/capture cleanup, lifecycle, or disposal.

### Validation precedes mutation; structural publication follows commit

Structural operations validate every candidate before changing state. Guarded
availability cleanup is the one pre-commit exception: focus releases, capture
state clears before cancellation callbacks, and `OnUnavailable` observes the
complete old tree. Membership, parent, inherited context, and slot metadata then
commit before parent, theme, detach, attach, or slot notifications. A callback
exception may propagate after remaining cleanup, but observers always see either
the complete old tree during guarded availability cleanup or the complete new
tree during structural publication. Removal caused by disposal publishes one
disposal reason, never a second detached reason.

## Target hierarchy

```text
Control
├── Container
├── ContentControl
│   └── Pressable
├── CompositeControl
├── ItemsControl
└── primitive leaves

Screen : CompositeControl
```

### `Control`

`Control` owns the box model, styleable values, routed events, lifecycle,
layout/render template methods, protected extension kernel, and central visual
ownership registry. `Parent` is `Control?`, not `Container?`.

Border and shadow are intrinsic `Control` capabilities. The prerequisite
intrinsic-chrome plan makes `BorderThickness` reserve layout and removes the
redundant `Border` and `Shadow` wrapper types before role migration begins.

The public/protected kernel is:

```csharp
public enum ChangeImpact
{
    None,
    Render,
    Arrange,
    Measure,
}

[Flags]
public enum ResolvedAxes
{
    None = 0,
    Width = 1,
    Height = 2,
    Both = Width | Height,
}

protected Policy CellPolicy { get; }

protected bool SetProperty<T>(
    ref T field,
    T value,
    ChangeImpact impact,
    [CallerMemberName] string? propertyName = null);

protected void NotifyPropertyChanged(string propertyName, ChangeImpact impact);
protected void Invalidate(ChangeImpact impact);
protected void InvalidateVisualState();
protected Size MeasureChild(Control child, Constraint constraint);
protected void ArrangeChild(Control child, Rect slot, ResolvedAxes resolvedAxes = ResolvedAxes.None);
protected bool RequestFocus();
protected bool CapturePointer();
protected bool HasPointerCapture { get; }
protected void ReleasePointerCapture();
protected virtual bool ClipsChildren { get; }
protected virtual void OnAttached();
protected virtual void OnDetached();
protected virtual void OnDisposing();
protected virtual void OnPointerCaptureCancelled(ReleaseReason reason);
protected virtual void OnUnavailable(ReleaseReason reason);
protected virtual void OnParentChanged(Control? previous, Control? current);
```

`MeasureChild` and `ArrangeChild` accept only a direct owned child. They do not
expose an arbitrary-tree layout back door. The internal non-virtual
`Measure`/`Arrange`/`Render` transaction wrappers remain inaccessible.

`ChangeImpact` replaces the styling-only `Impact` enum. One public abstraction
describes the earliest affected UI phase for ordinary properties, style
properties, style events, and application invalidation. Its declaration order is
also its severity order.

### `Container`

`Container : Control` is the only general-purpose public multi-child owner.
`Children` is its public semantic collection. It retains intrinsic `AutoSize`,
`AutoSizeMode`, `AutoScroll`, ranges, offsets, scroll policy, and scrollbar
configuration.

Internal scrolling and scrollbar chrome are composed behind that public API.
They are registered private visual parts and are never members of public
`Children`.

Only these shipped controls remain `Container`s:

- `Stack`;
- `Grid`;
- `Dock`;
- `Overlay`;
- `Canvas`.

### `ContentControl`

`ContentControl : Control` owns zero or one publicly replaceable `Content`
control. It supplies default child measure/arrange, rendering, hit testing,
navigation, theme/context propagation, and disposal.

The following types migrate to it:

- `Window`;
- `Popup`;
- `Pressable`, and therefore `Button`, `CheckBox`, `RadioButton`, `MenuItem`,
  and `ComboBox`.

`Window.Child` and `Popup.Child` become `Content`. No inherited arbitrary
`Children` collection remains.

### `CompositeControl`

`CompositeControl : Control` owns exactly one private immutable composition
root. A concrete constructor calls:

```csharp
InitializeContent(root);
```

The method rejects null, repeated initialization, disposed/attached/already
owned content, and cycles before mutation. The protected `Content` getter lets
the derived component coordinate its own tree; callers cannot replace or remove
it.

The base supplies passthrough layout and normal child rendering/hit testing.
Components that need more than one visual child return a real layout container
as their root.

`Screen` derives from `CompositeControl`. `View` and `Build()` are removed after
the showcase and all screens migrate. Keeping an obsolete `View : Container`
would retain the very public-children leak this change removes.

### `ItemsControl`

`ItemsControl : Control` standardizes private item-presentation ownership, not
the public data collection type. A derived constructor initializes one private
`Container` presentation host. Protected APIs expose its realized controls and
validated insert/remove/replace operations. The base supplies passthrough
layout, rendering, hit testing, context propagation, and style-scope behavior.

`List`, `Menu`, and `Table` migrate to `ItemsControl` while retaining their
existing semantic collections:

- `List.Items : IReadOnlyList<object?>` plus `ItemTemplate`;
- `Menu.Items : MenuItems`;
- `Table.Rows : TableRows` and `Columns`.

`List` and `Menu` use private `Stack` hosts. `Table` uses an internal
`TablePresenter : Container`, moving table geometry out of the semantic control.
Callers cannot inject arbitrary realized controls.

### Primitive leaves

`Text`, `RichText`, `FigletText`, `ScrollBar`, and `TextInput` derive directly
from `Control`. `TextInput` owns its rails as private parts rather than
inheriting an unrelated public child collection and a second scroll model.

## Central owned-control registry

Every `Control` owns an internal ordered registry of visual slots. A slot
records:

- semantic role: container child, content, composition root, item visual, or
  named framework part;
- stable part key when applicable;
- render layer: normal or popup;
- whether it participates in hit testing and focus navigation;
- deterministic order within the role.

`Container.Children` is a public adapter over only the container-child slot.
`ContentControl.Content`, `CompositeControl.Content`, item hosts, and framework
parts use separate private slots over the same engine.

The registry is the sole source for:

- `Parent` assignment and cycle detection;
- attach/detach validation and dispatcher propagation;
- Unicode cell-policy and theme-context propagation;
- inherited enabled/visible state;
- focus and pointer-capture cleanup;
- routed ancestry;
- focus navigation;
- render and popup traversal;
- hit testing;
- disposal.

No derived control may replace lifecycle traversal with an internal override.
Controls customize role metadata and layout, not whether an owned child exists
for the rest of the framework.

## Structural transaction contract

For insert, remove, replace, clear, composite initialization, item realization,
and table-row batches:

1. Validate the owner, dispatcher access, capacity/index, every candidate
   subtree, duplicates, cycles, current ownership, disposal, and batch-wide
   conflicts without changing observable state.
2. Prepare the complete old/new edge set and inherited context.
3. Release invalid focus and capture while the old tree is still structurally
   coherent. Capture state clears before cancellation callbacks; guarded
   `OnUnavailable` runs afterward against that old tree. Capture the first
   callback failure without abandoning the transaction.
4. Commit collection membership, parents, inherited managers, theme, cell
   policy, dispatcher, and slot metadata without user callbacks.
5. Publish parent, theme, detach, attach, and slot notifications in that order
   from the committed tree, continuing after callback failures.
6. Request the earliest invalidation once, then rethrow the first captured
   callback failure.

Removed controls detach but are not disposed. Owner disposal disposes every
remaining descendant exactly once. A child disposed directly asks its owning
slot to remove it with `ReleaseReason.Disposed`; it does not re-enter the normal
detached path.

## Focus and pointer behavior

`CanFocus` means explicit and pointer focus eligibility. `IsTabStop`
independently controls traversal membership and defaults to true, preserving the
existing effect of setting `CanFocus = true`. Setting `CanFocus = false`
synchronously releases current focus.

Pointer-state ownership is a protected semantic behavior, not an accidental
synonym for focusability. Pressable composites own hover/press for their visual
content. Focus traversal walks the central owned registry, so content,
composites, item visuals, and eligible parts cannot disappear merely because
their owner is not a `Container`.

Protected focus/capture helpers keep manager implementations private. An
implicit capture cancellation calls `OnPointerCaptureCancelled` after manager
state is internally cleared.

## Styling and theming

### Immediate correctness

- `Theme.SetColor` accepts only concrete colors, increments `Version`, and
  raises one render-impact `Changed` event targeting `Control` when the value
  changes.
- Replacing a theme style publishes the maximum impact of the removed and new
  style.
- Style/local assignment of an equivalent value is a no-op.
- `Control.Style` invalidates the maximum old/new aggregate impact rather than
  always measuring.
- Style scope layers apply low-to-high so descendant type and instance styles
  win over ancestor resources as documented.
- State specificity resolves inside each cascade layer. A theme's focused value
  cannot override an instance style's normal value.
- `ChangeImpact.Arrange` maps to arrange plus render everywhere.

### Open state model

The closed `[Flags] State` enum is replaced by registered `VisualStateKey`
values and immutable `VisualStateSet` snapshots. SharpVision registers hovered,
focused, checked, pressed, disabled, selected, indeterminate, read-only, and
expanded. A third-party control registers stable type-scoped keys and updates
them through a protected state API.

A style selector is a set of required keys. Resolution scans stored selectors
whose keys are a subset of the active set and selects the most specific match,
then deterministic registered precedence. It never generates every subset of an
open state set.

### Named parts without templates

`PartKey<TOwner, TPart>` gives one stable, typed name to a control-owned part.
The key also declares semantic exposure: exposed node, flattened implementation
detail, or hidden decoration.

`Theme.SetPartStyle(key, style)` stores a validated style for that exact part.
Part style resolves after the ordinary type theme chain and before explicit
ancestor resources, instance style, and local values. Themes cannot replace or
rearrange the part.

Initial part keys cover:

- container and editor horizontal/vertical scrollbars;
- scrollbar decrement, track, thumb, and increment regions;
- list/menu/table item host;
- combo-box indicator, popup, and list;
- check-box mark;
- button face and shadow;
- window frame, title, and shadow.

Drawn regions that are not child controls resolve their part style through the
same key and owner state.

### Theme files

JSON theme files remain palette and semantic-role data only. Typed control and
part styles are code resources that clone and extend a theme.

Schema version 1 is required. The loader rejects unknown fields and enforces
document bytes, nesting depth, palette/role count, key length, and metadata
length before retaining content. Streams remain caller-owned.

## Accessibility semantics

SharpVision adds a framework-neutral semantic model:

- `SemanticRole`;
- composable `SemanticState` and `SemanticAction` flags;
- immutable `SemanticRange` and `SemanticsSnapshot` values;
- `AccessibleName`, `AccessibleDescription`, and accessibility visibility on
  `Control`;
- protected snapshot population and semantic-action invocation;
- a deterministic semantic tree built from the central owned registry and part
  exposure metadata.

Semantic actions execute on the dispatcher and enter the same activation,
selection, expansion, scrolling, and editing paths as keyboard, pointer, and
public APIs.

Built-ins cover buttons, check/radio controls, text, password-redacted text
input, list items and selection, combo-box value/expanded state, scrollbar
ranges, menus, popups, and windows. Checked, selected, focused, expanded, error,
and disabled state must remain distinguishable without color through glyphs,
attributes, or text.

## Concrete migration table

| Control                                                     | Current base | Target base        |
| ----------------------------------------------------------- | ------------ | ------------------ |
| `Stack`, `Grid`, `Dock`, `Overlay`, `Canvas`                | `Container`  | `Container`        |
| `Border`, `Shadow`                                          | `Container`  | removed            |
| `Window`, `Popup`                                           | `Container`  | `ContentControl`   |
| `Pressable`                                                 | `Container`  | `ContentControl`   |
| `Button`, `CheckBox`, `RadioButton`, `MenuItem`, `ComboBox` | `Pressable`  | `Pressable`        |
| `View`                                                      | `Container`  | removed            |
| `Screen`                                                    | `View`       | `CompositeControl` |
| `List`, `Menu`, `Table`                                     | `Container`  | `ItemsControl`     |
| `TextInput`                                                 | `Container`  | `Control`          |
| `Text`, `RichText`, `FigletText`, `ScrollBar`               | `Control`    | `Control`          |

`Inline`, `Run`, `LineBreak`, and `Hyperlink` retain their separate rich-text
model and are not part of the control inheritance tree.

## Third-party proof boundary

`tests/SharpVision.Consumer.Tests` references only the production project and is
never listed in `InternalsVisibleTo`. Foundation currently compiles the
Unicode-aware `Gauge` leaf, multi-child `FlowPanel`, capacity-one unclipped
`OverflowPanel`, and interactive lifecycle/focus/capture `InteractiveProbe`.

The role plan adds one-content, encapsulated-composite, item-host, and pressable
specimens only after those public bases exist. The orthogonal plan then adds
open-state, named-part, semantics, and semantic-action specimens. A reflection
test fails if the product assembly ever friends the consumer test assembly.

A later packaging gate packs `SharpVision`, creates a temporary project from the
package, and builds the completed specimens to catch package-shape errors that
the current project-reference proof cannot.

## Verification

Each migration slice follows test-first development and runs its focused
consumer/unit/integration suites. A phase is complete only after:

```bash
make format
make lint
make build
make test
```

The full mutable-tree randomized model is expanded to mix every owner role, part
layer, focus/capture state, theme mutation, disposal, reparenting, and callback
exception while asserting one parent, uniform inherited context, valid manager
targets, deterministic traversal, and no retained detached or disposed controls.
