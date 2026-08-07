// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Text.Json;

/// <summary>Owns one parsed JSON syntax entry and its disclosure state.</summary>
internal sealed class JsonViewNode
{
    /// <summary>Initializes one owned syntax entry.</summary>
    /// <param name="path">The RFC 6901 pointer.</param>
    /// <param name="label">The rendered property or array-index token, or null for the root.</param>
    /// <param name="kind">The JSON value kind.</param>
    /// <param name="rawValue">The owned scalar lexeme or container delimiter.</param>
    /// <param name="parent">The containing entry, or null for the root.</param>
    /// <param name="isArrayElement">Whether the label represents an array index.</param>
    internal JsonViewNode(
        string path,
        string? label,
        JsonValueKind kind,
        string rawValue,
        JsonViewNode? parent,
        bool isArrayElement)
    {
        Path = path;
        Label = label;
        Kind = kind;
        RawValue = rawValue;
        Parent = parent;
        IsArrayElement = isArrayElement;
    }

    /// <summary>Gets the stable RFC 6901 pointer for this entry.</summary>
    internal string Path { get; }

    /// <summary>Gets the rendered property or array-index token, or null for the root.</summary>
    internal string? Label { get; }

    /// <summary>Gets the parsed JSON value kind.</summary>
    internal JsonValueKind Kind { get; }

    /// <summary>Gets the owned scalar lexeme or opening container delimiter.</summary>
    internal string RawValue { get; }

    /// <summary>Gets the containing entry, or null for the root.</summary>
    internal JsonViewNode? Parent { get; }

    /// <summary>Gets whether the label is an array index rather than an object key.</summary>
    internal bool IsArrayElement { get; }

    /// <summary>Gets child entries in source order.</summary>
    internal List<JsonViewNode> Children { get; } = [];

    /// <summary>Gets or sets whether child entries participate in the visible projection.</summary>
    internal bool Expanded { get; set; } = true;

    /// <summary>Gets whether this value is an object or array.</summary>
    internal bool IsContainer => Kind is JsonValueKind.Object or JsonValueKind.Array;
}
