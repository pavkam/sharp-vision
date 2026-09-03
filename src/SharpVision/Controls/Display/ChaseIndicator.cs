// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using Terminal.Rendering;

/// <summary>Displays one or two moving glyphs with a fading trail on a horizontal or vertical track.</summary>
[PublicAPI]
public sealed class ChaseIndicator: AnimatedIndicatorBase, IStyled<ChaseIndicatorStyle>
{
    /// <summary>Gets the maximum retained movement frames supported by one indicator.</summary>
    internal const int MaximumTrailLength = 4096;

    private static readonly TimeSpan _maximumFadeRefresh = TimeSpan.FromMilliseconds(50);
    private static readonly long _minimumTimerTicks = TimeSpan.TicksPerMillisecond;

    private int[] _trailFirstPositions = [];
    private int[] _trailSecondPositions = [];
    private long[] _trailStartedAt = [];
    private int _trailCount;
    private long _animationTicks;
    private long _lastTimestamp;
    private long _movementElapsedTicks;
    private long _phase;
    private Rune _phaseActive;
    private Rune _phaseInactive;
    private bool _hasPhaseGlyphs;
    private readonly StyleSlot<ChaseIndicatorStyle> _style;

    /// <summary>Initializes a playing five-cell circle chase indicator.</summary>
    public ChaseIndicator()
    {
        _style = InitializeStyle(ChaseIndicatorStyle.Definition, OnStyleChanged);
        ResetPhase();
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed.</exception>
    public ChaseIndicatorStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public ChaseIndicatorStyle ActualStyle => _style.Actual;

    #region Playback properties

    /// <summary>Gets or sets how the active glyph traverses the track.</summary>
    /// <remarks>Changing the movement resets playback to its first frame.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed of.</exception>
    public ChaseMovement Movement
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The chase movement is unknown.");

            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            ResetPhase();
            NotifyPropertyChanged(nameof(Movement), InvalidationImpact.Render);
        }
    } = ChaseMovement.Bounce;

    /// <summary>Gets or sets the number of glyph positions in the track.</summary>
    /// <remarks>Changing the length resets playback to the first position.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than two.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed of.</exception>
    public int Length
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 2);
            _ = Extent(value, Spacing, nameof(value), value);
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            ResetPhase();
            NotifyPropertyChanged(nameof(Length), InvalidationImpact.Measure);
        }
    } = 5;

    /// <summary>Gets or sets whether glyph positions run horizontally or vertically.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed of.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The chase indicator orientation is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets the blank terminal cells between adjacent glyph positions.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or produces an unsupported extent.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed of.</exception>
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = Extent(Length, value, nameof(value), value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the number of preceding movement frames shown as a fading trail.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or exceeds 4096.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed of.</exception>
    public int TrailLength
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumTrailLength);

            _ = SetPropertyAndContinue(ref field, value, InvalidationImpact.Render, ReconfigureTrail);
        }
    } = 2;

    /// <summary>Gets or sets the duration for one abandoned head frame to reach the trail color.</summary>
    /// <remarks>The default is 400 milliseconds. Every retained frame fades independently.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the supported timer range.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed of.</exception>
    public TimeSpan FadeDuration
    {
        get;
        set
        {
            DispatcherTimer.ValidateInterval(value, nameof(value));

            _ = SetPropertyAndContinue(ref field, value, InvalidationImpact.Render, ScheduleNext);
        }
    } = TimeSpan.FromMilliseconds(400);

    /// <summary>Gets retained trail capacity for proving it follows the committed public length.</summary>
    internal int TrailCapacity => _trailFirstPositions.Length;

    /// <summary>Gets the most recently configured timer interval for proving property repair ordering.</summary>
    internal TimeSpan ScheduledInterval { get; private set; }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var extent = Extent(Length, Spacing, nameof(Spacing), Spacing);
        return Orientation == Orientation.Horizontal
            ? new Size(extent, 1)
            : new Size(1, extent);
    }

    /// <inheritdoc/>
    protected override void OnRenderFrame(TerminalCanvas canvas, Rect bounds)
    {
        var presentation = SynchronizeStylePhase();

        var active = presentation.Glyphs.Active.Resolve(new Rune('*'), CellPolicy.AmbiguousWidth);
        var inactive = presentation.Glyphs.Inactive.Resolve(new Rune('.'), CellPolicy.AmbiguousWidth);
        var axisLength = Orientation == Orientation.Horizontal ? bounds.Width : bounds.Height;
        var stride = Spacing + 1;
        var visible = Math.Min(Length, ((axisLength - 1) / stride) + 1);
        var inherited = ResolvedStyle;
        var headColor = ResolveColor(presentation.HeadColor);
        var trailColor = ResolveColor(presentation.TrailColor);
        var trackStyle = inherited.WithForeground(ResolveColor(presentation.TrackColor));

        for (var position = 0; position < visible; position++)
        {
            DrawPosition(canvas, bounds, position, stride, inactive, trackStyle);
        }

        for (var index = _trailCount - 1; index >= 0; index--)
        {
            var firstPosition = _trailFirstPositions[index];
            var secondPosition = _trailSecondPositions[index];

            if (firstPosition >= visible && (secondPosition < 0 || secondPosition >= visible))
            {
                continue;
            }

            var elapsed = Math.Clamp(
                _animationTicks - _trailStartedAt[index],
                0,
                FadeDuration.Ticks);
            var color = Interpolate(headColor, trailColor, elapsed, FadeDuration.Ticks);
            var trailStyle = inherited.WithForeground(color);

            if (firstPosition < visible)
            {
                DrawPosition(canvas, bounds, firstPosition, stride, active, trailStyle);
            }

            if (secondPosition >= 0 && secondPosition < visible)
            {
                DrawPosition(canvas, bounds, secondPosition, stride, active, trailStyle);
            }
        }

        PositionsAtPhase(_phase, out var head, out var secondHead);

        if (head < visible)
        {
            DrawPosition(
                canvas,
                bounds,
                head,
                stride,
                active,
                inherited.WithForeground(headColor));
        }

        if (secondHead >= 0 && secondHead < visible)
        {
            DrawPosition(
                canvas,
                bounds,
                secondHead,
                stride,
                active,
                inherited.WithForeground(headColor));
        }
    }

    private void OnStyleChanged(ChaseIndicatorStyle previous, ChaseIndicatorStyle current)
    {
        _ = previous;
        _ = current;
        _ = SynchronizeStylePhase();
    }

    private ChaseIndicatorStyle SynchronizeStylePhase()
    {
        var style = ActualStyle;
        if (!_hasPhaseGlyphs || _phaseActive != style.Glyphs.Active || _phaseInactive != style.Glyphs.Inactive)
        {
            _phaseActive = style.Glyphs.Active;
            _phaseInactive = style.Glyphs.Inactive;
            _hasPhaseGlyphs = true;
            ResetPhase();
        }

        return style;
    }

    #endregion

    #region Lifetime and timing

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        Debug.Assert(Dispatcher is not null, "An attached chase indicator owns a dispatcher.");
        SetLastTimestamp();
        ScheduledInterval = NextInterval();
        ScheduleAnimation(ScheduledInterval);
    }

    /// <inheritdoc/>
    protected override void OnAnimationTick()
    {
        var elapsed = ElapsedSinceLastTimestamp();

        _animationTicks += elapsed.Ticks;
        _movementElapsedTicks += elapsed.Ticks;

        if (_movementElapsedTicks >= Interval.Ticks)
        {
            PositionsAtPhase(_phase, out var first, out var second);
            AddTrail(first, second);
            _phase = (_phase + 1) % CycleLength;
            _movementElapsedTicks = 0;
        }

        Invalidate(InvalidationImpact.Render);
        ScheduleNext();
    }

    /// <inheritdoc/>
    protected override void OnIntervalChanged() => RestartInterval();

    /// <inheritdoc/>
    protected override bool ShouldSynchronizeIntervalBeforePublication() => false;

    /// <inheritdoc/>
    protected override void OnPlaybackStarting()
    {
        SetLastTimestamp();
        ScheduleNext();
    }

    private long CycleLength => Movement switch
    {
        ChaseMovement.Bounce => 2L * (Length - 1),
        ChaseMovement.Wrap => Length,
        ChaseMovement.Spread => Math.Max(1L, 2L * ((Length - 1) / 2)),
        _ => throw new UnreachableException()
    };

    private void ResetPhase()
    {
        _phase = 0;
        _movementElapsedTicks = 0;
        SeedTrail();
        ScheduleNext();
    }

    private void SeedTrail()
    {
        var capacity = Math.Min(TrailLength, Length - 1);
        _trailFirstPositions = new int[capacity];
        _trailSecondPositions = new int[capacity];
        _trailStartedAt = new long[capacity];
        _trailCount = capacity;

        for (var index = 0; index < capacity; index++)
        {
            PositionsAtPhase(
                _phase - index - 1L,
                out _trailFirstPositions[index],
                out _trailSecondPositions[index]);
            var elapsed = SaturatingElapsed(index);
            _trailStartedAt[index] = _animationTicks - elapsed;
        }
    }

    private void ResizeTrail()
    {
        var capacity = Math.Min(TrailLength, Length - 1);
        var firstPositions = new int[capacity];
        var secondPositions = new int[capacity];
        var startedAt = new long[capacity];
        var retained = Math.Min(_trailCount, capacity);

        Array.Copy(_trailFirstPositions, firstPositions, retained);
        Array.Copy(_trailSecondPositions, secondPositions, retained);
        Array.Copy(_trailStartedAt, startedAt, retained);

        for (var index = retained; index < capacity; index++)
        {
            PositionsAtPhase(
                _phase - index - 1L,
                out firstPositions[index],
                out secondPositions[index]);
            startedAt[index] = _animationTicks - FadeDuration.Ticks;
        }

        _trailFirstPositions = firstPositions;
        _trailSecondPositions = secondPositions;
        _trailStartedAt = startedAt;
        _trailCount = capacity;
    }

    private void ReconfigureTrail()
    {
        ResizeTrail();
        ScheduleNext();
    }

    private void RestartInterval()
    {
        _movementElapsedTicks = 0;
        ScheduleNext();
    }

    private void AddTrail(int firstPosition, int secondPosition)
    {
        if (_trailFirstPositions.Length == 0)
        {
            return;
        }

        var retained = Math.Min(_trailCount, _trailFirstPositions.Length - 1);

        if (retained > 0)
        {
            Array.Copy(_trailFirstPositions, 0, _trailFirstPositions, 1, retained);
            Array.Copy(_trailSecondPositions, 0, _trailSecondPositions, 1, retained);
            Array.Copy(_trailStartedAt, 0, _trailStartedAt, 1, retained);
        }

        _trailFirstPositions[0] = firstPosition;
        _trailSecondPositions[0] = secondPosition;
        _trailStartedAt[0] = _animationTicks;
        _trailCount = Math.Min(_trailCount + 1, _trailFirstPositions.Length);
    }

    [Pure]
    private long SaturatingElapsed(int priorIntervalCount)
    {
        if (priorIntervalCount == 0)
        {
            return 0;
        }

        var maximumCount = FadeDuration.Ticks / Interval.Ticks;
        return priorIntervalCount > maximumCount
            ? FadeDuration.Ticks
            : Math.Min(FadeDuration.Ticks, priorIntervalCount * Interval.Ticks);
    }

    [Pure]
    private void PositionsAtPhase(long phase, out int first, out int second)
    {
        var normalized = phase % CycleLength;

        if (normalized < 0)
        {
            normalized += CycleLength;
        }

        switch (Movement)
        {
            case ChaseMovement.Bounce:
                first = normalized < Length
                    ? (int) normalized
                    : (int) (CycleLength - normalized);
                second = -1;
                return;
            case ChaseMovement.Wrap:
                first = (int) normalized;
                second = -1;
                return;
            case ChaseMovement.Spread:
                var maximumRadius = (Length - 1) / 2;
                var radius = normalized <= maximumRadius
                    ? (int) normalized
                    : (int) (CycleLength - normalized);
                first = ((Length - 1) / 2) - radius;
                second = (Length / 2) + radius;

                if (first == second)
                {
                    second = -1;
                }

                return;
            default:
                throw new UnreachableException();
        }
    }

    private TimeSpan ElapsedSinceLastTimestamp()
    {
        Debug.Assert(Dispatcher is not null, "Attached timer callbacks own a dispatcher clock.");
        var timestamp = Dispatcher.TimeProvider.GetTimestamp();
        var elapsed = Dispatcher.TimeProvider.GetElapsedTime(_lastTimestamp, timestamp);
        _lastTimestamp = timestamp;
        return elapsed;
    }

    private void SetLastTimestamp()
    {
        if (Dispatcher is null)
        {
            return;
        }

        _lastTimestamp = Dispatcher.TimeProvider.GetTimestamp();
    }

    [Pure]
    private TimeSpan NextInterval()
    {
        var nextTicks = Interval.Ticks - _movementElapsedTicks;

        for (var index = 0; index < _trailCount; index++)
        {
            var elapsed = Math.Max(0, _animationTicks - _trailStartedAt[index]);

            if (elapsed >= FadeDuration.Ticks)
            {
                continue;
            }

            var remaining = FadeDuration.Ticks - elapsed;
            nextTicks = Math.Min(nextTicks, Math.Min(_maximumFadeRefresh.Ticks, remaining));
        }

        // Fade interpolation uses sub-millisecond ticks, while DispatcherTimer
        // accepts only whole-millisecond intervals. Quantize the final refresh
        // without changing the elapsed-tick value used by rendering.
        nextTicks = Math.Max(nextTicks, _minimumTimerTicks);
        Debug.Assert(nextTicks >= _minimumTimerTicks, "Timer scheduling produces a supported interval.");
        return TimeSpan.FromTicks(nextTicks);
    }

    private void ScheduleNext()
    {
        ScheduledInterval = NextInterval();
        ScheduleAnimation(ScheduledInterval);
    }

    private void DrawPosition(
        TerminalCanvas canvas,
        Rect bounds,
        int position,
        int stride,
        Rune glyph,
        TerminalStyle style)
    {
        var offset = position * stride;
        var point = Orientation == Orientation.Horizontal
            ? new Point(bounds.X + offset, bounds.Y)
            : new Point(bounds.X, bounds.Y + offset);
        canvas.DrawRune(glyph, point, style, BackgroundMode.Transparent);
    }

    [Pure]
    private static Color Interpolate(
        Color head,
        Color trail,
        long elapsedTicks,
        long durationTicks)
    {
        Debug.Assert(elapsedTicks >= 0 && elapsedTicks <= durationTicks, "Fade elapsed is clamped.");

        if (elapsedTicks == 0)
        {
            return head;
        }

        if (head.IsDefault || trail.IsDefault)
        {
            return trail;
        }

        Debug.Assert(head.IsRgb, "Resolved chase head colors are RGB or terminal default.");
        Debug.Assert(trail.IsRgb, "Resolved chase trail colors are RGB or terminal default.");
        return Color.Rgb(
            InterpolateComponent(head.Red, trail.Red, elapsedTicks, durationTicks),
            InterpolateComponent(head.Green, trail.Green, elapsedTicks, durationTicks),
            InterpolateComponent(head.Blue, trail.Blue, elapsedTicks, durationTicks));
    }

    [Pure]
    private static int InterpolateComponent(
        byte start,
        byte end,
        long elapsedTicks,
        long durationTicks)
    {
        var ratio = elapsedTicks / (double) durationTicks;
        return (int) Math.Round(
            start + ((end - start) * ratio),
            MidpointRounding.AwayFromZero);
    }

    [Pure]
    private static int Extent(
        int length,
        int spacing,
        string parameterName,
        int actualValue)
    {
        var extent = length + ((long) (length - 1) * spacing);

        return extent > int.MaxValue
            ? throw new ArgumentOutOfRangeException(
                parameterName,
                actualValue,
                "The spaced chase indicator exceeds the supported cell dimension.")
            : (int) extent;
    }

    #endregion
}
