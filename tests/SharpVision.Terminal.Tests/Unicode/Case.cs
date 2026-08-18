// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>Stores one parsed Unicode grapheme conformance case.</summary>
internal sealed record Case
{
    /// <summary>Initializes one validated conformance case.</summary>
    /// <param name="value">The non-null UTF-16 source text.</param>
    /// <param name="boundaries">The non-null expected UTF-16 boundary offsets.</param>
    internal Case(string value, int[] boundaries)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(boundaries);

        Value = value;
        Boundaries = boundaries;
    }

    /// <summary>Gets the UTF-16 source text.</summary>
    internal string Value { get; }

    /// <summary>Gets the expected UTF-16 boundary offsets.</summary>
    internal int[] Boundaries { get; }
}
