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
            _ = await _changes.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            while (_changes.Reader.TryRead(out _))
            {
            }

            return Native.GetDimensions(_fileDescriptor);
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
        value = Native.GetDimensions(_fileDescriptor);
        return true;
    }

    /// <summary>Stops signal observation and completes pending wakeup production.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _registration.Dispose();
            _ = _changes.Writer.TryComplete();
        }

        return ValueTask.CompletedTask;
    }
}
