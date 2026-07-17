// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Displays one active glyph bouncing through a fixed-length horizontal track.</summary>
public sealed class ChaseIndicator: Control
{
    private static readonly (ThemedGlyph Active, ThemedGlyph Inactive)[] _glyphs =
    [
        (new ThemedGlyph(new Rune('●'), new Rune('@')), new ThemedGlyph(new Rune('◯'), new Rune('o'))),
        (new ThemedGlyph(new Rune('◆'), new Rune('*')), new ThemedGlyph(new Rune('◇'), new Rune('.'))),
        (new ThemedGlyph(new Rune('■'), new Rune('#')), new ThemedGlyph(new Rune('□'), new Rune('.'))),
        (new ThemedGlyph(new Rune('▲'), new Rune('^')), new ThemedGlyph(new Rune('△'), new Rune('.'))),
        (new ThemedGlyph(new Rune('▼'), new Rune('v')), new ThemedGlyph(new Rune('▽'), new Rune('.'))),
        (new ThemedGlyph(new Rune('◀'), new Rune('<')), new ThemedGlyph(new Rune('◁'), new Rune('.'))),
        (new ThemedGlyph(new Rune('▶'), new Rune('>')), new ThemedGlyph(new Rune('▷'), new Rune('.'))),
    ];
    private int _direction = 1;
    private int _position;
    private DispatcherTimer? _timer;

    /// <summary>Initializes a playing five-cell circle chase indicator.</summary>
    public ChaseIndicator()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        IsHitTestVisible = false;
    }

    #region Playback properties

    /// <summary>Gets or sets the built-in active and inactive glyph pair.</summary>
    /// <remarks>Changing the pattern resets playback to the first position.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed.</exception>
    public ChasePattern Pattern
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The chase pattern is unknown.");
            }

            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            ResetPhase();
            NotifyPropertyChanged(nameof(Pattern), ChangeImpact.Render);
        }
    } = ChasePattern.Circle;

    /// <summary>Gets or sets the horizontal track length in terminal cells.</summary>
    /// <remarks>Changing the length resets playback to the first position.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than two.</exception>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed.</exception>
    public int Length
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 2);
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            ResetPhase();
            NotifyPropertyChanged(nameof(Length), ChangeImpact.Measure);
        }
    } = 5;

    /// <summary>Gets or sets the duration between position advances.</summary>
    /// <remarks>The default is 200 milliseconds. Changing a running indicator restarts one complete interval.</remarks>
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

            if (field == value)
            {
                return;
            }

            if (_timer is { } timer)
            {
                timer.Interval = value;
            }

            field = value;
            NotifyPropertyChanged(nameof(Interval), ChangeImpact.None);
        }
    } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets or sets whether attached playback advances automatically.</summary>
    /// <remarks>Pausing retains the current position; resuming starts one complete interval.</remarks>
    /// <exception cref="InvalidOperationException">The attached indicator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The indicator is disposed.</exception>
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

            if (_timer is not null)
            {
                if (value)
                {
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
            }

            field = value;
            NotifyPropertyChanged(nameof(IsPlaying), ChangeImpact.None);
        }
    } = true;

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(Length, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        (var activeGlyph, var inactiveGlyph) = _glyphs[(int) Pattern];
        var active = CellGlyph.Resolve(
            activeGlyph.Value,
            activeGlyph.Fallback,
            CellPolicy.AmbiguousWidth);
        var inactive = CellGlyph.Resolve(
            inactiveGlyph.Value,
            inactiveGlyph.Fallback,
            CellPolicy.AmbiguousWidth);
        var visible = Math.Min(Length, Bounds.Width);

        for (var offset = 0; offset < visible; offset++)
        {
            canvas.DrawRune(
                offset == _position ? active : inactive,
                new Point(Bounds.X + offset, Bounds.Y),
                ResolvedStyle,
                BackgroundMode.Transparent);
        }
    }

    #endregion

    #region Lifetime and timing

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        Debug.Assert(Dispatcher is not null, "An attached chase indicator owns a dispatcher.");
        _timer = new DispatcherTimer(Dispatcher, Interval);
        _timer.Tick += OnTick;

        if (IsPlaying)
        {
            _timer.Start();
        }
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        ReleaseTimer();
        base.OnDetached();
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        ReleaseTimer();
        base.OnDisposing();
    }

    private void OnTick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!IsPlaying || !EffectiveIsVisible)
        {
            return;
        }

        var next = _position + _direction;

        if (next < 0 || next >= Length)
        {
            _direction = -_direction;
            next = _position + _direction;
        }

        _position = next;
        Invalidate(ChangeImpact.Render);
    }

    private void ResetPhase()
    {
        _position = 0;
        _direction = 1;
    }

    private void ReleaseTimer()
    {
        var timer = _timer;

        if (timer is null)
        {
            return;
        }

        timer.Tick -= OnTick;
        timer.Dispose();
        _timer = null;
    }

    #endregion
}
