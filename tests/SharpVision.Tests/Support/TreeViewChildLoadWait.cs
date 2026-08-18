// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Waits for one <see cref="TreeViewItem"/>'s asynchronous child-loading state to settle,
/// signalled by <see cref="TreeViewItem.ChildStateChanged"/> rather than a fixed poll interval or a
/// timing-dependent guess about when a background continuation posts its commit back.</summary>
internal static class TreeViewChildLoadWait
{
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Waits until <paramref name="predicate"/> holds, checked once before subscribing and
    /// once again after - the state may already satisfy it, or may change in the window between the
    /// first check and the subscription taking effect - and on every subsequent
    /// <see cref="TreeViewItem.ChildStateChanged"/> notification.</summary>
    /// <param name="item">The item whose child-state transitions may satisfy the predicate.</param>
    /// <param name="predicate">The condition to await.</param>
    /// <param name="cancellationToken">The cancellation token bounding the wait.</param>
    /// <returns>A task completed once the predicate holds.</returns>
    internal static async Task UntilAsync(
        TreeViewItem item,
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        if (predicate())
        {
            return;
        }

        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(object? sender, TreeViewChildStateChangedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;

            if (predicate())
            {
                _ = settled.TrySetResult();
            }
        }

        item.ChildStateChanged += OnChanged;

        try
        {
            if (predicate())
            {
                return;
            }

            await settled.Task.WaitAsync(_defaultTimeout, cancellationToken);
        }
        finally
        {
            item.ChildStateChanged -= OnChanged;
        }
    }
}
