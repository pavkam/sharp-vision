// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Stores one compact mutable construction node in a described-key byte trie.</summary>
internal struct KeySequenceNode
{
    /// <summary>Initializes an empty node.</summary>
    public KeySequenceNode()
    {
        FirstEdge = -1;
        BindingIndex = -1;
    }

    /// <summary>Gets or sets the first linked edge index, or -1.</summary>
    public int FirstEdge { get; set; }

    /// <summary>Gets or sets the terminal binding index, or -1.</summary>
    public int BindingIndex { get; set; }
}
