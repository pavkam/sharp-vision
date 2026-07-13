// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Buffers;
using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Transport;

using TerminalCapabilities = Capabilities.Capabilities;

/// <summary>
/// Owns terminal mode leases and serializes input, resize, closure, and cleanup.
/// </summary>
public sealed class Session: IAsyncDisposable
{
    private readonly ITransport _transport;
    private readonly IResizeSource _resize;
    private readonly ISink _sink;
    private readonly Options _options;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<Lease> _leases = [];
    private int _disposed;
    private int _running;

    /// <summary>Initializes a session that owns transport and resize-source disposal.</summary>
    /// <param name="transport">The non-null terminal transport.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="sink">The non-null ordered event sink.</param>
    /// <param name="options">Validated policy, or null for defaults.</param>
    /// <param name="timeProvider">Cleanup clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public Session(
        ITransport transport,
        IResizeSource resize,
        ISink sink,
        Options? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ArgumentNullException.ThrowIfNull(sink);
        _transport = transport;
        _resize = resize;
        _sink = sink;
        _options = options ?? new Options();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the first cleanup failure without replacing the primary failure.</summary>
    public Exception? LastCleanupException { get; private set; }

    /// <summary>Runs startup, ordered event delivery, and guaranteed reverse cleanup.</summary>
    /// <param name="cancellationToken">Cancels pending reads and resize waits.</param>
    /// <returns>The complete session operation.</returns>
    /// <exception cref="InvalidOperationException">The session is already running.</exception>
    /// <exception cref="ObjectDisposedException">The session is disposed.</exception>
    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            throw new InvalidOperationException("The terminal session is already running.");
        }

        Exception? primary = null;
        LastCleanupException = null;

        try
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetime.Token);
            await StartAsync(linked.Token).ConfigureAwait(false);
            await EventsAsync(linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primary = exception;

            if (exception is not OperationCanceledException)
            {
                try
                {
                    _sink.Fault(exception);
                }
                catch (Exception notificationException)
                {
                    LastCleanupException = notificationException;
                }
            }
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
            Volatile.Write(ref _running, 0);
        }

        if (primary is not null)
        {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }

        if (LastCleanupException is not null)
        {
            ExceptionDispatchInfo.Capture(LastCleanupException).Throw();
        }
    }

    /// <summary>Cancels active work and disposes owned resize and transport resources.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        await _resize.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    private async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        if (_options.AlternateScreen)
        {
            await EnableAsync(Lease.AlternateScreen, cancellationToken).ConfigureAwait(false);
        }

        if (_options.HideCursor)
        {
            await EnableAsync(Lease.Cursor, cancellationToken).ConfigureAwait(false);
        }

    }

    private async ValueTask EnableOptionalAsync(
        TerminalCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        if (_options.Focus && capabilities.FocusReporting.IsSupported)
        {
            await EnableAsync(Lease.Focus, cancellationToken).ConfigureAwait(false);
        }

        if (_options.Paste && capabilities.BracketedPaste.IsSupported)
        {
            await EnableAsync(Lease.Paste, cancellationToken).ConfigureAwait(false);
        }

        if (_options.Tracking.HasValue && MouseSupported(capabilities))
        {
            await EnableAsync(Lease.Mouse, cancellationToken).ConfigureAwait(false);
        }

        if (_options.Keyboard.HasValue && capabilities.KittyKeyboard.IsSupported)
        {
            await EnableAsync(Lease.Keyboard, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EnableAsync(Lease lease, CancellationToken cancellationToken)
    {
        _leases.Add(lease);
        await WriteAsync(lease, enabled: true, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EventsAsync(CancellationToken cancellationToken)
    {
        Input.Options inputOptions = _options.Input with
        {
            PixelMouse = _options.Coordinates == MouseCoordinates.Pixel,
        };
        Negotiator? negotiator = _options.Negotiation is null
            ? null
            : new Negotiator(_options.Negotiation, _timeProvider);
        IProtocolSink routeSink = negotiator is null
            ? _sink
            : new NegotiationSink(_sink, negotiator);
        using ProtocolRouter router = new(routeSink, inputOptions, _timeProvider);

        if (negotiator is null)
        {
            await EnableOptionalAsync(_options.Capabilities, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            ArrayBufferWriter<byte> queries = new();
            negotiator.Start(queries);
            await _transport.WriteAsync(queries.WrittenMemory, cancellationToken)
                .ConfigureAwait(false);
            await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(_options.ReadBufferSize);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? deadline = negotiator is null
            ? null
            : DelayUntilAsync(negotiator.Deadline, linked.Token);
        bool ready = negotiator is null;
        bool hasPendingResize = false;
        Dimensions pendingResize = default;

        try
        {
            Task<int> read = _transport.ReadAsync(
                buffer.AsMemory(0, _options.ReadBufferSize),
                linked.Token).AsTask();
            Task<Dimensions> resize = _resize.ReadAsync(linked.Token).AsTask();

            while (true)
            {
                Task completed = deadline is null
                    ? await Task.WhenAny(read, resize).ConfigureAwait(false)
                    : await Task.WhenAny(read, resize, deadline).ConfigureAwait(false);

                if (deadline is not null && ReferenceEquals(completed, deadline))
                {
                    await deadline.ConfigureAwait(false);
                    _ = negotiator!.Expire();
                    TerminalCapabilities capabilities = negotiator.Capabilities;
                    _sink.Profile(capabilities);
                    await EnableOptionalAsync(capabilities, linked.Token)
                        .ConfigureAwait(false);
                    ready = true;
                    deadline = null;

                    if (hasPendingResize)
                    {
                        _sink.Resize(in pendingResize);
                        hasPendingResize = false;
                    }

                    continue;
                }

                if (ReferenceEquals(completed, resize))
                {
                    Dimensions dimensions = await resize.ConfigureAwait(false);
                    router.SetCellMetrics(dimensions.CellMetrics);

                    if (ready)
                    {
                        _sink.Resize(in dimensions);
                    }
                    else
                    {
                        pendingResize = dimensions;
                        hasPendingResize = true;
                    }

                    resize = _resize.ReadAsync(linked.Token).AsTask();
                    continue;
                }

                int count = await read.ConfigureAwait(false);

                if (count == 0)
                {
                    router.Complete();

                    if (!ready)
                    {
                        _ = negotiator!.Complete();
                        _sink.Profile(negotiator.Capabilities);
                        ready = true;
                        deadline = null;

                        if (hasPendingResize)
                        {
                            _sink.Resize(in pendingResize);
                            hasPendingResize = false;
                        }
                    }

                    _sink.Closed();
                    return;
                }

                router.Route(buffer.AsSpan(0, count));

                if (!ready && negotiator!.IsComplete)
                {
                    TerminalCapabilities capabilities = negotiator.Capabilities;
                    _sink.Profile(capabilities);
                    await EnableOptionalAsync(capabilities, linked.Token)
                        .ConfigureAwait(false);
                    ready = true;
                    deadline = null;

                    if (hasPendingResize)
                    {
                        _sink.Resize(in pendingResize);
                        hasPendingResize = false;
                    }
                }

                read = _transport.ReadAsync(
                    buffer.AsMemory(0, _options.ReadBufferSize),
                    linked.Token).AsTask();
            }
        }
        finally
        {
            linked.Cancel();
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private Task DelayUntilAsync(DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        TimeSpan delay = deadline - _timeProvider.GetUtcNow();

        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        return Task.Delay(delay, _timeProvider, cancellationToken);
    }

    private async ValueTask CleanupAsync()
    {
        using CancellationTokenSource timeout = new(
            _options.CleanupTimeout,
            _timeProvider);

        for (int index = _leases.Count - 1; index >= 0; index--)
        {
            try
            {
                await WriteAsync(_leases[index], enabled: false, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LastCleanupException ??= exception;
            }
        }

        _leases.Clear();
    }

    private async ValueTask WriteAsync(
        Lease lease,
        bool enabled,
        CancellationToken cancellationToken)
    {
        ArrayBufferWriter<byte> destination = new();
        Writer writer = new(destination);

        switch (lease)
        {
            case Lease.AlternateScreen:
                Modes.AlternateScreen(writer, enabled);
                break;
            case Lease.Cursor:
                Modes.CursorVisible(writer, visible: !enabled);
                break;
            case Lease.Focus:
                Modes.FocusReporting(writer, enabled);
                break;
            case Lease.Paste:
                Modes.BracketedPaste(writer, enabled);
                break;
            case Lease.Mouse:
                Modes.Mouse(
                    writer,
                    _options.Tracking!.Value,
                    _options.Coordinates,
                    enabled);
                break;
            case Lease.Keyboard:
                if (enabled)
                {
                    Keyboard.Push(writer, _options.Keyboard!.Value);
                }
                else
                {
                    Keyboard.Pop(writer);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lease), lease, "The mode lease is unknown.");
        }

        await _transport.WriteAsync(destination.WrittenMemory, cancellationToken)
            .ConfigureAwait(false);
        await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool MouseSupported(TerminalCapabilities capabilities) =>
        _options.Coordinates == MouseCoordinates.Pixel
            ? capabilities.PixelMouse.IsSupported
            : capabilities.CellMouse.IsSupported;

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

}
