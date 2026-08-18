// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Supplies the children of a <see cref="TreeViewItem"/> on demand.</summary>
/// <remarks>
/// Assign an instance to <see cref="TreeViewItem.ChildSource"/> to make that item load its children
/// asynchronously the first time it expands, instead of requiring every descendant to be authored
/// up front through <see cref="TreeViewItem.Children"/>.
/// </remarks>
[PublicAPI]
public interface ITreeViewChildSource
{
    /// <summary>Gets the children of the item described by <paramref name="context"/>.</summary>
    /// <param name="context">The request context, captured on the owning dispatcher.</param>
    /// <param name="cancellationToken">
    /// The token observed while the request is outstanding. It is signalled when the requesting
    /// item collapses or is superseded by a newer request before this one completes.
    /// </param>
    /// <returns>The described children, in the order they should be materialized.</returns>
    public Task<IReadOnlyList<TreeViewChildDescription>> GetChildrenAsync(
        TreeViewChildContext context,
        CancellationToken cancellationToken);
}
