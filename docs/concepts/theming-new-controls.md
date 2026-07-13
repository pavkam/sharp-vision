# Theming a new control

This guide is for authors building a control **outside** the SharpVision
assembly (a NuGet consumer) who want it to participate in theming exactly like
the built-in controls. Every API named here is part of the public surface — no
`internal` helpers are required.

## 1. Declare style properties

Register each themeable property once, in a static initializer on your control
type:

```csharp
public sealed class Gauge : Control
{
    public static StyleProperty<Color?> FillColorProperty { get; } =
        StyleProperty<Color?>.Register<Gauge>("fill-color", null, Impact.Render);

    // Optional: give the CLR name explicitly when it differs from the serialized name.
    public static StyleProperty<int> SegmentsProperty { get; } =
        StyleProperty<int>.Register<Gauge>("segments", 10, Impact.Measure, clrName: nameof(Segments));

    public Color? FillColor
    {
        get => GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    public int Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }
}
```

- `Impact.Render` re-renders on change; `Impact.Measure` also re-runs layout.
- `GetValue`/`SetValue`/`ClearValue` are public: they resolve through the full
  cascade (local value → ancestor style scopes → per-instance style → theme →
  class default → registered default) and honor visual states.
- Change notifications (`INotifyPropertyChanged.PropertyChanged`) report the
  `clrName` (defaulting to the PascalCase form of the serialized name, e.g.
  `"fill-color"` → `FillColor`).

## 2. Per-type defaults

Structural defaults that should apply to every instance of your type
(independent of theme) use class defaults:

```csharp
static Gauge() => _ = SegmentsProperty.RegisterClassDefault<Gauge>(20);
```

Class defaults are ranked **below** the theme, so a theme can still override
them with `theme.SetStyle(new ControlStyle<Gauge> { ... })`. The most-derived
class default wins.

## 3. Render with the resolved style

Obtain the composed terminal style for the current visual state and, optionally,
draw the shared border/shadow/fill chrome:

```csharp
protected override void OnRender(TerminalCanvas canvas)
{
    RenderChrome(canvas);            // optional shared border/shadow/fill
    var style = ResolvedStyle;       // composed fg/bg/attributes for the current visual state
    // ...draw content with `style`...
}
```

`ResolvedStyle`, `GetResolvedStyle(State)`, and `RenderChrome` are `protected`.

## 4. React to visual states

Override the boolean hooks rather than `GetVisualState`:

```csharp
protected override bool IsCheckedState => _isChecked;         // drives State.Checked
protected override bool IsSelectedState => _isSelected;       // drives State.Selected
protected override bool IsIndeterminateState => _isMixed;     // drives State.Indeterminate
```

Base `GetVisualState` combines these with `Hovered`, `Focused`, `Pressed`, and
`Disabled`. A style may target `State.Normal`, any single overlay, or a
**combination** (for example `State.Hovered | State.Focused`); a more specific
combination wins over single-flag definitions.

## 5. Cascade style to descendants

A container whose style should flow to its logical children implements
`IStyleScope`:

```csharp
public sealed class Tree : Container, IStyleScope { }
```

Every descendant then inherits the tree's themed and per-instance style values,
with the nearest scope winning and a descendant's own values winning over any
scope. This is the same mechanism the built-in list uses; it is not restricted
to a specific control type.

## 6. Read semantic theme colors

To track a theme's accent/surface/border color across theme swaps instead of
hardcoding palette values:

```csharp
if (TryGetThemeColor(ColorRole.Accent, out var accent))
{
    // ...use `accent`...
}
```

## 7. Tooling / design time

- `StylePropertyRegistry.GetProperties(typeof(Gauge))` lists every property a
  type participates in (its own and inherited), forcing registration if needed.
- `StylePropertyRegistry.FindProperty(typeof(Gauge), "fill-color")` looks one up
  by serialized name.
- `ThemeResolver.Resolve(theme, typeof(Gauge), FillColorProperty, State.Normal)`
  evaluates a value for a type under a theme **without** a live control
  instance.
