// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Threading;

/// <summary>Executes one fire-and-observe dispatcher callback.</summary>
internal sealed class PostWork: Work
{
    private readonly Action _action;
    private readonly Action? _onCancelled;

    /// <summary>Initializes one validated fire-and-observe callback.</summary>
    /// <param name="action">The non-null callback to execute.</param>
    /// <param name="onCancelled">
    /// An optional callback invoked instead of <paramref name="action"/> when this work is
    /// cancelled by dispatcher shutdown before it runs. Ordinary <see cref="Dispatcher.Post(Action)"/>
    /// callers have no way to observe this outcome; this seam exists only for callers - such as
    /// <see cref="DispatcherTimer"/> - that must release state they set before posting even if
    /// the post never executes, instead of that state staying latched forever.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public PostWork(Action action, Action? onCancelled = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
        _onCancelled = onCancelled;
    }

    /// <inheritdoc/>
    public override void Execute() => _action();

    /// <inheritdoc/>
    public override void Cancel() => _onCancelled?.Invoke();
}
