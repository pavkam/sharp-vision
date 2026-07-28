// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

#pragma warning disable IDE0001 // Keep the terminal drawing alias explicit after retiring layout Canvas.
using TerminalCanvas = Terminal.Rendering.Canvas;
#pragma warning restore IDE0001

/// <summary>Provides a third-party-style control with one custom style property.</summary>
internal sealed class DemoPanel: Control
{
    /// <summary>Initializes a compact panel specimen for theme extensibility tests.</summary>
    internal DemoPanel()
    {
        Width = Length.Cells(12);
        Height = Length.Cells(3);
        Caption = "Demo";
    }

    /// <summary>Gets or sets the caption placement.</summary>
    internal DemoLabelPlacement LabelPlacement
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets the readable caption drawn according to <see cref="LabelPlacement"/>.</summary>
    /// <exception cref="ArgumentNullException">The assigned caption is null.</exception>
    internal string Caption
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => new(12, 3);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        var captionPoint = LabelPlacement == DemoLabelPlacement.Right
            ? new Point(Bounds.Right - Caption.Length, Bounds.Y)
            : new Point(Bounds.X, Bounds.Y);
        _ = canvas.Draw(Caption.AsSpan(), captionPoint, style);
    }
}
