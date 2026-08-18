# Custom components

## Overview

SharpVision keeps inheritance honest. Use
[`Container`](../controls/container.md#overview) only when callers may add
arbitrary controls and the new type's public purpose is laying them out. Use
`ContentControl` for zero-or-one caller-replaceable content,
`HeaderedContentControl` for content plus an independent replaceable header,
`CompositeControlBase` for a retained private composition, `ItemsControl` for a
typed semantic collection with a private presentation host, and direct
`ControlBase` inheritance for a new primitive leaf.
[`InputBase`](../controls/input-base.md#overview) is the focusable role for a
value editor or popup-backed input: it exposes press activation, a single text
caption, an optional command, segment editing, step-key translation, the shared
drop-down glyph, and an owned popup as independent `Enable*` capabilities, so a
control opts into exactly the ones it needs instead of inheriting all of them -
`Button` calls `EnablePressActivation`, `EnableCaption`, and `EnableCommand`
together, while `ComboBox` calls only `EnablePressActivation`.

There is no `View` type and no measure-time `Build()` composition. Construction
is never deferred to measure, arrange, or rendering: a component creates its
tree in its constructor and then hands over exactly one detached root with
`InitializeContent`. That root is immutable as an ownership edge, stays private
to the component, and participates in the normal dispatcher, theme, Unicode,
lifecycle, rendering, hit-testing, focus, capture, and disposal paths.

### Choosing a role

| Need                                                                                                                      | Base role                                         | Public ownership surface                                                         |
| ------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- | -------------------------------------------------------------------------------- |
| New leaf behavior or custom drawing                                                                                       | `ControlBase`                                     | None unless the type explicitly provides one                                     |
| General-purpose multi-child layout                                                                                        | [`Container`](../controls/container.md#overview)  | `Children`                                                                       |
| One caller-owned replaceable visual                                                                                       | `ContentControl`                                  | `Content`                                                                        |
| Content plus an independent replaceable header                                                                            | `HeaderedContentControl`                          | `Content`, `Header`, `HeaderText`                                                |
| Reusable component built from existing controls                                                                           | `CompositeControlBase`                            | None; its root is private                                                        |
| Typed data/semantic collection with realized visuals                                                                      | `ItemsControl`                                    | The type's semantic collection, never the host                                   |
| IsFocusable value editor, popup-backed input, or activating single text caption, opting into only the needed capabilities | [`InputBase`](../controls/input-base.md#overview) | Whatever the concrete control needs; `EnableCaption` alone gives `Text` (string) |

Concrete shipped controls are sealed, with three documented exceptions: `Popup`,
`ContextMenu`, and `Window`. Each stays unsealed only because the library itself
subclasses it internally — `Flyout`/`Tooltip : Popup`,
`TextInputContextMenu : ContextMenu`, and `Dialog<TResult> : Window` (every
concrete dialog type derives from `Dialog<TResult>` in turn, per the
[dialog catalog](../dialogs/index.md#dialog-catalog)). `Popup` and `Window`
expose substantial protected seams for that purpose; `ContextMenu`'s non-public
surface is a single `internal Menu` field plus one protected override, so
deriving from it directly gains little over composing one. Third parties derive
from the abstract roles, compose sealed controls, or subclass one of these three
documented exceptions; they do not depend on internal ownership, layout, focus,
capture, or renderer transactions beyond what each type's own protected members
expose.

## Retained private composition

Call `InitializeContent` once, from the concrete `CompositeControlBase`
constructor. The supplied root must be non-null, detached, available, and
outside the component's own ancestry. A rejected candidate leaves initialization
still available; once ownership commits, neither direct disposal of the root nor
a callback failure ever makes the component reinitializable. Layout is a
pass-through over the root and never constructs or mutates the tree.

Use a real layout container as the root when the composition needs more than one
visual child. Application-dependent work belongs in lifecycle hooks such as
`OnAttached` and `OnStarted`, not in composition construction.

```csharp
public sealed class LoginPanel : CompositeControlBase
{
    public LoginPanel()
    {
        var root = new Stack
        {
            Spacing = 1,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                SemanticColor.ControlBorder,
                SemanticColor.Surface,
                SemanticDecoration.Border),
            Padding = new Thickness(1),
            Shadow = new Shadow(
                true,
                ShadowMode.Composite,
                new Point(1, 1),
                new Rune('▓'),
                SemanticColor.ControlShadow,
                Color.Transparent,
                SemanticDecoration.Shadow),
        };
        root.Children.Add(new Text("Sign in"));
        root.Children.Add(new TextInput());
        root.Children.Add(new Button { Text = "Go" });
        InitializeContent(root);
    }
}
```

## Semantic item presentation

An `ItemsControl` constructor calls `InitializeItemsHost` once, passing a
private `Container`. The derived type exposes its own typed collection and
realizes controls through the protected inspection and insert/remove/replace
helpers. The host's `Children` collection never becomes public API. `ListView`,
`Menu`, and `Table` follow this pattern; `Table` keeps its scrolling cell
presenter private and exposes only `Rows`, `Columns`, and delegated scroll
state.

## Chrome and custom rendering

`Border` and `Shadow` are declared once on `ControlBase` and are already public
on every control; there is nothing to republish. Reading or writing either
throws `InvalidOperationException` until the owning control calls the protected
`EnableChromeAuthoring()`, typically once from its own constructor - the same
one-shot-capability idiom [`InputBase`](../controls/input-base.md#overview) uses
for its own `Enable*` methods. A derived component calls it when arbitrary
caller-authored chrome is a supported layout feature; otherwise it leaves
authoring disabled and exposes one complete Style instead.

```csharp
public sealed class Card : ContentControl
{
    public Card() => EnableChromeAuthoring();
}

// var card = new Card { Border = new Border(/* ... */) };
```

A custom `OnRenderContent` draws through `ContentBounds`; the framework-owned
chrome is painted around it, so the override must not repeat border or padding
deflation. The public `ActualBorder` and `ActualShadow` properties expose the
resolved result whether or not chrome authoring is enabled.

Rendering runs `OnRenderContent` beneath a control's own children, in a fixed
order: content, then descendants, then `OnRenderAdornment`, then the framework
border and any internal overlay chrome. A component that needs to paint over its
own subtree — gridlines above cells, a focus ring around an active cell, a
splitter grip, a drag adorner — overrides `OnRenderAdornment` instead of
appending a synthetic last child to the public `Children` collection just to
paint above earlier siblings.

## Affix support

`ControlBase` declares four protected members that let a derived control host
`StartAffix`/`EndAffix` properties without reimplementing the reserved-column
layout every affix-hosting control shares - `Button`, `ComboBox`, `NumberInput`,
`MenuItem`, and `TextInput` all wire the same seam. There is no base-class
`StartAffix`/`EndAffix` property to inherit; a control declares its own typed
`Affix?` properties and calls into the seam explicitly, the same way it opts
into chrome authoring above.

- `AffixMetrics MeasureAffixes(Affix? start, Affix? end, int gap)` resolves the
  reserved leading and trailing cell columns for a possibly-null affix pair,
  given the hosting style's own gap - typically `InputStyle.AffixGap` or an
  equivalent control-owned constant. A null affix costs nothing.
- `static Rect DeflateForAffixes(Rect contentBox, AffixMetrics metrics)` shrinks
  an already-known content box by the reserved columns, leaving the middle box a
  caption arranges into.
- `RenderAffixes` draws the affixes into the undeflated content box, live
  against current bounds. Overflow is decided here rather than from the
  measure-time metrics: the end affix drops whole before the start affix does,
  and the caption itself already shrinks first because `DeflateForAffixes`
  saturates its middle box at zero width instead of going negative:

  ```csharp
  protected void RenderAffixes(
      TerminalCanvas canvas,
      Rect contentBox,
      AffixMetrics metrics,
      Affix? start,
      Affix? end,
      TerminalStyle style)
  {
  }
  ```

- `InvalidationImpact GetAffixChangeImpact(Affix? previous, Affix? current)`
  grades a property setter's own invalidation: null-to-set or set-to-null
  changes the reserved width and requires `Measure`; a same-width content or
  color swap requires only `Render`.

A typical `StartAffix` property, following `TextInput`'s own shape:

```csharp
public Affix? StartAffix
{
    get;
    set
    {
        var impact = GetAffixChangeImpact(field, value);

        if (SetProperty(ref field, value, impact))
        {
            ArrangeChrome();
        }
    }
}
```

> [!NOTE]
>
> `MeasureOverride` must fold the same `MeasureAffixes(...)` reservation into
> its returned `Size` that `DeflateForAffixes` later removes at arrange time. An
> auto-sized control that skips this under-measures by exactly the affix columns
> it promises, and arrange then deflates an already-too-narrow content box down
> toward zero - starving whatever viewport sits inside it. Fold the reservation
> into every measured path a control has, including a `WordWrap` or similar
> reflow branch that computes its own width independently of the plain-text
> path.

Render against the undeflated content box, not a cached deflated one:
`RenderAffixes` decides overflow live, against current bounds, so a
possibly-stale measure-time reservation never strands an affix that would still
fit.

## Expected behavior

A custom component exposes only its public role: no leaked `Children` or private
parts, one-shot constructor-time initialization, and rejection of invalid
ownership candidates. Its first layout runs without mutating the tree, context
propagates into the private root, and the component participates normally in
layout, render, hit-test, and focus traversal. Direct disposal of the root and
disposal of the owner both behave as described above, and the component compiles
externally against the packed public API.
