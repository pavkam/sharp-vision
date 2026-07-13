namespace SharpVision.Terminal.Capabilities;

using System.Diagnostics;

/// <summary>Stores one active query token and deadline.</summary>
internal readonly record struct Active
{
    /// <summary>Initializes one validated active query.</summary>
    /// <param name="token">The positive tracker-local token.</param>
    /// <param name="deadline">The inclusive response deadline.</param>
    internal Active(QueryToken token, DateTimeOffset deadline)
    {
        Debug.Assert(token.Value > 0, "An active query token must be positive.");

        Token = token;
        Deadline = deadline;
    }

    /// <summary>Gets the tracker-local token.</summary>
    internal QueryToken Token { get; }

    /// <summary>Gets the inclusive response deadline.</summary>
    internal DateTimeOffset Deadline { get; }
}
