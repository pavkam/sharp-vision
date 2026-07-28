// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Stores one immutable compact trie edge and sibling link.</summary>
internal readonly record struct KeySequenceEdge
{
    /// <summary>Initializes one byte transition.</summary>
    public KeySequenceEdge(byte value, int child, int next)
    {
        Debug.Assert(child > 0, "A trie edge targets a non-root node.");
        Debug.Assert(next >= -1, "A sibling edge index is non-negative or absent.");

        Value = value;
        Child = child;
        Next = next;
    }

    /// <summary>Gets the transition byte.</summary>
    public byte Value { get; }

    /// <summary>Gets the target node index.</summary>
    public int Child { get; }

    /// <summary>Gets the next sibling edge index, or -1.</summary>
    public int Next { get; }
}
