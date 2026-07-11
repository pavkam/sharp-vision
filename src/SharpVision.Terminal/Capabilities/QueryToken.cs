namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies one active query without exposing internal correlation state.</summary>
/// <param name="Value">The positive tracker-local token.</param>
public readonly record struct QueryToken(long Value);
