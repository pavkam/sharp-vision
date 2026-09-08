// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Manages a repeating dispatcher-timer lifecycle for one owner-bound helper.</summary>
/// <remarks>
/// <see cref="ControlTimer"/> is the shared seam behind any control feature that needs a
/// dispatcher-affine repeating callback whose lifetime follows the owning <see cref="ControlBase"/>'s
/// dispatcher attachment - a blinking caret, a marquee, an auto-dismiss delay, or an entrance or
/// fade animation. Construct one and register it with
/// <see cref="ControlBase.RegisterAttachmentParticipant"/> from the owner's constructor: the
/// underlying <see cref="DispatcherTimer"/> is released when the owner detaches and disposed
/// together with the owner. It is allocated lazily rather than eagerly at attachment - only once
/// <see cref="IsPlaying"/> is true while attached - so a timer an owner never ends up starting
/// (an animation the caller never enables, a delay that never fires) never allocates a dispatcher
/// resource at all. Every tick runs on the owner's dispatcher, matching every other control
/// mutation.
/// </remarks>
public sealed class ControlTimer: IControlAttachmentParticipant
{
    private readonly Action _onTick;
    private readonly Func<bool>? _shouldTick;
    private Dispatcher? _dispatcher;
    private DispatcherTimer? _timer;

    /// <summary>Initializes one stopped timer bound to the given interval and tick callback.</summary>
    /// <param name="interval">The interval between ticks once playing and attached.</param>
    /// <param name="onTick">The non-null callback invoked on the owner's dispatcher for each eligible tick.</param>
    /// <param name="shouldTick">An optional gate re-checked before every tick; returning false stops
    /// the underlying timer immediately for that tick and every later one until <see cref="EnsureRunning"/>
    /// explicitly restarts it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onTick"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is outside the
    /// supported dispatcher-timer range.</exception>
    public ControlTimer(TimeSpan interval, Action onTick, Func<bool>? shouldTick = null)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        DispatcherTimer.ValidateInterval(interval, nameof(interval));
        Interval = interval;
        _onTick = onTick;
        _shouldTick = shouldTick;
    }

    /// <summary>Gets or sets the interval between ticks.</summary>
    /// <remarks>
    /// Assigning while a dispatcher timer is already allocated rearms it with the new interval
    /// immediately, starting one complete new period from now rather than waiting for the period
    /// already in flight to finish. Assigning before that allocation - while detached, or while
    /// attached but never yet started - only stores the value for when a dispatcher timer is
    /// actually allocated.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the supported
    /// dispatcher-timer range.</exception>
    public TimeSpan Interval
    {
        get;
        set
        {
            DispatcherTimer.ValidateInterval(value, nameof(value));
            field = value;

            if (_timer is { } timer)
            {
                timer.Interval = value;
            }
        }
    }

    /// <summary>Gets or sets whether this timer plays while attached.</summary>
    /// <remarks>
    /// Setting true allocates the underlying dispatcher timer on first use (if attached) and starts
    /// it; setting false stops it without releasing it, so a subsequent true resumes cheaply.  The
    /// value persists across detachment and is reapplied on the next attachment: a timer left
    /// playing while its owner briefly detaches resumes automatically, reallocating its dispatcher
    /// timer, once reattached. Setting true while still detached only records the desired state for
    /// that next attachment.
    /// </remarks>
    public bool IsPlaying
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            if (value)
            {
                EnsureUnderlyingTimerAllocated();
                _timer?.Start();
            }
            else
            {
                _timer?.Stop();
            }
        }
    }

    /// <summary>Gets whether this timer currently owns a live dispatcher timer instance.</summary>
    /// <remarks>Exposed for test seams proving that a caller-driven release (<see cref="OnOwnerDetached"/>
    /// invoked directly by an owner that models a logical unavailability distinct from real dispatcher
    /// detachment, such as a merely hidden popup) actually releases the dispatcher resource rather
    /// than merely pausing it, and that a timer never started never allocated one in the first
    /// place.</remarks>
    internal bool HasUnderlyingTimer => _timer is not null;

    /// <summary>Gets the underlying dispatcher timer's current Tick subscriber count, or zero when
    /// no dispatcher timer is currently allocated.</summary>
    /// <remarks>Exposed for duplicate-subscription test seams: this timer wires exactly one internal
    /// handler per allocation, so a live count above one would indicate a subscription leak.</remarks>
    internal int TickSubscribers => _timer?.TickSubscribers ?? 0;

    /// <summary>Stops playback if currently playing, equivalent to setting <see cref="IsPlaying"/> to false.</summary>
    /// <remarks>
    /// Provided for an owner that models its timer as an imperative one-shot or restart-driven
    /// action - an auto-dismiss delay, a completed entrance animation - rather than as a persistent
    /// toggle a caller flips directly through <see cref="IsPlaying"/>. This pauses the dispatcher
    /// timer without releasing it; call <see cref="OnOwnerDetached"/> instead to also release the
    /// underlying resource between uses.
    /// </remarks>
    public void Stop() => IsPlaying = false;

    /// <summary>Restarts playback after an internal <c>shouldTick</c> stop, without changing <see cref="IsPlaying"/>.</summary>
    /// <remarks>
    /// A tick observing <c>shouldTick</c> return false stops the underlying dispatcher timer
    /// directly, leaving <see cref="IsPlaying"/> at its last caller-assigned value. Call this from a
    /// render or recovery pass to resume once the gated condition is expected to hold again; it is a
    /// silent no-op before a dispatcher timer has ever been allocated, while already running, or
    /// while <see cref="IsPlaying"/> is false.
    /// </remarks>
    public void EnsureRunning()
    {
        if (IsPlaying && _timer is { IsRunning: false })
        {
            _timer.Start();
        }
    }

    /// <summary>Records the owner's committed dispatcher, allocating and starting the underlying
    /// dispatcher timer immediately only when <see cref="IsPlaying"/> is already true.</summary>
    /// <param name="dispatcher">The exact committed owner dispatcher.</param>
    public void OnOwnerAttached(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        if (IsPlaying)
        {
            EnsureUnderlyingTimerAllocated();
            _timer?.Start();
        }
    }

    /// <summary>Releases the underlying dispatcher timer, if one was ever allocated.</summary>
    /// <remarks>
    /// The framework calls this automatically when the owner's real dispatcher attachment ends. An
    /// owner may also call it directly to force an early release for a logical unavailability that
    /// does not itself detach the dispatcher (a hidden popup, for example) or to fully retire a
    /// bounded-duration timer between uses (a completed fade or auto-dismiss delay) rather than
    /// merely pausing it; a later call to <see cref="OnOwnerAttached"/> restores the dispatcher
    /// reference this needs to allocate again.
    /// </remarks>
    public void OnOwnerDetached() => Release();

    /// <summary>Releases the underlying dispatcher timer as part of the owner's final disposal.</summary>
    public void Dispose() => Release();

    private void EnsureUnderlyingTimerAllocated()
    {
        if (_timer is not null || _dispatcher is not { } dispatcher)
        {
            return;
        }

        _timer = new DispatcherTimer(dispatcher, Interval);
        _timer.Tick += OnTick;
    }

    private void Release()
    {
        _dispatcher = null;
        var timer = _timer;
        _timer = null;

        if (timer is null)
        {
            return;
        }

        timer.Tick -= OnTick;
        timer.Dispose();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_shouldTick is not null && !_shouldTick())
        {
            _timer?.Stop();
            return;
        }

        _onTick();
    }
}
