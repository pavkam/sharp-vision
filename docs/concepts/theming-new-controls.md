# Theming a new control

Custom controls use the same retained-object and direct-appearance model as
built-in controls. There is no property registry, selector cascade, or theme
recipe registration.

## Ordinary CLR properties

Expose configuration as validated CLR properties and choose the smallest
invalidation impact:

```csharp
public sealed class Gauge : Control
{
    public int Segments
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = 10;

    public ThemeColor? FillColor
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }
}
```

Use `Color`, `ColorRole`, or `ThemeColor` for UI colours. `ColorRole.Accent`
remains deferred until the control renders against its inherited `Theme`; a
terminal `Color` is always concrete.

## Appearance and visual state

`Appearance` is an immutable optional overlay. A control may set a normal local
appearance through `Appearance`, and can set a policy for one visual state with
`SetAppearance`:

```csharp
SetAppearance(
    VisualState.Current,
    new Appearance(
        foreground: ColorRole.SelectionForeground,
        background: ColorRole.SelectionBackground,
        attributes: null,
        underline: null,
        underlineColor: null,
        borderColor: null,
        borderAttributes: null,
        shadowForeground: null,
        shadowBackground: null,
        shadowAttributes: null));
```

The resolver applies local state in this fixed order:

```text
PointerOver -> FocusWithin -> Focused -> Current -> Selected -> Checked
-> Indeterminate -> Pressed -> Disabled
```

Implement `IsCheckedState`, `IsSelectedState`, or `IsIndeterminateState` only
when the control owns that semantic fact. Do not override state assembly or
borrow parent selection as a styling shortcut.

## Rendering

`ResolvedStyle` contains the concrete terminal values for the current local
appearance. A normal control fills or preserves its own cells according to its
documented background policy, then draws content and children. Intrinsic chrome
is rendered by the framework path; custom content should use the resolved style
without emitting terminal escape bytes.

```csharp
protected override void OnRenderContent(TerminalCanvas canvas)
{
    var style = ResolvedStyle;
    // Draw semantic cells with style.
}
```

Text values inherit only normal foreground/attributes/underline from an ordinary
parent. Background, border, shadow, and state overlays never cascade. Set
`AppearanceBoundary = true` when a composite's private root must start a new
ambient text context.

## Public proof

A shipped external control needs a consumer test built without internals access.
Its tests should cover argument validation, direct property invalidation, role
resolution after an application theme swap, state-specific appearance, and final
cells rather than private resolver calls.
