// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Manages a repeating dispatcher timer lifecycle for animated controls.</summary>
internal sealed class AnimationTimer: IControlAttachmentParticipant
{
    private DispatcherTimer? _timer;
    private readonly Action _onTick;
    private readonly Func<bool>? _shouldTick;

    public AnimationTimer(TimeSpan interval, Action onTick, Func<bool>? shouldTick = null)
    {
        Interval = interval;
        _onTick = onTick;
        _shouldTick = shouldTick;
    }

    public TimeSpan Interval
    {
        get;
        set
        {
            field = value;

            if (_timer is { } timer)
            {
                timer.Interval = value;
            }
        }
    }

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

            if (_timer is null)
            {
                return;
            }

            if (value)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }
    }

    public void EnsureRunning()
    {
        if (IsPlaying && _timer is { IsRunning: false })
        {
            _timer.Start();
        }
    }

    public void OnOwnerAttached(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(dispatcher, Interval);
        _timer.Tick += OnTick;

        if (IsPlaying)
        {
            _timer.Start();
        }
    }

    public void OnOwnerDetached() => Release();

    public void Dispose() => Release();

    private void Release()
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
