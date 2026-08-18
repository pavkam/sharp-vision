// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using SharpVision.Controls.Collections;

/// <summary>Provides deterministic in-memory child snapshots to tree-view async-loading tests.</summary>
internal sealed class FakeTreeViewChildSource: ITreeViewChildSource
{
    // TreeViewChildContext.Key is null for a caller-authored root, but Dictionary<TKey, TValue>
    // rejects a null key - every lookup normalizes null to this private sentinel instead.
    private static readonly object _rootKey = new();

    private readonly Dictionary<object, TreeViewChildDescription[]> _children = [];
    private readonly Dictionary<object, Queue<Func<Task<IReadOnlyList<TreeViewChildDescription>>>>> _next = [];
    private readonly List<object?> _requests = [];

    /// <summary>Gets the key requested by every call so far, in request order.</summary>
    internal IReadOnlyList<object?> Requests => _requests;

    /// <summary>Gets the managed thread id observed by the most recent request.</summary>
    internal int LastRequestThreadId { get; private set; }

    /// <summary>Registers or replaces the children answered for one key.</summary>
    /// <param name="key">The non-null key, or null for a caller-authored root.</param>
    /// <param name="children">The non-null child descriptions.</param>
    internal void AddChildren(object? key, params TreeViewChildDescription[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        _children[key ?? _rootKey] = children;
    }

    /// <summary>Defers the next request for one key until the returned completion is resolved.</summary>
    /// <param name="key">The non-null key, or null for a caller-authored root.</param>
    /// <returns>The controllable completion; it deliberately ignores request cancellation to test stale guards.</returns>
    internal TaskCompletionSource<IReadOnlyList<TreeViewChildDescription>> DeferNext(object? key)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<TreeViewChildDescription>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(key, () => completion.Task);
        return completion;
    }

    /// <summary>Fails the next request for one key with the supplied exception.</summary>
    /// <param name="key">The non-null key, or null for a caller-authored root.</param>
    /// <param name="exception">The non-null exception to observe.</param>
    internal void FailNext(object? key, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Enqueue(key, () => Task.FromException<IReadOnlyList<TreeViewChildDescription>>(exception));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TreeViewChildDescription>> GetChildrenAsync(
        TreeViewChildContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        LastRequestThreadId = Environment.CurrentManagedThreadId;
        _requests.Add(context.Key);
        var normalized = context.Key ?? _rootKey;

        if (_next.TryGetValue(normalized, out var requests) && requests.TryDequeue(out var request))
        {
            return request();
        }

        if (!_children.TryGetValue(normalized, out var children))
        {
            throw new InvalidOperationException(
                $"Fake tree view child source has no children registered for '{context.Key}'.");
        }

        IReadOnlyList<TreeViewChildDescription> result = [.. children];
        return Task.FromResult(result);
    }

    private void Enqueue(object? key, Func<Task<IReadOnlyList<TreeViewChildDescription>>> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = key ?? _rootKey;

        if (!_next.TryGetValue(normalized, out var requests))
        {
            requests = new Queue<Func<Task<IReadOnlyList<TreeViewChildDescription>>>>();
            _next.Add(normalized, requests);
        }

        requests.Enqueue(request);
    }
}
