// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Collections.Immutable;

/// <summary>Displays one automatically advancing glyph from a built-in frame sequence.</summary>
[PublicAPI]
public sealed class Spinner: AnimatedIndicatorBase, IStyled<SpinnerStyle>
{
    private int _frameIndex;
    private ImmutableArray<Rune> _phaseFrames;
    private bool _hasPhaseFrames;
    private readonly StyleSlot<SpinnerStyle> _style;

    /// <summary>Initializes a playing one-cell Braille spinner.</summary>
    public Spinner() => _style = InitializeStyle(SpinnerStyle.Definition, OnStyleChanged);

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached spinner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The spinner is disposed.</exception>
    public SpinnerStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public SpinnerStyle ActualStyle => _style.Actual;

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderFrame(TerminalCanvas canvas, Rect bounds)
    {
        var frames = Frames();

        // Resolve against the live Ambiguous-width policy rather than drawing the frame raw:
        // SpinnerStyle only validates frames under Ambiguous.Narrow at construction, so a frame
        // that is one cell under Narrow can still be two cells under Wide. The fallback is drawn
        // per-index from the built-in Ascii rotation (cycling if the custom sequence is longer)
        // so the animation still reads as motion instead of collapsing to one repeated glyph.
        var asciiFrames = SpinnerStyle.Ascii.Frames;
        var fallback = asciiFrames[_frameIndex % asciiFrames.Length];
        var frame = frames[_frameIndex].Resolve(fallback, CellPolicy.AmbiguousWidth);
        canvas.DrawRune(
            frame,
            new Point(bounds.X, bounds.Y),
            ResolvedStyle,
            BackgroundMode.Transparent);
    }

    #endregion

    private void OnStyleChanged(SpinnerStyle previous, SpinnerStyle current)
    {
        _ = previous;
        _ = current;
        _ = Frames();
    }

    private ImmutableArray<Rune> Frames()
    {
        var frames = ActualStyle.Frames;
        if (!_hasPhaseFrames || !_phaseFrames.AsSpan().SequenceEqual(frames.AsSpan()))
        {
            _phaseFrames = frames;
            _hasPhaseFrames = true;
            _frameIndex = 0;
        }

        return frames;
    }

    /// <inheritdoc/>
    protected override void OnAnimationTick()
    {
        var frames = Frames();
        _frameIndex = (_frameIndex + 1) % frames.Length;
        Invalidate(InvalidationImpact.Render);
    }

}
