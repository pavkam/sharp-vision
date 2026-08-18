// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Declares whether a described child may itself have children.</summary>
[PublicAPI]
public enum TreeViewChildPresence
{
    /// <summary>
    /// The described child may have its own children, discoverable through the same child source
    /// once it expands. This is the default - a child source must opt in to <see cref="Leaf"/>
    /// rather than have leaf presence inferred from an empty answer.
    /// </summary>
    MayHaveChildren,

    /// <summary>The described child has no children and never requests any.</summary>
    Leaf
}
