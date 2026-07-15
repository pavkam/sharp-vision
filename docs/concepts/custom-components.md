# Custom components

## Custom components contract

SharpVision keeps inheritance honest. Use `Container` only when callers may add
arbitrary controls and the new type's public purpose is laying them out. Use
`ContentControl` for zero-or-one caller-replaceable content, `CompositeControl`
for a retained private composition, `ItemsControl` for a typed semantic
collection with a private presentation host, and direct `Control` inheritance
for a new primitive leaf. `Pressable` is the focusable, single-content
interaction role.

`View` and measure-time `Build()` composition do not exist. Construction is
never deferred to measure, arrange, or rendering: a component creates its tree
in its constructor, then transfers exactly one detached root with
`InitializeContent`. The root is immutable as an ownership edge, remains private
to the component, and participates in the normal dispatcher, theme, Unicode,
lifecycle, rendering, hit-testing, focus, capture, and disposal paths.

### Choosing a role

| Need                                                 | Base role          | Public ownership surface                       |
| ---------------------------------------------------- | ------------------ | ---------------------------------------------- |
| New leaf behavior or custom drawing                  | `Control`          | None unless the type explicitly provides one   |
| General-purpose multi-child layout                   | `Container`        | `Children`                                     |
| One caller-owned replaceable visual                  | `ContentControl`   | `Content`                                      |
| Reusable component built from existing controls      | `CompositeControl` | None; its root is private                      |
| Typed data/semantic collection with realized visuals | `ItemsControl`     | The type's semantic collection, never the host |
| Focusable activating single face                     | `Pressable`        | Inherited `Content`                            |

Concrete shipped controls are sealed. Third parties derive from these abstract
roles or compose sealed controls; they do not depend on internal ownership,
layout, focus, capture, or renderer transactions.

## Retained private composition

Call `InitializeContent` once from the concrete `CompositeControl` constructor.
The supplied root must be non-null, detached, available, and outside the
component's own ancestry. Rejected candidates leave initialization available;
once ownership commits, direct disposal of the root or a callback failure never
makes the component reinitializable. Layout is a pass-through over the root and
does not construct or mutate the tree.

Use a real layout container as the root when more than one visual child is
needed. Application-dependent work belongs in lifecycle hooks such as
`OnAttached` and `OnStarted`, not in composition construction.

```csharp
public sealed class LoginPanel : CompositeControl
{
    public LoginPanel()
    {
        var root = new Stack
        {
            Spacing = 1,
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Padding = new Thickness(1),
            HasShadow = true,
            ShadowOffset = new Point(1, 1),
        };
        root.Children.Add(new Text("Sign in"));
        root.Children.Add(new TextInput());
        root.Children.Add(new Button { Content = new Text("Go") });
        InitializeContent(root);
    }
}
```

## Semantic item presentation

An `ItemsControl` constructor calls `InitializeItemsHost` once with a private
`Container`. The derived type exposes its own typed collection and realizes
controls through the protected inspection and insert/remove/replace helpers. The
host's `Children` collection never becomes public API. `List`, `Menu`, and
`Table` follow this pattern; `Table` keeps its scrolling cell presenter private
while exposing only `Rows`, `Columns`, and delegated scroll state.

## Chrome and custom rendering

Border and shadow are intrinsic `Control` properties, not wrapper controls. Set
`BorderThickness`, `BorderGlyphs`, `HasShadow`, and related style properties on
the component that owns the chrome. A custom `OnRender` that opts into intrinsic
chrome calls `RenderChrome` before drawing through `ContentBounds`; it must not
repeat border or padding deflation.

## Tests

Prove the public role, absence of leaked `Children` or private parts,
constructor-time one-shot initialization, rejected ownership candidates, first
layout without tree mutation, context propagation, layout/render/hit/focus
traversal, direct-root disposal, owner disposal, and external compilation
against the packed public API.
