# Theming a new control

This guide is for authors building a control **outside** the SharpVision
assembly who want it to participate in theming exactly like the built-in
controls. Every API named here is part of the public surface — no `internal`
helpers are required. The foundation proves that surface from an unfriended
project reference; a later package gate separately proves the packed NuGet
shape.

## 1. Declare style properties

Register each themeable property once, in a static initializer on your control
type:

```csharp
public sealed class Gauge : Control
{
    public static StyleProperty<Color?> FillColorProperty { get; } =
        StyleProperty<Color?>.Register<Gauge>("fill-color", null, ChangeImpact.Render);

    // Optional: give the CLR name explicitly when it differs from the serialized name.
    public static StyleProperty<int> SegmentsProperty { get; } =
        StyleProperty<int>.Register<Gauge>(
            "segments",
            10,
            ChangeImpact.Measure,
            clrName: nameof(Segments));

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

- `ChangeImpact.None` changes no UI phase, `Render` regenerates cells, `Arrange`
  recalculates bounds and cells, and `Measure` recalculates the full layout and
  cells. The values are ordered so an aggregate uses the strongest impact.
- `GetValue`/`SetValue`/`ClearValue` are public: they resolve through the full
  cascade (registered and class defaults → far-to-near scope theme chains →
  descendant theme chain → far-to-near scope instance styles → descendant
  instance style → local value) and honor visual states.
- Reassigning an equivalent local value is a no-op: it does not invalidate or
  raise `PropertyChanged`.
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
combination wins over single-flag definitions inside that style layer. The next
higher cascade layer then overrides it even with a `State.Normal` value.

## 5. Cascade style to descendants

A container whose style should flow to its logical children implements
`IStyleScope`:

```csharp
public sealed class Tree : Container, IStyleScope { }
```

Every descendant then uses the tree's theme chain and per-instance style as
lower-priority resources. Scope resources apply farthest to nearest, followed by
the descendant's theme chain and own instance style, so the descendant always
wins over an ancestor scope. This is the same mechanism the built-in list uses;
it is not restricted to a specific control type.

## 6. Use semantic theme colors

To track a theme's accent/surface/border color across theme swaps instead of
hardcoding palette values, assign a `ThemeColors.*` value to any color-typed
style property — a class default, a theme style, or a local value — exactly like
any other `Color`:

```csharp
public sealed class Gauge : Control
{
    static Gauge() => _ = FillColorProperty.RegisterClassDefault<Gauge>(ThemeColors.Accent);
}

var gauge = new Gauge { FillColor = ThemeColors.Accent };
```

`ThemeColors.Accent` (and its eleven siblings) is a deferred color: it resolves
to the active theme's concrete palette color during property resolution, so
`gauge.FillColor` continues to track theme swaps with no query API and no custom
control needed. `ColorRole` has twelve members — `Foreground`, `Background`,
`Surface`, `Border`, `Accent`, `Muted`, `SelectionBackground`,
`SelectionForeground`, `Error`, `Warning`, `Success`, and `Info` — every one of
which every theme resolves, by explicit value or fallback, and every one of
which has a matching `ThemeColors` property. See
[Themes](themes.md#themecolors-semantic-colors-as-color-values) for the
`ThemeColors` surface and late-resolution model, and
[Themes](themes.md#semantic-roles-and-fallbacks) for the full role reference and
fallback derivation.

## 7. Tooling / design time

- `StylePropertyRegistry.GetProperties(typeof(Gauge))` lists every property a
  type participates in (its own and inherited), forcing registration if needed.
- `StylePropertyRegistry.FindProperty(typeof(Gauge), "fill-color")` looks one up
  by serialized name.
- `ThemeResolver.Resolve(theme, typeof(Gauge), FillColorProperty, State.Normal)`
  evaluates a value for a type under a theme **without** a live control
  instance.

## 8. Complete external leaf

Ordinary mutable state uses the same `ChangeImpact` values as style metadata.
The control below needs no friend assembly or internal transaction API: it
commits state through `SetProperty`, measures with the inherited Unicode
`CellPolicy`, and draws through the protected canvas/style seams.

```csharp
public sealed class Gauge : Control
{
    public int Value
    {
        get;
        set
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    }

    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var markerWidth = CellPolicy.AmbiguousWidth == Ambiguous.Wide ? 2 : 1;
        var percentageWidth = Value.ToString(CultureInfo.InvariantCulture).Length + 1;
        return new Size(markerWidth + 1 + percentageWidth, 1);
    }

    protected override void OnRender(TerminalCanvas canvas)
    {
        RenderChrome(canvas);
        var content = string.Create(CultureInfo.InvariantCulture, $"· {Value}%");
        _ = canvas.Draw(
            content.AsSpan(),
            new Point(ContentBounds.X, ContentBounds.Y),
            ResolvedStyle);
    }
}
```

Equivalent assignments are quiet. Unknown impacts and invalid public values are
rejected before field mutation. The public setter must validate its own domain
before calling `SetProperty`; the helper validates its property name, impact,
dispatcher access, and lifetime. A property that changes checked,
indeterminate, selected, or another visual-state flag calls
`SetVisualStateProperty` instead, so warmed resolved values are cleared and
geometry-bearing state styles request layout rather than render alone. A
multi-child owner derives from `Container`,
iterates only its `Children`, and uses `MeasureChild`/`ArrangeChild`; it never
calls raw layout transactions.

`tests/SharpVision.Consumer.Tests` is the executable foundation proof. It
references only the production UI project and receives no `InternalsVisibleTo`.
Its `Gauge`, `FlowPanel`, `OverflowPanel`, `InteractiveProbe`,
`ExternalContentControl`, and `ExternalToggleChip` specimens
prove Unicode-aware leaf rendering, ordinary and unclipped custom layout,
protected focus/capture, lifecycle observation, single-content ownership,
visual-state mutation, and cancellation ordering. Composite, item, open-state,
named-part, and semantic specimens are added by later phases. Packing and consuming the
produced package is also a later gate; the project-reference suite does not
claim to prove package contents.
