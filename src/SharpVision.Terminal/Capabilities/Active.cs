namespace SharpVision.Terminal.Capabilities;

/// <summary>Stores one active query token and deadline.</summary>
/// <param name="Token">The tracker-local token.</param>
/// <param name="Deadline">The inclusive response deadline.</param>
internal readonly record struct Active(QueryToken Token, DateTimeOffset Deadline);
