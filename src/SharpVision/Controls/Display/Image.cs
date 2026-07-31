// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using Terminal.Graphics;

using CellMetrics = Metrics;
using PlacementMode = Terminal.Graphics.PlacementMode;
using Shade = Terminal.Rendering.Shade;
using UnicodeWidth = Width;

/// <summary>Displays a borrowed immutable image with deterministic semantic cell fallback.</summary>
/// <remarks>
/// The control never owns or disposes <see cref="Source"/> and never emits terminal protocol
/// bytes. It paints its complete fallback first and records one backend-neutral placement last,
/// allowing later controls, popups, and windows to occlude the image through ordinary cell paint.
/// </remarks>
[PublicAPI]
public sealed class Image: Control
{
    /// <summary>Gets or sets the borrowed immutable image, or null for fallback-only rendering.</summary>
    /// <remarks>Replacement, null assignment, and control disposal never mutate or dispose the image.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ImageSource? Source
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets non-null plain fallback text drawn over the complete preview underlay.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a control character.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string AlternateText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (UnicodeWidth.Measure(value).Controls > 0)
            {
                throw new ArgumentException("Alternate text cannot contain control characters.", nameof(value));
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets how the complete source fits the arranged cell destination.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ImageStretch Stretch
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The image stretch mode is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;

        if (Source is { } source)
        {
            if (CellMetrics is { } metrics && metrics.TryMeasureCells(source.Size, out var cells))
            {
                return cells;
            }

            var sourceAlternateCells = UnicodeWidth.Measure(
                AlternateText.AsSpan(),
                CellPolicy.AmbiguousWidth).Cells;
            return new Size(Math.Max(1, sourceAlternateCells), 1);
        }

        var alternateCells = UnicodeWidth.Measure(
            AlternateText.AsSpan(),
            CellPolicy.AmbiguousWidth).Cells;
        return alternateCells == 0 ? default : new Size(alternateCells, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var source = Source;

        if (source is not null)
        {
            canvas.FillShade(bounds, Shade.Light, ResolvedStyle);
        }
        else if (AlternateText.Length == 0)
        {
            return;
        }

        if (AlternateText.Length != 0)
        {
            _ = canvas.Draw(
                AlternateText.AsSpan(),
                new Point(bounds.X, bounds.Y),
                ResolvedStyle);
        }

        if (source is not null)
        {
            canvas.DrawImage(source, bounds, ToPlacementMode(Stretch));
        }
    }

    /// <inheritdoc/>
    protected override void OnCellMetricsChanged(CellMetrics? previous, CellMetrics? current)
    {
        base.OnCellMetricsChanged(previous, current);

        if (Source is not null)
        {
            Invalidate(InvalidationImpact.Measure);
        }
    }

    private static PlacementMode ToPlacementMode(ImageStretch value) => value switch
    {
        ImageStretch.Contain => PlacementMode.Contain,
        ImageStretch.Cover => PlacementMode.Cover,
        ImageStretch.Stretch => PlacementMode.Stretch,
        _ => throw new UnreachableException("Validated image stretch must map to placement semantics.")
    };
}
