// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>One process positioned in the live parent/child hierarchy.</summary>
public sealed class ProcessNode
{
    private readonly List<ProcessNode> _children = [];

    /// <summary>Initializes a tree node wrapping one process sample with no children yet.</summary>
    /// <param name="sample">The non-null process snapshot this node presents.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sample"/> is null.</exception>
    public ProcessNode(ProcessSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        Sample = sample;
    }

    /// <summary>Gets the process snapshot this node presents.</summary>
    public ProcessSample Sample { get; }

    /// <summary>Gets the direct children discovered for this process, in <c>ps</c>'s own order.</summary>
    public IReadOnlyList<ProcessNode> Children => _children;

    /// <summary>Attaches one already-built subtree as a direct child.</summary>
    /// <param name="child">The non-null child node.</param>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    internal void AddChild(ProcessNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        _children.Add(child);
    }

    /// <summary>Orders this node's own direct children by process ID.</summary>
    internal void SortChildren() => _children.Sort(static (left, right) => left.Sample.Pid.CompareTo(right.Sample.Pid));
}
