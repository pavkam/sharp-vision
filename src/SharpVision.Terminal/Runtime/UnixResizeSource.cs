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
    private readonly Func<int, Dimensions> _getDimensions;
    private readonly Channel<bool> _changes;
    private readonly PosixSignalRegistration _registration;
    private int _disposed;
    private int _reading;

    /// <summary>Initializes a Unix resize source for one terminal descriptor.</summary>
    /// <param name="fileDescriptor">The non-negative terminal file descriptor.</param>
    /// <exception cref="ArgumentOutOfRangeException">The descriptor is negative.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform is not Linux or macOS.</exception>
    public UnixResizeSource(int fileDescriptor)
        : this(fileDescriptor, getDimensions: null)
    {
    }

    /// <summary>
    /// Initializes a Unix resize source with a substitutable measurement boundary. Internal
    /// because production callers always measure the real terminal descriptor; tests use this to
    /// prove that a measurement failure (getDimensions throwing) is never published as a resize,
    /// without needing to force a real TIOCGWINSZ ioctl failure.
    /// </summary>
    /// <param name="fileDescriptor">The non-negative terminal file descriptor.</param>
    /// <param name="getDimensions">The dimensions reader, or null to measure the real terminal
    /// descriptor via <see cref="RuntimeInterop.GetDimensions"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The descriptor is negative.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform is not Linux or macOS.</exception>
    internal UnixResizeSource(int fileDescriptor, Func<int, Dimensions>? getDimensions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileDescriptor);

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "SIGWINCH terminal resize is supported only on Linux and macOS.");
        }

        _fileDescriptor = fileDescriptor;
        _getDimensions = getDimensions ?? RuntimeInterop.GetDimensions;
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
            while (true)
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

                // A failed measurement is distinct from an actual 0x0 suspend: TryMeasure returns
                // false only when the ioctl itself failed, never when it successfully reported a
                // suspended 0x0 window. Treating a transient measurement failure as a real resize
                // would tear the session down for a condition the transport would otherwise
                // report as its own connection-level fault; loop back and wait for the next
                // SIGWINCH wakeup instead.
                if (TryMeasure(out var dimensions))
                {
                    return dimensions;
                }
            }
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

        return TryMeasure(out value);
    }

    /// <summary>Reads the current dimensions, or fails when the terminal measurement itself
    /// fails.</summary>
    /// <param name="value">Receives the measured dimensions on success.</param>
    /// <returns>Whether the measurement succeeded.</returns>
    private bool TryMeasure(out Dimensions value)
    {
        try
        {
            value = _getDimensions(_fileDescriptor);
            return true;
        }
        catch (IOException)
        {
            value = default;
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            value = default;
            return false;
        }
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
