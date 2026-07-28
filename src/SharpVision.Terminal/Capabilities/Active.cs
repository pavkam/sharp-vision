// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Stores one active query token and deadline.</summary>
internal readonly record struct Active
{
    /// <summary>Initializes one validated active query.</summary>
    /// <param name="token">The positive tracker-local token.</param>
    /// <param name="deadline">The exclusive response deadline.</param>
    public Active(QueryToken token, DateTimeOffset deadline)
    {
        Debug.Assert(token.Value > 0, "An active query token must be positive.");

        Token = token;
        Deadline = deadline;
    }

    /// <summary>Gets the tracker-local token.</summary>
    public QueryToken Token { get; }

    /// <summary>Gets the exclusive response deadline.</summary>
    public DateTimeOffset Deadline { get; }
}
