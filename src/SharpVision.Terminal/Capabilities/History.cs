// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using System.Diagnostics;

/// <summary>Stores one bounded recent query outcome.</summary>
internal readonly record struct History
{
    /// <summary>Initializes one validated retained outcome.</summary>
    /// <param name="outcome">The terminal query outcome.</param>
    /// <param name="until">The exclusive retention deadline.</param>
    internal History(Outcome outcome, DateTimeOffset until)
    {
        Debug.Assert(Enum.IsDefined(outcome), "A retained query outcome must be defined.");

        Outcome = outcome;
        Until = until;
    }

    /// <summary>Gets the terminal query outcome.</summary>
    internal Outcome Outcome { get; }

    /// <summary>Gets the exclusive retention deadline.</summary>
    internal DateTimeOffset Until { get; }
}
