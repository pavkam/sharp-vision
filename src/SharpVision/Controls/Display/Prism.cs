// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Applies a deterministic RGB color cycle to the foreground of one replaceable content control.</summary>
/// <remarks>
/// Prism participates in ordinary <see cref="ContentControl"/> ownership and layout. It renders its
/// content inside the intersection of that child's arranged bounds and the border-and-padding-deflated
/// content box, then transforms only the stored grapheme owners actually written by that render pass.
/// Backgrounds, attributes, hyperlinks, underline semantics, wide-cell ownership, pre-existing underlay,
/// and Prism chrome remain unchanged. Effect properties are dispatcher-affine while attached and request
/// render-only invalidation; callers advance <see cref="Phase"/> explicitly when animation is desired.
/// </remarks>
[PublicAPI]
public sealed class Prism: ContentControl
{
    // A complete constructor Face outranks every theme state contribution and disables ambient
    // inheritance, permanently opting Prism out of visual-state feedback. Only the transparent
    // background is Prism-specific; the rest matches the ControlBase role's own normal defaults, so a
    // partial FaceOverlay contribution keeps state behavior alive.
    private static readonly AppearanceStatesOverlay _prismAppearance = new(
        normal: new AppearanceOverlay(
            face: new FaceOverlay(
                foreground: SemanticColor.ControlText,
                background: Color.Transparent,
                attributes: SemanticDecoration.NormalText,
                underline: Underline.None,
                underlineColor: Color.Default)));

    private readonly Action<TerminalCanvas> _draw;
    private readonly Func<Point, Color> _selector;
    private Rect _renderContentClip;

    #region Construction

    /// <summary>Initializes an empty Prism with a diagonal eighteen-cell cycle at phase zero.</summary>
    public Prism()
    {
        InitializeAppearanceOverlay(_prismAppearance);
        _draw = DrawContent;
        _selector = SelectColor;
    }

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
                    nameof(value), value,
                    "The Prism phase must be finite and from zero up to, but not including, one.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
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
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The Prism direction is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = PrismDirection.Diagonal;

    #endregion

    #region Rendering

    /// <summary>Renders ordinary content, then transforms its stored foregrounds in place.</summary>
    /// <param name="canvas">The frame-owned canvas clipped to this Prism's arranged bounds.</param>
    /// <param name="contentClip">The inherited soft content clip for the retained child.</param>
    /// <remarks>
    /// The cached draw callback and selector are borrowed synchronously by the canvas. Mutation
    /// provenance restricts the effect to owners written by the retained child inside its arranged
    /// bounds and <see cref="ControlBase.ContentBounds"/>, so empty content, underlay, this control's own
    /// border and shadow, and cells outside the child are not recolored.
    /// </remarks>
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        var content = Content;

        if (content is null)
        {
            base.RenderChildren(canvas, contentClip);
            return;
        }

        _renderContentClip = contentClip;

        try
        {
            canvas.DrawWithForeground(
                ContentBounds.Intersect(content.Bounds),
                _draw,
                _selector);
        }
        finally
        {
            _renderContentClip = default;
        }
    }

    private void DrawContent(TerminalCanvas canvas) =>
        base.RenderChildren(canvas, _renderContentClip);

    [Pure]
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
            _ => throw new UnreachableException()
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
            _ => Color.Rgb(byte.MaxValue, 0, falling)
        };
    }

    #endregion
}
