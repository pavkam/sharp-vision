// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Collections.Immutable;

/// <summary>Displays one automatically advancing glyph from a built-in frame sequence.</summary>
[PublicAPI]
public sealed class Spinner: ControlBase, IStyled<SpinnerStyle>
{
    private int _frameIndex;
    private ImmutableArray<Rune> _phaseFrames;
    private bool _hasPhaseFrames;
    private readonly AnimationTimer _animation;
    private readonly StyleSlot<SpinnerStyle> _style;

    /// <summary>Initializes a playing one-cell Braille spinner.</summary>
    public Spinner()
    {
        _style = InitializeStyle(SpinnerStyle.Definition, OnStyleChanged);
        _animation = new AnimationTimer(TimeSpan.FromMilliseconds(200), OnTick, () => EffectiveIsVisible) { IsPlaying = true };
        RegisterAttachmentParticipant(_animation);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        IsHitTestVisible = false;
    }

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

    #region Playback properties

    /// <summary>Gets or sets the duration between frame advances.</summary>
    /// <remarks>The default is 200 milliseconds. Changing a running spinner restarts one complete interval.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the supported timer range.</exception>
    /// <exception cref="InvalidOperationException">The attached spinner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The spinner is disposed.</exception>
    public TimeSpan Interval
    {
        get;
        set
        {
            DispatcherTimer.ValidateInterval(value, nameof(value));
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            _animation.Interval = value;
            field = value;
            NotifyPropertyChanged(nameof(Interval), InvalidationImpact.None);
        }
    } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets or sets whether attached playback advances automatically.</summary>
    /// <remarks>Pausing retains the current frame; resuming starts one complete interval.</remarks>
    /// <exception cref="InvalidOperationException">The attached spinner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The spinner is disposed.</exception>
    public bool IsPlaying
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            _animation.IsPlaying = value;
            field = value;
            NotifyPropertyChanged(nameof(IsPlaying), InvalidationImpact.None);
        }
    } = true;

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        _animation.EnsureRunning();

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var frames = Frames();
        canvas.DrawRune(
            frames[_frameIndex],
            new Point(Bounds.X, Bounds.Y),
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

    private void OnTick()
    {
        if (!IsPlaying || !EffectiveIsVisible)
        {
            return;
        }

        var frames = Frames();
        _frameIndex = (_frameIndex + 1) % frames.Length;
        Invalidate(InvalidationImpact.Render);
    }

}
