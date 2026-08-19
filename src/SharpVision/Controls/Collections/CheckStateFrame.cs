// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>
/// Carries one node's partial three-state aggregate during iterative check-state evaluation.
/// </summary>
/// <remarks>
/// Mutation is intrinsic to its role: it is a walk cursor plus an accumulator, advanced once per
/// visited child and written back into the caller's explicit stack. Evaluating check state
/// iteratively is what keeps a deep checkable chain from exhausting the process stack.
/// </remarks>
internal struct CheckStateFrame
{
#pragma warning disable IDE0032 // A readonly capture used by both the cursor and the aggregate.
    private readonly TreeViewItem _item;
#pragma warning restore IDE0032
    private int _cursor;
    private bool? _common;
    private bool _hasState;
    private bool _hasCheckableChild;
    private bool _indeterminate;

    /// <summary>Initializes a frame positioned before the first child of one item.</summary>
    /// <param name="item">The non-null checkable item with children.</param>
    internal CheckStateFrame(TreeViewItem item)
    {
        Debug.Assert(item is not null, "A walk frame always describes an item.");

        _item = item;
        _cursor = 0;
    }

    /// <summary>Gets the item this frame is aggregating.</summary>
    internal readonly TreeViewItem Item => _item;

    /// <summary>Advances to the next checkable child, if any remains.</summary>
    /// <param name="child">Receives the next checkable child on success.</param>
    /// <returns><see langword="true"/> when a child was taken; otherwise <see langword="false"/>.</returns>
    internal bool TryTakeNextCheckableChild([NotNullWhen(true)] out TreeViewItem? child)
    {
        // A resolved indeterminate cannot be changed by later siblings, so stop walking them.
        while (!_indeterminate && _cursor < _item.Children.Count)
        {
            var candidate = _item.Children[_cursor];
            _cursor++;

            if (candidate.IsCheckable)
            {
                child = candidate;
                return true;
            }
        }

        child = null;
        return false;
    }

    /// <summary>Folds one resolved child state into this node's aggregate.</summary>
    /// <param name="state">The child's effective state.</param>
    internal void Accumulate(bool? state)
    {
        _hasCheckableChild = true;

        if (state is null || (_hasState && _common != state))
        {
            _indeterminate = true;
            return;
        }

        if (!_hasState)
        {
            _common = state;
            _hasState = true;
        }
    }

    /// <summary>Produces this node's effective state once every checkable child was folded in.</summary>
    /// <returns>The aggregate, or the node's own state when it has no checkable child.</returns>
    [Pure]
    internal readonly bool? Resolve() =>
        _indeterminate ? null
        : _hasCheckableChild ? _common
        : _item.OwnCheckState;
}
