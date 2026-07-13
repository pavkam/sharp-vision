// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


using TerminalCanvas = Terminal.Rendering.Canvas;

/// <summary>Provides a third-party-style control with one custom style property.</summary>
internal sealed class DemoPanel: Control
{
    /// <summary>Registers the label-placement style property.</summary>
    internal static StyleProperty<DemoLabelPlacement> LabelPlacementProperty { get; } =
        StyleProperty<DemoLabelPlacement>.Register<DemoPanel>(
            "label-placement",
            DemoLabelPlacement.Left,
            Impact.Measure);

    /// <summary>Initializes a compact panel specimen for theme extensibility tests.</summary>
    internal DemoPanel()
    {
        Width = Length.Cells(12);
        Height = Length.Cells(3);
        Caption = "Demo";
    }

    /// <summary>Gets or sets the caption placement resolved through the theme cascade.</summary>
    internal DemoLabelPlacement LabelPlacement
    {
        get => GetValue(LabelPlacementProperty);
        set => SetValue(LabelPlacementProperty, value);
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
    protected override Size MeasureCore(Constraint constraint) => new(12, 3);

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        Style style = ResolvedStyle;
        Point captionPoint = LabelPlacement == DemoLabelPlacement.Right
            ? new Point(Bounds.Right - Caption.Length, Bounds.Y)
            : new Point(Bounds.X, Bounds.Y);
        _ = canvas.Draw(Caption.AsSpan(), captionPoint, style);
    }
}
