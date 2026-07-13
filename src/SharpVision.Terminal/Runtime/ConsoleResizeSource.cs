namespace SharpVision.Terminal.Runtime;

using SharpVision.Terminal.Geometry;

/// <summary>Polls portable console cell dimensions at a finite non-spinning interval.</summary>
public sealed class ConsoleResizeSource: IResizeSource
{
    private readonly TimeSpan _interval;
    private readonly TimeProvider _timeProvider;
    private Size? _last;
    private int _disposed;
    private int _reading;

    /// <summary>Initializes a portable cell-only resize source.</summary>
    /// <param name="interval">The positive finite polling interval.</param>
    /// <param name="timeProvider">The polling clock, or null for system time.</param>
    /// <exception cref="ArgumentOutOfRangeException">The interval is not positive and finite.</exception>
    public ConsoleResizeSource(TimeSpan interval, TimeProvider? timeProvider = null)
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
                var current = ReadCells();

                if (_last != current)
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

    /// <summary>Marks the source disposed. Any caller token still owns pending-read cancellation.</summary>
    public ValueTask DisposeAsync()
    {
        Volatile.Write(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    private static Size ReadCells()
    {
        try
        {
            return new Size(
                Math.Max(0, Console.WindowWidth),
                Math.Max(0, Console.WindowHeight));
        }
        catch (IOException)
        {
            return default;
        }
        catch (PlatformNotSupportedException)
        {
            return default;
        }
    }
}
