// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Renders one concrete terminal color as an opaque preview.</summary>
internal sealed class ColorSwatch: ControlBase
{
    /// <summary>Initializes a six-cell RGB-red preview.</summary>
    public ColorSwatch()
    {
        Width = Length.Cells(6);
        Height = Length.Cells(1);
    }

    /// <summary>Gets or sets the preview color.</summary>
    public Color Value
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
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
        var rgb = Value.IsRgb ? Value : Color.Rgb(0, 0, 0);
        var style = new TerminalStyle(rgb.Contrast(), Value);
        canvas.Fill(ContentBounds, new Rune(' '), style);
    }
}
