using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

internal static class ControlAppearance
{
    internal static ResolvedAppearance Resolve(Control control, State visualState)
    {
        ArgumentNullException.ThrowIfNull(control);

        var foreground = control.ResolveProperty(Control.ForegroundProperty, visualState);
        var background = control.ResolveProperty(Control.BackgroundProperty, visualState);
        var attributes = control.ResolveProperty(Control.AttributesProperty, visualState);
        var underline = control.ResolveProperty(Control.UnderlineProperty, visualState);
        var underlineColor = control.ResolveProperty(Control.UnderlineColorProperty, visualState);
        var fillMode = control.ResolveProperty(Control.FillModeProperty, visualState);
        var (resolvedAttributes, resolvedUnderline, resolvedUnderlineColor) = Decoration.Resolve(
            new TerminalStyle(
                foreground ?? Color.Default,
                background ?? Color.Default,
                attributes ?? TerminalAttributes.None),
            attributes,
            underline,
            underlineColor);

        var style = new TerminalStyle(
            foreground ?? Color.Default,
            background ?? Color.Default,
            resolvedAttributes,
            underline: resolvedUnderline,
            underlineColor: resolvedUnderlineColor);
        var hasOpaqueFill = fillMode == FillMode.Opaque ||
            (control.TryGetLocalValue(Control.BackgroundProperty, out var local) && local.HasValue) ||
            background.HasValue;

        return new ResolvedAppearance
        {
            Style = style,
            HasOpaqueFill = hasOpaqueFill,
        };
    }

    internal static TerminalStyle ResolveTerminalStyle(Control control, State visualState) =>
        control.GetResolvedAppearance(visualState).Style;

    internal static bool HasOpaqueFill(Control control, State visualState) =>
        control.GetResolvedAppearance(visualState).HasOpaqueFill;

    internal static TerminalStyle ResolveBorderStyle(Control control, State visualState)
    {
        ArgumentNullException.ThrowIfNull(control);

        var body = control.GetResolvedAppearance(visualState).Style;
        var borderColor = control.ResolveProperty(Control.BorderColorProperty, visualState);
        var borderAttributes = control.ResolveProperty(Control.BorderAttributesProperty, visualState);
        var (attributes, underline, underlineColor) = Decoration.Resolve(body, borderAttributes);

        return new TerminalStyle(
            borderColor ?? body.Foreground,
            body.Background,
            attributes,
            body.Hyperlink,
            underline,
            underlineColor);
    }
}
