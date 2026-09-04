// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Provides dispatcher-timed playback, passive input behavior, and content-box rendering
/// for animated display indicators.</summary>
[PublicAPI]
public abstract class AnimatedIndicatorBase: ControlBase
{
    private readonly AnimationTimer _animation;
    private bool _wasEffectiveIsVisible = true;

    /// <summary>Initializes a playing, non-interactive indicator with a 200 millisecond cadence.</summary>
    protected AnimatedIndicatorBase()
    {
        _animation = new AnimationTimer(Interval, DispatchAnimationTick, () => EffectiveIsVisible)
        {
            IsPlaying = true
        };
        RegisterAttachmentParticipant(_animation);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        IsHitTestVisible = false;
        PropertyChanged += OnAnimatedIndicatorPropertyChanged;
    }

    /// <summary>Gets or sets the duration between semantic animation advances.</summary>
    /// <remarks>A derived indicator may schedule intermediate visual refreshes without changing
    /// this public semantic cadence.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the supported timer range.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed.</exception>
    public TimeSpan Interval
    {
        get;
        set
        {
            DispatcherTimer.ValidateInterval(value, nameof(value));
            VerifyMutable();

            _ = ShouldSynchronizeIntervalBeforePublication()
                ? SetPropertyAndSynchronize(
                    ref field,
                    value,
                    InvalidationImpact.None,
                    OnIntervalChanged)
                : SetPropertyAndContinue(
                    ref field,
                    value,
                    InvalidationImpact.None,
                    OnIntervalChanged);
        }
    } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets or sets whether attached playback advances automatically.</summary>
    /// <remarks>Pausing retains the current frame; resuming starts one complete scheduled interval.</remarks>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed.</exception>
    public bool IsPlaying
    {
        get;
        set => _ = SetPropertyAndSynchronize(
            ref field,
            value,
            InvalidationImpact.None,
            SynchronizePlayback);
    } = true;

    /// <summary>Updates derived timing state after <see cref="Interval"/> commits.</summary>
    /// <remarks>The default schedules the next timer callback at the new semantic interval.</remarks>
    protected virtual void OnIntervalChanged() => ScheduleAnimation(Interval);

    /// <summary>Chooses whether interval-dependent state synchronizes before property publication.</summary>
    /// <returns>True to synchronize before publication; false to publish before mandatory synchronization.</returns>
    /// <remarks>The default is true. A derived indicator may return false only when preserving an
    /// established observer-order contract; synchronization still runs when an observer throws.</remarks>
    protected virtual bool ShouldSynchronizeIntervalBeforePublication() => true;

    /// <summary>Updates derived clock state immediately before paused playback resumes.</summary>
    protected virtual void OnPlaybackStarting()
    {
    }

    /// <summary>Advances one semantic animation frame on the owning dispatcher.</summary>
    protected abstract void OnAnimationTick();

    /// <summary>Renders the current frame inside the already arranged content box.</summary>
    /// <param name="canvas">The frame-owned canvas clipped to <paramref name="bounds"/>.</param>
    /// <param name="bounds">The non-empty content box after border and padding are removed.</param>
    protected abstract void OnRenderFrame(TerminalCanvas canvas, Rect bounds);

    /// <summary>Schedules the next timer callback without changing <see cref="Interval"/>.</summary>
    /// <param name="interval">The positive supported dispatcher-timer interval.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the supported timer range.</exception>
    protected void ScheduleAnimation(TimeSpan interval)
    {
        DispatcherTimer.ValidateInterval(interval, nameof(interval));
        _animation.Interval = interval;
    }

    /// <inheritdoc/>
    protected sealed override void OnRenderContent(TerminalCanvas canvas)
    {
        _animation.EnsureRunning();
        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        OnRenderFrame(canvas.Clip(bounds), bounds);
    }

    private void SynchronizePlayback()
    {
        if (IsPlaying)
        {
            OnPlaybackStarting();
        }

        _animation.IsPlaying = IsPlaying;
    }

    private void DispatchAnimationTick()
    {
        if (IsPlaying && EffectiveIsVisible)
        {
            OnAnimationTick();
        }
    }

    /// <summary>Fires <see cref="OnPlaybackStarting"/> on a hidden-to-visible transition, matching
    /// the resume semantics the <see cref="IsPlaying"/>-driven path already gives a wall-clock
    /// sensitive subclass. A control that was never hidden while attached stays at the initial
    /// <see langword="true"/> baseline, so the very first render never double-fires alongside
    /// <see cref="ControlBase.OnAttached"/>.</summary>
    private void OnAnimatedIndicatorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName != nameof(EffectiveIsVisible))
        {
            return;
        }

        var isVisible = EffectiveIsVisible;

        if (isVisible && !_wasEffectiveIsVisible)
        {
            OnPlaybackStarting();
        }

        _wasEffectiveIsVisible = isVisible;
    }
}
