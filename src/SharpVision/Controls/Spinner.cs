// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Displays one automatically advancing glyph from a built-in frame sequence.</summary>
public sealed class Spinner: Control
{
    private static readonly Rune[] _brailleFrames =
    [
        new('⠋'), new('⠙'), new('⠹'), new('⠸'), new('⠼'),
        new('⠴'), new('⠦'), new('⠧'), new('⠇'), new('⠏'),
    ];
    private static readonly Rune[] _denseBrailleFrames =
    [
        new('⣿'), new('⣷'), new('⣯'), new('⣟'),
        new('⡿'), new('⢿'), new('⣻'), new('⣽'),
    ];
    private static readonly Rune[] _asciiFrames =
    [
        new('|'), new('/'), new('-'), new('\\'),
    ];
    private int _frameIndex;
    private DispatcherTimer? _timer;

    /// <summary>Initializes a playing one-cell Braille spinner.</summary>
    public Spinner()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        IsHitTestVisible = false;
    }

    #region Playback properties

    /// <summary>Gets or sets the built-in frame sequence.</summary>
    /// <remarks>Changing the pattern resets playback to its first frame.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached spinner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The spinner is disposed.</exception>
    public SpinnerPattern Pattern
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The spinner pattern is unknown.");
            }

            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            _frameIndex = 0;
            NotifyPropertyChanged(nameof(Pattern), ChangeImpact.Render);
        }
    } = SpinnerPattern.Braille;

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

            if (_timer is { } timer)
            {
                timer.Interval = value;
            }

            field = value;
            NotifyPropertyChanged(nameof(Interval), ChangeImpact.None);
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
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
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

    #region Lifetime and timing

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        Debug.Assert(Dispatcher is not null, "An attached spinner owns a dispatcher.");
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

    private ReadOnlySpan<Rune> Frames() => Pattern switch
    {
        SpinnerPattern.Braille => _brailleFrames,
        SpinnerPattern.DenseBraille => _denseBrailleFrames,
        SpinnerPattern.Ascii => _asciiFrames,
        _ => throw new UnreachableException(),
    };

    private void OnTick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!IsPlaying || !EffectiveIsVisible)
        {
            return;
        }

        var frames = Frames();
        _frameIndex = (_frameIndex + 1) % frames.Length;
        Invalidate(ChangeImpact.Render);
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
