// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Threading.Channels;

/// <summary>
/// Coalesces Unix SIGWINCH wakeups and reads newest cell/pixel dimensions.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[PublicAPI]
public sealed class UnixResizeSource: IResizeSource
{
    private readonly int _fileDescriptor;
    private readonly Channel<bool> _changes;
    private readonly PosixSignalRegistration _registration;
    private int _disposed;
    private int _reading;

    /// <summary>Initializes a Unix resize source for one terminal descriptor.</summary>
    /// <param name="fileDescriptor">The non-negative terminal file descriptor.</param>
    /// <exception cref="ArgumentOutOfRangeException">The descriptor is negative.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform is not Linux or macOS.</exception>
    public UnixResizeSource(int fileDescriptor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileDescriptor);

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "SIGWINCH terminal resize is supported only on Linux and macOS.");
        }

        _fileDescriptor = fileDescriptor;
        _changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _registration = PosixSignalRegistration.Create(
            PosixSignal.SIGWINCH,
            context =>
            {
                context.Cancel = true;
                _ = _changes.Writer.TryWrite(true);
            });
        _ = _changes.Writer.TryWrite(true);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Another read is pending.</exception>
    /// <exception cref="ObjectDisposedException">The source is disposed.</exception>
    public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0)
        {
            throw new InvalidOperationException("A resize read is already pending.");
        }

        try
        {
            try
            {
                _ = await _changes.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException exception) when (exception.InnerException is ObjectDisposedException disposed)
            {
                // DisposeAsync completes the channel with an explicit ObjectDisposedException so
                // a concurrently pending read observes the same exception every other entry point
                // on this type already throws once disposed; the channel API always wraps that in
                // a ChannelClosedException, so unwrap it back to the documented contract here.
                ExceptionDispatchInfo.Capture(disposed).Throw();
                throw;
            }

            while (_changes.Reader.TryRead(out _))
            {
            }

            return RuntimeInterop.GetDimensions(_fileDescriptor);
        }
        finally
        {
            Volatile.Write(ref _reading, 0);
        }
    }

    /// <inheritdoc/>
    public bool TryReadCurrent(out Dimensions value)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        // The constructor seeds one wakeup so a consumer that only ever calls ReadAsync still gets
        // an initial observation. This synchronous snapshot *is* that initial observation, so the
        // seed is consumed here: leaving it buffered made the very next ReadAsync return
        // immediately with the same dimensions, publishing the startup size twice - a second
        // full layout and render of an unchanged geometry on every startup.
        //
        // Drained before the measurement, never after. A SIGWINCH landing in the window between
        // the two is then still reflected in the value returned here, whereas draining afterwards
        // would discard that wakeup while returning the older size, losing the resize outright.
        while (_changes.Reader.TryRead(out _))
        {
        }

        value = RuntimeInterop.GetDimensions(_fileDescriptor);
        return true;
    }

    /// <summary>Stops signal observation and completes pending wakeup production.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _registration.Dispose();

            // Complete with an explicit exception so a ReadAsync call blocked concurrently on
            // this dispose surfaces the same ObjectDisposedException every other entry point on
            // this type already throws once disposed, instead of an unmapped
            // ChannelClosedException the interface never promises.
            _ = _changes.Writer.TryComplete(new ObjectDisposedException(nameof(UnixResizeSource)));
        }

        return ValueTask.CompletedTask;
    }
}
