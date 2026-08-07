// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Describes a committed JsonView selection change using RFC 6901 JSON Pointers.</summary>
[PublicAPI]
public sealed class JsonViewSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes a selection transition.</summary>
    /// <param name="previousPath">The previously selected pointer, or null.</param>
    /// <param name="path">The newly selected pointer, or null.</param>
    public JsonViewSelectionChangedEventArgs(string? previousPath, string? path)
    {
        PreviousPath = previousPath;
        Path = path;
    }

    /// <summary>Gets the previously selected JSON Pointer, or null.</summary>
    public string? PreviousPath { get; }

    /// <summary>Gets the newly selected JSON Pointer, or null.</summary>
    public string? Path { get; }
}
