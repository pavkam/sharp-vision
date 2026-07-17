// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Renders one concrete terminal color as an opaque preview.</summary>
internal sealed class ColorSwatch: Control
{
    /// <summary>Initializes a six-cell RGB-red preview.</summary>
    internal ColorSwatch()
    {
        Width = Length.Cells(6);
        Height = Length.Cells(1);
    }

    /// <summary>Gets or sets the preview color.</summary>
    internal Color Value
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    } = Color.Rgb(255, 0, 0);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint.Width;
        return new Size(6, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = new TerminalStyle(ColorMath.Contrast(ResolvedRgb()), Value);
        canvas.Fill(ContentBounds, new Rune(' '), style);
    }

    private Color ResolvedRgb()
    {
        var resolved = Terminal.Rendering.Palette.Resolve(Value);
        return resolved.Kind == ColorKind.Rgb ? resolved : Color.Rgb(0, 0, 0);
    }
}
