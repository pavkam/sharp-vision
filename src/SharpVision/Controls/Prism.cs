// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Applies a deterministic RGB color cycle to the foreground of one replaceable content control.</summary>
/// <remarks>
/// Prism participates in ordinary <see cref="ContentControl"/> ownership and layout. It renders its
/// content first, then transforms only stored grapheme owners inside the intersection of that child's
/// arranged bounds and the border-and-padding-deflated content box. Backgrounds, attributes, hyperlinks,
/// underline semantics, wide-cell ownership, and Prism chrome remain unchanged. Effect properties are
/// dispatcher-affine while attached and request render-only invalidation; callers advance
/// <see cref="Phase"/> explicitly when animation is desired.
/// </remarks>
public sealed class Prism: ContentControl
{
    private readonly Func<Point, Color> _selector;

    #region Construction

    /// <summary>Initializes an empty Prism with a diagonal eighteen-cell cycle at phase zero.</summary>
    public Prism() => _selector = SelectColor;

    #endregion

    #region Effect properties

    /// <summary>Gets or sets the normalized color-cycle offset in the half-open range zero through one.</summary>
    /// <remarks>
    /// One complete unit represents a full RGB hue rotation. Changing the phase preserves measure and
    /// arrangement and requests only rendering. Mutation is dispatcher-affine while the control is attached.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is non-finite or outside [0, 1).</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public double Phase
    {
        get;
        set
        {
            if (!double.IsFinite(value) || value < 0d || value >= 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "The Prism phase must be finite and from zero up to, but not including, one.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets the positive number of terminal cells in one complete color cycle.</summary>
    /// <remarks>
    /// Coordinates are divided by this value before the normalized <see cref="Phase"/> is applied.
    /// Changing the cycle length preserves measure and arrangement and requests only rendering.
    /// Mutation is dispatcher-affine while the control is attached.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int CycleLength
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = 18;

    /// <summary>Gets or sets the content-relative coordinate direction used to advance the color cycle.</summary>
    /// <remarks>
    /// Changing the direction preserves measure and arrangement and requests only rendering.
    /// Mutation is dispatcher-affine while the control is attached.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public PrismDirection Direction
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The Prism direction is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = PrismDirection.Diagonal;

    #endregion

    #region Rendering

    /// <summary>Renders ordinary content, then transforms its stored foregrounds in place.</summary>
    /// <param name="canvas">The frame-owned canvas clipped to this Prism's arranged bounds.</param>
    /// <remarks>
    /// The cached selector is borrowed synchronously by the canvas. The effect is restricted to
    /// the retained child's arranged bounds inside <see cref="Control.ContentBounds"/>, so empty
    /// content, this control's own border and shadow, and cells outside the child are not recolored.
    /// </remarks>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        var content = Content;
        base.RenderChildren(canvas);

        if (content is null)
        {
            return;
        }

        canvas.ApplyForeground(ContentBounds.Intersect(content.Bounds), _selector);
    }

    private Color SelectColor(Point point)
    {
        var bounds = ContentBounds;
        var x = point.X - bounds.X;
        var y = point.Y - bounds.Y;
        var coordinate = Direction switch
        {
            PrismDirection.Horizontal => x,
            PrismDirection.Vertical => y,
            PrismDirection.Diagonal => checked(x + y),
            _ => throw new UnreachableException(),
        };
        var hue = Phase + ((double) coordinate / CycleLength);
        hue -= Math.Floor(hue);
        var scaled = hue * 6d;
        var sector = (int) Math.Floor(scaled);
        var rising = (int) Math.Round((scaled - sector) * byte.MaxValue, MidpointRounding.AwayFromZero);
        var falling = byte.MaxValue - rising;

        return sector switch
        {
            0 => Color.Rgb(byte.MaxValue, rising, 0),
            1 => Color.Rgb(falling, byte.MaxValue, 0),
            2 => Color.Rgb(0, byte.MaxValue, rising),
            3 => Color.Rgb(0, falling, byte.MaxValue),
            4 => Color.Rgb(rising, 0, byte.MaxValue),
            _ => Color.Rgb(byte.MaxValue, 0, falling),
        };
    }

    #endregion
}
