# Custom components

## Overview

SharpVision keeps inheritance honest. Use
[`Container`](../controls/container.md#overview) only when callers may add
arbitrary controls and the new type's public purpose is laying them out. Use
`ContentControl` for zero-or-one caller-replaceable content,
`HeaderedContentControl` for content plus an independent replaceable header,
`CompositeControlBase` for a retained private composition, `ItemsControl` for a
typed semantic collection with a private presentation host, and direct
`ControlBase` inheritance for a new primitive leaf. `PressableBase` is the
focusable, single-text-caption interaction role.

There is no `View` type and no measure-time `Build()` composition. Construction
is never deferred to measure, arrange, or rendering: a component creates its
tree in its constructor and then hands over exactly one detached root with
`InitializeContent`. That root is immutable as an ownership edge, stays private
to the component, and participates in the normal dispatcher, theme, Unicode,
lifecycle, rendering, hit-testing, focus, capture, and disposal paths.

### Choosing a role

| Need                                                 | Base role                                        | Public ownership surface                       |
| ---------------------------------------------------- | ------------------------------------------------ | ---------------------------------------------- |
| New leaf behavior or custom drawing                  | `ControlBase`                                    | None unless the type explicitly provides one   |
| General-purpose multi-child layout                   | [`Container`](../controls/container.md#overview) | `Children`                                     |
| One caller-owned replaceable visual                  | `ContentControl`                                 | `Content`                                      |
| Content plus an independent replaceable header       | `HeaderedContentControl`                         | `Content`, `Header`, `HeaderText`              |
| Reusable component built from existing controls      | `CompositeControlBase`                           | None; its root is private                      |
| Typed data/semantic collection with realized visuals | `ItemsControl`                                   | The type's semantic collection, never the host |
| Focusable activating single text caption             | `PressableBase`                                  | `Text` (string)                                |

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

Border and shadow are intrinsic protected `ControlBase` properties, not wrapper
controls. A derived component may set the complete composites when its contract
owns that chrome, or leave them Theme-owned. Republish them only when arbitrary
caller-authored chrome is a supported layout feature; otherwise expose one
complete Style. A custom `OnRenderContent` draws through `ContentBounds`; the
framework-owned chrome is painted around it, so the override must not repeat
border or padding deflation. The public `ActualBorder` and `ActualShadow`
properties expose the resolved result.

Rendering runs `OnRenderContent` beneath a control's own children, in a fixed
order: content, then descendants, then `OnRenderAdornment`, then the framework
border and any internal overlay chrome. A component that needs to paint over its
own subtree — gridlines above cells, a focus ring around an active cell, a
splitter grip, a drag adorner — overrides `OnRenderAdornment` instead of
appending a synthetic last child to the public `Children` collection just to
paint above earlier siblings.

## Expected behavior

A custom component exposes only its public role: no leaked `Children` or private
parts, one-shot constructor-time initialization, and rejection of invalid
ownership candidates. Its first layout runs without mutating the tree, context
propagates into the private root, and the component participates normally in
layout, render, hit-test, and focus traversal. Direct disposal of the root and
disposal of the owner both behave as described above, and the component compiles
externally against the packed public API.
