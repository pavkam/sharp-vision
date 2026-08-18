// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

using Capabilities;

/// <summary>Identifies one query family and optional correlation or typed-identity key.</summary>
internal readonly record struct Key
{
    /// <summary>Initializes one validated query key.</summary>
    /// <param name="kind">The response family.</param>
    /// <param name="id">The optional non-empty correlation or canonical typed identity.</param>
    public Key(QueryKind kind, string? id)
    {
        Debug.Assert(Enum.IsDefined(kind), "A query key kind must be defined.");
        Debug.Assert(id is null || id.Length > 0, "A present correlation ID cannot be empty.");

        Kind = kind;
        Id = id;
    }

    /// <summary>Gets the response family.</summary>
    public QueryKind Kind { get; }

    /// <summary>Gets the optional correlation or canonical typed identity.</summary>
    public string? Id { get; }
}
