# Control base API

## Control contract

`Control` is the abstract mutable UI element. It belongs to at most one parent
and, while attached, exactly one
[`Dispatcher`](../concepts/threading.md#threading-contract). Detached trees can
be assembled on any thread. Attached mutation and disposal must run on that
dispatcher.

## Core properties

| Property                                   | Default           | Contract                                                                                                                                     |
| ------------------------------------------ | ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `Width`, `Height`                          | `Length.Auto`     | Fixed, percentage, automatic, or proportional `Length`.                                                                                      |
| `MinWidth`, `MinHeight`                    | `0`               | Non-negative cell minimums.                                                                                                                  |
| `MaxWidth`, `MaxHeight`                    | `int.MaxValue`    | Cell maximums not below the corresponding minimum.                                                                                           |
| `Margin`                                   | Zero edges        | External non-negative `Thickness`.                                                                                                           |
| `BorderThickness`                          | Zero edges        | Physical zero-or-one-cell edges reserved inside the border box before padding; a `Measure`-impact style property.                            |
| `BorderGlyphs`                             | `Glyphs.Default`  | Validated printable one-cell glyph family used by standard chrome; render-only.                                                              |
| `Padding`                                  | Zero edges        | Internal non-negative `Thickness`.                                                                                                           |
| `HorizontalAlignment`, `VerticalAlignment` | `Left`, `Stretch` | Placement within the arranged slot.                                                                                                          |
| `Visibility`                               | `Visible`         | Visible, hidden, or collapsed.                                                                                                               |
| `IsEnabled`                                | `true`            | Inherited effective input state.                                                                                                             |
| `IsHitTestVisible`                         | `true`            | Whether pointer hit testing may target the control.                                                                                          |
| `CanFocus`, `TabIndex`                     | `false`, `0`      | Focus participation and deterministic order.                                                                                                 |
| `IsFocused`, `IsHovered`, `IsPressed`      | `false`           | Read-only committed interaction state; only interactive (focusable) controls are hovered, and composite hover belongs to its semantic owner. |
| `Style`                                    | `null`            | Optional per-instance `IControlStyle` overlay; null uses only the theme chain.                                                               |
| `DesiredSize`                              | Empty             | Read-only result of the last successful measure.                                                                                             |
| `Bounds`                                   | Empty             | Read-only committed arranged rectangle.                                                                                                      |

Setters validate before mutation, verify dispatcher access while attached, and
raise `PropertyChanged` once after the changed value is committed. Invalid
lengths, negative constraints, inconsistent min/max, invalid enum values, and
disposed access throw documented argument or object-lifetime exceptions. Changes
that hide, collapse, or disable a control additionally complete focus and
pointer-capture cleanup before `PropertyChanged`. Cleanup and property
publication are both attempted when either callback path fails, and the earliest
failure is rethrown after the state and manager transitions are complete.

`EffectiveIsEnabled` and `EffectiveIsVisible` are computed through the complete
ancestor chain. Changing an inherited state invalidates affected descendants.
`IsHitTestVisible` affects pointer targeting only; it does not suppress drawing,
visibility, enabled state, or explicit focus.

## Intrinsic chrome

Border, shadow, and body fill are properties of every `Control`; there are no
Border or Shadow wrapper controls. `BorderThickness` always participates in the
base box model. Visual chrome is automatic only for a render path that calls
`RenderChrome` or a specialized `ControlChrome` equivalent.

| Property group                                             | Base default                | Impact  | Validation and contract                                                                        |
| ---------------------------------------------------------- | --------------------------- | ------- | ---------------------------------------------------------------------------------------------- |
| `Background`, `FillMode`                                   | `null`, transparent         | Render  | A background resolved from any cascade layer or opaque fill mode makes the body own its cells. |
| `BorderThickness`                                          | Zero edges                  | Measure | Every physical edge is zero or one cell and is reserved before padding.                        |
| `BorderGlyphs`                                             | `Glyphs.Default`            | Render  | Every Rune is printable and one cell under the narrow policy; invalid default rejected.        |
| `BorderColor`, `BorderAttributes`                          | `null`, `null`              | Render  | Optional appearance overlays; attributes reject unknown flags or conflicts.                    |
| `HasShadow`, `ShadowMode`, `ShadowOffset`                  | `false`, composite, `(0,0)` | Render  | Mode is defined; offset is signed visual overflow and never reserves layout.                   |
| `ShadowGlyph`                                              | `▓`                         | Render  | Printable one-cell Rune; used only by block-glyph mode.                                        |
| `ShadowForeground`, `ShadowBackground`, `ShadowAttributes` | `null`, `null`, `null`      | Render  | Optional overlays; attributes reject unknown flags or conflicts.                               |

These are registered base defaults. Effective values resolve through class
defaults, style scopes, the active standard theme, instance style, visual state,
and local values. Standard themes resolve a body background, border color, and
shadow foreground from semantic roles, so their effective values differ from the
nullable base metadata without changing property defaults.

All validation occurs before observable mutation for local and theme values. At
render time, a glyph that becomes wide only under the active ambiguous-width
policy is repaired to a portable ASCII edge (`+`, `-`, or `|`) or block (`#`),
so chrome never writes half of a wide cell. Partial borders draw only enabled
edges; a corner glyph appears only when both adjoining edges are active.

Base `RenderChrome` draws shadow first, then clears the body when `FillMode` is
opaque or a background resolves from any cascade layer, then draws the border
last. Composite shadow restyles the translated cells it covers; block-glyph
shadow replaces those cells with `ShadowGlyph`. The body rectangle is excluded
from either shadow. `HasShadow = true` with the base `(0,0)` offset therefore
has no visible footprint. On the base render/layout path, shadow expands
`VisualBounds` for drawing but reserves no desired size, arranged bounds, child
space, or hit target. Button is intentionally different while pressed: it
translates its face and owned content by `ShadowOffset` without changing its
arranged border box.

These are base defaults, not universal control appearance. `Button` publishes a
one-cell rounded border, composite shadow, `(1,1)` offset, and dim shadow while
retaining zero padding. It invokes `ControlChrome` with specialized pressed-face
and shadow-gap options. `Window` keeps its bespoke one-cell titled frame, uses a
composite `(2,1)` dim shadow by default, and draws that frame/title/shadow
through its specialized renderer rather than base `RenderChrome`.

Sealed bespoke content renderers such as `Text`, `FigletText`, and `TextInput`
do not call base `RenderChrome`. Setting `BorderThickness` on them still
reserves the base layout inset but does not automatically paint a frame or
shadow. Wrap one in an ordinary chrome-rendering container such as `Dock` when
callers need a distinct visible frame or shadow.

Intrinsic chrome adds no ownership edge. Use an ordinary container such as
`Dock` when the chrome needs distinct bounds, margin, style, ancestry, or
lifetime. A custom `OnRender` that opts into intrinsic chrome calls
`RenderChrome` before custom content and draws that content through
`ContentBounds` without repeating border or padding deflation.

## Children and ownership

Every `Control` owns one central registry of distinct ordered visual slots.
`Control.Parent` is therefore `Control?`; ownership is not evidence that the
parent exposes a public child collection. Slots record structural role, render
layer, hit-test and navigation participation, an optional stable part key, and
the invalidation impact of a committed mutation.

`Container.Children` is the mutable public adapter over only its container-child
slot. Private scrollbar rails, item hosts, and other framework parts use
separate slots over the same registry and never leak into `Children`.
Registering two slots with the same role is valid; slot identity, not role,
determines membership and capacity.

The role vocabulary is container child, content, composition root, item visual,
item host, or framework part. The foundation instantiates container children,
item hosts, and private framework parts. The public
[`ContentControl`](content-control.md#contentcontrol-contract) instantiates the
capacity-one content role, while
[`CompositeControl`](composite-control.md#compositecontrol-contract)
instantiates the permanent capacity-one composition-root role. A slot also
selects normal or popup layer, hit-test and navigation participation, an
optional stable part key, and the earliest invalidation impact. These are
independent policies: excluding an edge from hit testing or navigation never
excludes it from parentage, inherited context, lifecycle, or disposal.

Cross-cutting traversal reads this registry directly instead of testing whether
an owner is a `Container`. Stable tree order is slot registration order followed
by item order. Focus navigation visits only navigation-participating edges in
that order; ordinary rendering visits normal-layer edges in that order; hit
testing visits hit-participating edges in reverse order. Popup-layer edges and
popup descendants are promoted above every ordinary sibling for both rendering
and hit testing and never paint once in the ordinary pass and again in the popup
pass. A `Popup` surface promotes itself while legacy owners still keep it in an
ordinary slot; dedicated popup slots remain the preferred ownership metadata.
Routed ancestry, inherited state, style scopes, lifecycle, focus/capture
cleanup, radio-group discovery, and disposal follow every edge regardless of its
interaction metadata.

`Add`, indexed insert and replacement, `Remove`, `Clear`, and complete-slot
replacement validate the whole proposed snapshot before changing ownership. A
control cannot have two parents, appear twice, be attached independently, or be
inserted beneath one of its own descendants. Batch failure preserves the old
order, parent links, inherited context, focus, and pointer capture.

Adding below an attached owner recursively attaches the subtree. Removing
recursively detaches it and clears its parent. Disposing an owner disposes all
owned descendants; repeated disposal is safe.

Structural removal has one deliberate exception to publication-after-commit.
While the old tree is still coherent, guarded availability cleanup releases
focus, clears capture before its cancellation hook, and then calls
`OnUnavailable`; those callbacks observe the old parent and inherited context,
but manager state is already clear. Root-manager disposal cleanup may follow
`OnUnavailable` before the transaction commits slot membership, parent links,
dispatcher, Unicode policy, theme, and manager context without callbacks. Parent
changes, theme changes, detach hooks, and attach hooks publish from the complete
new tree. The transaction then requests its slot impact exactly once before the
slot notification, so notification callbacks can consume current layout without
leaving a redundant pass. Callback failures are remembered while remaining
publication and cleanup continue; a `finally` path still requests invalidation
when an unexpected earlier failure bypasses normal publication, and the first
failure is rethrown from a coherent new tree. Tree mutation and disposal are
rejected while any affected ownership transaction is publishing.

When a root owns focus or capture managers, that ownership propagates through
every registered slot. Removal, inherited disable/hide, and disposal
synchronously release manager state before clearing parent or dispatcher
references. Direct child disposal removes through its exact owning slot with
`ReleaseReason.Disposed`, publishes the slot change, and never emits a second
detached reason. Owner disposal continues across all slots after a descendant
callback failure and disposes each remaining descendant once. The structural
publication guard spans `OnDisposing`, unavailable notification, exact-slot
unlink, and descendant cleanup; a disposal callback cannot switch to ordinary
collection removal to publish `Detached` or bypass the exact slot.

```csharp
container.Children.Add(control);
Debug.Assert(control.Parent == container);
```

## Invalidation

Dirty phases form a dependency closure: measure implies arrange and render,
arrange implies render, and render stands alone. Property setters request the
earliest affected phase and coalesce repeated requests while they bubble to the
root. Public style-property metadata expresses that phase as ordered
`ChangeImpact.None`, `Render`, `Arrange`, or `Measure` values.

| Change                                                              | Dirty phases                 |
| ------------------------------------------------------------------- | ---------------------------- |
| Width, height, min/max, margin, border thickness, padding, collapse | Measure, arrange, and render |
| Horizontal or vertical alignment                                    | Arrange and render           |
| Enabled state or visible/hidden transition                          | Render                       |
| Hit-test visibility                                                 | No layout or render phase    |
| Style replacement                                                   | Maximum old/new style impact |

The `Arrange` impact always requests arrange plus render, while `Measure`
requests all three phases. Assigning an equivalent value through `SetValue` is a
no-op. Replacing either `Control.Style` or a type style in `Theme` uses the
maximum aggregate impact of the removed and new styles, so removing geometric
values still invalidates their previous layout.

Third-party controls use the same phase vocabulary for ordinary CLR state. The
public setter validates its domain value before calling
`SetProperty(ref field, value, impact)`. The helper validates impact, property
name, dispatcher access, and lifetime before checking equivalence; a changed
value commits, invalidates, and then raises `PropertyChanged` once. A
coordinated mutation that has already committed all of its fields uses
`NotifyPropertyChanged(name, impact)`, which applies the same name, impact,
access, and lifetime validation. `Invalidate(impact)` validates the impact and
requests work without a property notification, while `InvalidateVisualState()`
clears resolved appearance caches and requests the strongest phase required by
active styles. A CLR property that changes `GetVisualState()` uses
`SetVisualStateProperty(ref field, value)`: it performs access and lifetime
validation before equivalence, commits the state, clears resolved caches,
calculates the dynamic aggregate style impact, and publishes exactly one
property notification. None of these seams exposes the framework's pending phase
flags.

## Lifecycle and events

Attachment commits the same dispatcher, Unicode policy, theme, and manager
context across the complete subtree before any lifecycle callback.
`OnAttached()` therefore sees every sibling fully attached. During application
startup it additionally sees the installed focus and capture managers and may
request either service immediately. Detachment clears complete subtree context
before any `OnDetached()` callback, so every sibling is already detached. Direct
`Attach` and `Detach` operations accept only an unowned root; owned controls
change lifecycle context exclusively through their registry edge, so a child can
never attach or detach independently from its parent. `OnDisposing()` runs at
most once before owned state is released; if that hook throws, base cleanup
completes before the original exception is rethrown. These hooks and
`OnParentChanged(Control?, Control?)` always observe committed ownership state.
`OnUnavailable` is the guarded pre-commit exception described under
[children and ownership](#children-and-ownership): manager state is clear, while
parent and inherited context still describe the coherent old tree.

Focus and pointer capture are synchronously released when a control becomes
unavailable or leaves the owned tree. Derived controls request those behaviors
through the target-safe helpers documented in
[Input routing](../concepts/input-routing.md#pointer-capture-and-coordinates).

`AddHandler<TArgs>` registers a typed synchronous handler and returns an
idempotent removal token. Routed arguments expose `OriginalSource`, retargetable
`Source`, route `Phase`, and `Handled`; preview and bubble use stable ancestry
and handler snapshots even when a callback mutates the tree. The standard
[`Events`](../concepts/input-routing.md#routed-event-api) cover key, text,
pointer, paste, and terminal focus payloads.

## Layout extension points

Derived controls implement `MeasureOverride(Constraint)` to report intrinsic
content size and `ArrangeOverride(Rect)` to receive their committed content box.
The base class owns margin, border, padding, explicit/deferred length
resolution, min/max clamping, alignment, caching, collapse behavior, dispatcher
checks, and reentrancy guards. The physical order is margin → border → padding →
content; combined measure insets saturate, and arrange deflation saturates at
zero. Extension points therefore deal only with content and must not repeat
box-model arithmetic.

A derived owner lays out only its direct children through
`MeasureChild(child, constraint)` and `ArrangeChild(child, slot, resolvedAxes)`.
Both reject null and non-direct children before entering the child's internal
transaction. `ArrangeChild` also rejects undefined `ResolvedAxes` flags;
`Width`, `Height`, or `Both` means the parent already resolved that border-box
dimension. Raw `Measure`, `Arrange`, `Render`, phase flags, and layout managers
remain internal.

If an extension point changes a layout property, that invalidation remains
pending for a later transaction. If it throws, the active phase is marked dirty
again before the exception escapes.

Building a retained component out of existing controls, rather than a new
primitive, does not use these seams directly; derive from
[`CompositeControl`](composite-control.md#compositecontrol-contract), construct
the private tree once, and transfer its root through `InitializeContent`.

`OnRender` receives a canvas clipped to the control's `VisualBounds`, so
deliberate own-content overflow such as shadow can extend beyond arranged
`Bounds` while remaining inside ancestor clips. Ordinary descendants render
through the owner's `Bounds` clip; intrinsic scrolling uses its committed
viewport clip. A container whose protected `ClipsChildren` override returns
false retains only the ancestor clip for descendants, which is the shared
mechanism behind documented Overlay and Canvas unclipped-child modes. Such an
owner also hit-tests eligible ordinary children outside its own `Bounds`, while
the owner itself remains a target only inside that box. Enabling intrinsic
`AutoScroll` restores viewport and owner-bounds clipping regardless of the
override.

## Styling extension point

Style properties, themes, and visual-state resolution are defined in
[Styling and visual states](../concepts/styling.md#styling-contract). This
control exposes its registered properties through `GetValue`, `SetValue`, and
`ClearValue`; each operation validates applicability before observable mutation.

`GetVisualState()` derives normal, hovered, focused, pressed, and disabled flags
from behavior. Controls with semantic selection override it to add checked
state. Only interactive (focusable) controls are ever marked hovered, so the
hovered flag never appears on static content such as text or tables.
`GetResolvedStyle` converts the active theme cascade into the complete terminal
cell style used by rendering.

The default `OnRender` calls `RenderChrome`, which draws configured body fill,
per-side border, and shadow. A control that fully overrides `OnRender` and wants
those intrinsic visuals must call `RenderChrome` itself before custom content.
Layout still reserves a non-zero `BorderThickness` even when a custom renderer
deliberately omits the visual, so rendering and reservation cannot silently
invent different geometry.

Primitive controls also read the protected inherited `CellPolicy` during
measurement and drawing. This keeps grapheme width decisions identical to the
application and frame without exposing policy mutation to derived code.

## Example

```csharp
control.Width = Length.Cells(14);
control.Margin = new Thickness(horizontal: 1, vertical: 0);
control.IsEnabled = true;

using var registration = control.AddHandler(
    Events.Key,
    (_, args) => args.Handled = true);
```

## Test obligations

Every concrete control tests validation-before-mutation, phase-specific
invalidation, dispatcher affinity, attach/detach ownership, visibility, enabled
inheritance, focus/capture cleanup, zero/tiny bounds, and final cells.
