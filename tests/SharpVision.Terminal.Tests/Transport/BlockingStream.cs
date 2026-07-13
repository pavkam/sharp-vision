// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

using System.Text;

/// <summary>Blocks writes to prove serialized transport access.</summary>
internal sealed class BlockingStream: MemoryStream
{
    private readonly TaskCompletionSource _firstStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _active;

    /// <summary>Gets a task completed after the first write starts.</summary>
    internal Task FirstStarted => _firstStarted.Task;

    /// <summary>Gets the maximum concurrent write count.</summary>
    internal int MaximumActive { get; private set; }

    /// <summary>Gets copied decoded writes.</summary>
    internal List<string> Writes { get; } = [];

    /// <summary>Releases every blocked write.</summary>
    internal void Release() => _release.TrySetResult();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        _active++;
        MaximumActive = Math.Max(MaximumActive, _active);
        _ = _firstStarted.TrySetResult();
        await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        Writes.Add(Encoding.UTF8.GetString(buffer.Span));
        _active--;
    }
}
