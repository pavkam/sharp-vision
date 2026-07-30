// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Polls portable console cell dimensions at a finite non-spinning interval.</summary>
[PublicAPI]
public sealed class ConsoleResizeSource: IResizeSource
{
    private readonly TimeSpan _interval;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Size?> _readCells;
    private Size? _last;
    private int _disposed;
    private int _reading;

    /// <summary>Initializes a portable cell-only resize source.</summary>
    /// <param name="interval">The positive finite polling interval.</param>
    /// <param name="timeProvider">The polling clock, or null for system time.</param>
    /// <exception cref="ArgumentOutOfRangeException">The interval is not positive and finite.</exception>
    public ConsoleResizeSource(TimeSpan interval, TimeProvider? timeProvider = null)
        : this(interval, timeProvider, readCells: null)
    {
    }

    /// <summary>
    /// Initializes a portable cell-only resize source with a substitutable measurement boundary.
    /// Internal because production callers always measure the real console; tests use this to
    /// prove that a measurement failure (readCells returning null) is never published as a
    /// resize, without needing to force a real Console.WindowWidth/Height exception.
    /// </summary>
    internal ConsoleResizeSource(TimeSpan interval, TimeProvider? timeProvider, Func<Size?>? readCells)
    {
        if (interval <= TimeSpan.Zero || interval == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "The polling interval must be positive and finite.");
        }

        _interval = interval;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _readCells = readCells ?? ReadConsoleCells;
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
                // A failed measurement is distinct from an actual 0x0 suspend: ReadCells
                // returns null only when the console API itself threw, never when it
                // successfully reported zero cells. Treating a transient measurement failure
                // as a real resize would publish a spurious suspend to the sink (see #98).
                if (_readCells() is { } current && _last != current)
                {
                    _last = current;
                    return new Dimensions(current);
                }

                await Task.Delay(_interval, _timeProvider, cancellationToken)
                    .ConfigureAwait(false);
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

        if (_readCells() is { } current)
        {
            value = new Dimensions(current);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Marks the source disposed. Any caller token still owns pending-read cancellation.</summary>
    public ValueTask DisposeAsync()
    {
        Volatile.Write(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads the current cell dimensions, or null when the console API itself failed.</summary>
    private static Size? ReadConsoleCells()
    {
        try
        {
            return new Size(
                Math.Max(0, Console.WindowWidth),
                Math.Max(0, Console.WindowHeight));
        }
        catch (IOException)
        {
            return null;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }
}
