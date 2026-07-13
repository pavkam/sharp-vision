// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using TerminalAttributes = TerminalAttributes;
using TerminalStyle = TerminalStyle;

internal static class ControlAppearance
{
    internal static ResolvedAppearance Resolve(Control control, State visualState)
    {
        ArgumentNullException.ThrowIfNull(control);

        Color? foreground = control.ResolveProperty(Control.ForegroundProperty, visualState);
        Color? background = control.ResolveProperty(Control.BackgroundProperty, visualState);
        TerminalAttributes? attributes = control.ResolveProperty(Control.AttributesProperty, visualState);
        Underline? underline = control.ResolveProperty(Control.UnderlineProperty, visualState);
        Color? underlineColor = control.ResolveProperty(Control.UnderlineColorProperty, visualState);
        FillMode fillMode = control.ResolveProperty(Control.FillModeProperty, visualState);
        (TerminalAttributes resolvedAttributes, Underline resolvedUnderline, Color resolvedUnderlineColor) = Decoration.Resolve(
            new TerminalStyle(
                foreground ?? Color.Default,
                background ?? Color.Default,
                attributes ?? TerminalAttributes.None),
            attributes,
            underline,
            underlineColor);

        TerminalStyle style = new TerminalStyle(
            foreground ?? Color.Default,
            background ?? Color.Default,
            resolvedAttributes,
            underline: resolvedUnderline,
            underlineColor: resolvedUnderlineColor);
        var hasOpaqueFill = fillMode == FillMode.Opaque ||
            (control.TryGetLocalValue(Control.BackgroundProperty, out Color? local) && local.HasValue) ||
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

        TerminalStyle body = control.GetResolvedAppearance(visualState).Style;
        Color? borderColor = control.ResolveProperty(Control.BorderColorProperty, visualState);
        TerminalAttributes? borderAttributes = control.ResolveProperty(Control.BorderAttributesProperty, visualState);
        (TerminalAttributes attributes, Underline underline, Color underlineColor) = Decoration.Resolve(body, borderAttributes);

        return new TerminalStyle(
            borderColor ?? body.Foreground,
            body.Background,
            attributes,
            body.Hyperlink,
            underline,
            underlineColor);
    }
}
