// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Describes the item an <see cref="ITreeViewChildSource"/> is being asked to expand.</summary>
/// <remarks>
/// Captured on the owning dispatcher before a request starts, so a source's background work never
/// touches a dispatcher-affine <see cref="TreeViewItem"/>.
/// </remarks>
[PublicAPI]
public sealed class TreeViewChildContext
{
    /// <summary>Initializes an immutable request context.</summary>
    /// <param name="key">
    /// The stable key of the item being expanded, or null when the source was assigned directly to
    /// a caller-authored root rather than materialized from a prior <see cref="TreeViewChildDescription"/>.
    /// </param>
    /// <param name="header">The non-null display text of the item being expanded.</param>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is null.</exception>
    public TreeViewChildContext(object? key, string header)
    {
        ArgumentNullException.ThrowIfNull(header);
        Key = key;
        Header = header;
    }

    /// <summary>
    /// Gets the stable key of the item being expanded, or null for a caller-authored root the
    /// source was assigned to directly.
    /// </summary>
    public object? Key { get; }

    /// <summary>Gets the display text of the item being expanded.</summary>
    public string Header { get; }
}
