// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

/// <summary>Provides an externally authored leaf control with mutable state and Unicode-aware layout.</summary>
public sealed class Gauge: Control
{
    /// <summary>Initializes an empty percentage gauge.</summary>
    public Gauge()
    {
    }

    /// <summary>Gets or sets the inclusive percentage value from zero through one hundred.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is outside zero through one hundred.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Value
    {
        get;
        set
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A gauge value must be from zero through one hundred.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    }

    /// <summary>Gets the cell policy observed during the latest measure pass, or null before layout.</summary>
    public Policy? LastMeasuredPolicy { get; private set; }

    /// <summary>Gets the number of completed render-hook calls.</summary>
    public int RenderCount { get; private set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        LastMeasuredPolicy = CellPolicy;
        var markerWidth = CellPolicy.AmbiguousWidth == Ambiguous.Wide ? 2 : 1;
        var percentageWidth = Value.ToString(CultureInfo.InvariantCulture).Length + 1;
        return new Size(markerWidth + 1 + percentageWidth, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var content = string.Create(CultureInfo.InvariantCulture, $"· {Value}%");
        _ = canvas.Draw(content.AsSpan(), new Point(ContentBounds.X, ContentBounds.Y), ResolvedStyle);
        RenderCount++;
    }
}
