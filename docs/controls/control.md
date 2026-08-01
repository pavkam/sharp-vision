# Control base API

## Control contract

`Control` is the abstract mutable UI element. It belongs to at most one parent
and, while attached, exactly one
[`Dispatcher`](../concepts/threading.md#threading-contract). Detached trees can
be assembled on any thread. Attached mutation and disposal must run on that
dispatcher.

## API

| Property                                                                            | Default                         | Contract                                                                   |
| ----------------------------------------------------------------------------------- | ------------------------------- | -------------------------------------------------------------------------- |
| `Width`, `Height`                                                                   | `Length.Auto`                   | Fixed, percentage, automatic, or proportional `Length`.                    |
| `MinWidth`, `MinHeight`                                                             | `0`                             | Non-negative cell minimums.                                                |
| `MaxWidth`, `MaxHeight`                                                             | `int.MaxValue`                  | Cell maximums not below the corresponding minimum.                         |
| `Margin`                                                                            | Zero edges                      | External non-negative `Thickness`.                                         |
| `ActualBorder`                                                                      | Theme role                      | Read-only fully resolved current border; raw authoring is protected.       |
| `Padding`                                                                           | Zero edges                      | Internal non-negative `Thickness`.                                         |
| `HorizontalAlignment`, `VerticalAlignment`                                          | `Left`, `Stretch`               | Placement within the arranged slot.                                        |
| `Visibility`                                                                        | `Visible`                       | Visible, hidden, or collapsed.                                             |
| `IsEnabled`                                                                         | `true`                          | Inherited effective input state.                                           |
| `IsHitTestVisible`                                                                  | `true`                          | Whether pointer hit testing may target the control.                        |
| `Focusable`, `CanFocus`, `TabStop`, `TabIndex`                                      | `false`, effective, `true`, `0` | Configured and effective focus/tab participation with deterministic order. |
| `UseMnemonic`                                                                       | `true`                          | Enables ampersand access-key syntax for the control caption.               |
| `IsFocused`, `ContainsFocus`, `IsPointerOver`, `IsPointerDirectlyOver`, `IsPressed` | `false`                         | Read-only committed interaction state.                                     |
| `DesiredSize`                                                                       | Empty                           | Read-only result of the last successful measure.                           |
| `Bounds`                                                                            | Empty                           | Read-only committed arranged rectangle.                                    |

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

## Intrinsic appearance

Every `Control` has intrinsic face, border, and shadow composites. There are no
border or shadow wrapper controls.

The shared
[intrinsic-chrome contract](../concepts/intrinsic-chrome.md#intrinsic-chrome-contract)
owns border/shadow value members, rendering order, geometry, clipping, and proof
obligations. This page defines how the base control exposes that behavior.

The public surface is:

| Property or method                           | Contract                                                                       |
| -------------------------------------------- | ------------------------------------------------------------------------------ |
| `Face`, `ResetFace()`                        | Public complete local face authoring and Theme-ownership reset.                |
| `Border`, `Shadow`, their reset methods      | Protected derived-control chrome authoring.                                    |
| `SetAppearance(state, set)`                  | Protected derived-control partial state authoring.                             |
| `ActualFace`, `ActualBorder`, `ActualShadow` | Public read-only, fully composed current values with concrete terminal values. |
| `AppearanceBoundary`                         | Stops ambient face inheritance for descendants.                                |

The resolver applies semantic normal, semantic active states, complete local
values, and local state sets in that order. A developer assignment therefore
survives theme replacement. State sets can still vary any individual member,
including border sides, glyph style, colors, or shadow geometry. The
[appearance contract](../concepts/styling.md#visual-states) is normative for
composition order.

`Face` owns foreground, background, terminal attributes, underline style, and
underline color. `Border` owns `BorderSide`, `BorderGlyphStyle`, foreground,
background, and terminal attributes. `Shadow` owns visibility, `ShadowMode`,
offset, glyph, foreground, background, and terminal attributes. Their matching
`*Set` records contain optional members for partial state contributions.

All complete and partial values validate before mutation. Transparent is valid
for background composition; glyph-painting foreground and underline channels
reject it. Border glyphs and block-shadow glyphs must be printable one-cell
runes. Partial borders draw only enabled edges, and a corner appears only when
both adjoining edges are enabled. Every enabled edge reserves one cell before
padding. A state change that alters border sides invalidates measure; other
appearance changes invalidate rendering.

The render pipeline draws shadow, body fill, content and normal-layer children,
then the border or specialized frame overlay. Border cells use the border's own
background; changing the face background on hover does not alter border-cell
background unless the state also supplies `BorderSet.Background`.

`ShadowMode.Composite` restyles translated destination cells, `BlockGlyph`
replaces them with the configured glyph, and `FractionalBlock` uses code-owned
`▄`, `▀`, and `█` cells. X offsets are whole columns; fractional Y offsets are
half rows. The body is excluded from its own shadow. Shadow overflow expands
`VisualBounds` but does not reserve desired size, child space, scrolling extent,
or hit targets. Unrepresentable translated bounds keep the body drawable and
clip unreachable shadow cells.

Button may translate its face while a composite or block shadow is pressed;
fractional shadow is suppressed because a complete face cannot move by half a
terminal row. Window and Popup keep specialized titled or transient frame
rendering while consuming their resolved composites.

Intrinsic appearance adds no ownership edge. Use an ordinary container when
chrome needs distinct bounds, margin, ancestry, or lifetime. A custom
`OnRenderContent` draws through `ContentBounds`; framework-owned chrome already
accounts for border and padding.

## Children and ownership

Every `Control` owns one central registry of distinct ordered visual slots.
`Control.Parent` is therefore `Control?`; ownership is not evidence that the
parent exposes a public child collection. Slots record structural role, render
layer, hit-test and navigation participation, an optional stable part key, and
the invalidation impact of a committed mutation.

[`Container.Children`](container.md#children-and-ownership) is a
`ControlCollection`, the mutable public adapter over only its container-child
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

## Invalidation API

The shared
[invalidation contract](../concepts/invalidation.md#invalidation-contract) owns
phase dependencies, ancestor propagation, dispatcher scheduling, retries, and
frame coordination. `Control` exposes the authoring seams that let derived
controls participate without exposing pending phase flags.

| Seam                                       | Use                                                             |
| ------------------------------------------ | --------------------------------------------------------------- |
| `SetProperty(ref field, value, impact)`    | Commit one ordinary CLR property and raise `PropertyChanged`.   |
| `NotifyPropertyChanged(name, impact)`      | Publish a coordinated mutation after all related fields commit. |
| `Invalidate(InvalidationImpact)`           | Request phase work without a property notification.             |
| `InvalidateVisualState()`                  | Clear resolved appearance caches after semantic state changes.  |
| `SetVisualStateProperty(ref field, value)` | Commit a property that changes `GetAppearanceState()`.          |

Each seam validates dispatcher access, lifetime, arguments, and the selected
impact before changing observable state. Equivalent assignments are no-ops. A
real property change commits and requests work before notifying observers, so
callbacks see both the new value and its pending update. Specialized visual
state changes calculate their strongest impact from the active style rather than
assuming that every state transition is render-only.

## Access-key extension points

A derived captioned control overrides `AccessKeyText` with its borrowed action,
header, label, or title string. `OnAccessKey(Rune)` runs only after the
application matches that caption and revalidates current availability. Its
default focuses this control, the first eligible descendant of a scope, or the
next tab stop for a label-like leaf. Action controls override the method and
reuse their ordinary keyboard state machine. Returning false permits the next
duplicate candidate.

`UseMnemonic` controls both marker rendering and discovery. Changing it
invalidates measure for the caption subtree. Rich/body `Text` specializes its
default to false; a `Text` used as a `Pressable` caption inherits the owner's
effective setting. The full syntax, modifier, duplicate, modality, and paired
text rules live in the shared
[access-key contract](../concepts/access-keys.md#access-key-contract).

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
the private tree once, and transfer its root through `InitializeContent`. Derive
from [`Container`](container.md#container-contract) only when callers own an
arbitrary public child collection and the concrete type supplies both layout
passes.

`OnRenderContent` receives a canvas clipped to the control's resolved
`VisualBounds`. Rendering carries two constraints down each normal-layer branch:
a hard canvas from the frame, explicit clip, or scroll viewport, and a soft
content aperture accumulated from ordinary arranged bounds. A control may expand
only its own soft aperture where its `VisualBounds` exceed `Bounds`. That
expansion follows arbitrary ordinary nesting without being shared with a
sibling, changing layout, or enlarging hit testing.

`Overlay.ClipToBounds = true`, the caller's canvas, and an armed container's
committed scroll viewport are hard boundaries that contain descendant shadows.
When `ClipToBounds` is false, Overlay retains its inherited hard canvas and soft
ancestor aperture. A container whose protected `ClipsChildren` override returns
false has the same soft-clip behavior. Such an owner also hit-tests eligible
ordinary children outside its own `Bounds`, while the owner itself remains a
target only inside that box. Popup-layer roots restart from the root frame
canvas during their elevated pass; ordinary-owner clips neither truncate nor
admit them into the normal pass.

## Appearance extension point

Controls expose validated CLR properties for face and layout configuration.
Specialized presentation is a nullable complete Style plus an always-present
`ActualStyle`. Null uses the inherited immutable Theme; local Style wins over
Theme replacement. Raw border, shadow, and state authoring is protected unless a
chrome-host control intentionally republishes it.

Appearance is local and render-only. Derived controls may use protected
SetAppearance for one VisualState. The resolver applies the fixed state order
PointerOver, FocusWithin, Focused, Current, Selected, Checked, Indeterminate,
Pressed, then Disabled. Text-only ambient values can flow through normal
parentage; background, border, shadow, and visual states never cascade. Callers
may assign `FocusWithin` explicitly when a composite intentionally needs
descendant-focus emphasis.

Pointer membership and hover appearance are separate contracts. Every control in
the physical hit ancestry exposes `PointerOver`. The active theme and local
appearance overlays determine whether that state changes any channel.

GetAppearanceState derives the local flags from physical pointer membership,
focus, availability, and a control's explicit pressed, current, selection,
checked, or indeterminate facts. A derived control uses SetVisualStateProperty
when a CLR state property changes one of those facts. Pressed defaults to the
interaction state the framework's own gesture behaviors track; a control with
its own press concept - continuous drag tracking rather than one-shot
activation, for example - overrides the protected `IsPressedState` seam
directly, the same pattern `IsCheckedState`, `IsSelectedState`,
`IsCurrentState`, and `IsIndeterminateState` already use for their own facts.

Intrinsic body, border, and shadow rendering is framework-owned. Custom
OnRenderContent implementations draw semantic content with ResolvedStyle; they
do not emit escape bytes or manually invoke a chrome helper.

## Example

![The Control control rendered in the live showcase](../images/controls/control.png)

```csharp
control.Width = Length.Cells(14);
control.Margin = new Thickness(horizontal: 1, vertical: 0);
control.IsEnabled = true;

using var registration = control.AddHandler(
    Events.Key,
    (_, args) => args.Handled = true);
```

## Expected behavior

Every concrete control tests validation-before-mutation, phase-specific
invalidation, dispatcher affinity, attach/detach ownership, visibility, enabled
inheritance, focus/capture cleanup, zero/tiny bounds, and final cells.
