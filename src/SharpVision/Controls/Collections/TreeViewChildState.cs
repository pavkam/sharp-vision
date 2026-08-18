// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Describes the asynchronous child-loading lifecycle of one <see cref="TreeViewItem"/>.</summary>
[PublicAPI]
public enum TreeViewChildState
{
    /// <summary>The item has no <see cref="TreeViewItem.ChildSource"/> and no children. It never
    /// offers to expand.</summary>
    Leaf,

    /// <summary>
    /// The item's <see cref="TreeViewItem.Children"/> reflect the most recently committed load, or
    /// were authored directly by the caller. A loaded item with zero children still reports
    /// <see cref="Loaded"/> rather than <see cref="Leaf"/> - only an explicit
    /// <see cref="TreeViewChildPresence.Leaf"/> answer, or never having had a source, means leaf.
    /// </summary>
    Loaded,

    /// <summary>The item has a <see cref="TreeViewItem.ChildSource"/> but has not yet requested
    /// its children.</summary>
    Unloaded,

    /// <summary>A child request is in flight.</summary>
    Loading,

    /// <summary>
    /// The most recently attempted child request failed or was rejected by validation. Children
    /// committed by an earlier successful load, if any, are retained unchanged.
    /// </summary>
    LoadFailed
}
