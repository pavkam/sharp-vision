// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Threading.Channels;

/// <summary>Records host-preflight resize input and resize-source disposal.</summary>
internal sealed class ConsoleApplicationResizeSource: IResizeSource
{
    private readonly Channel<Dimensions> _resizes = Channel.CreateUnbounded<Dimensions>();
    private readonly List<string>? _disposalOrder;

    /// <summary>Initializes a recorder with an optional shared disposal-order log.</summary>
    /// <param name="disposalOrder">The optional shared log.</param>
    internal ConsoleApplicationResizeSource(List<string>? disposalOrder = null) =>
        _disposalOrder = disposalOrder;

    /// <summary>Gets the number of disposal calls.</summary>
    internal int Disposals { get; private set; }

    /// <summary>Gets or sets the exact disposal failure raised after recording the attempt.</summary>
    internal Exception? DisposalFailure { get; set; }

    /// <summary>Queues one immutable resize record.</summary>
    /// <param name="value">The dimensions returned by the next read.</param>
    internal void QueueResize(Dimensions value) =>
        _resizes.Writer.TryWrite(value).ShouldBeTrue();

    /// <inheritdoc/>
    public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken) =>
        await _resizes.Reader.ReadAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Disposals++;
        _disposalOrder?.Add("resize");
        _ = _resizes.Writer.TryComplete();

        return DisposalFailure is { } failure
            ? ValueTask.FromException(failure)
            : ValueTask.CompletedTask;
    }
}
