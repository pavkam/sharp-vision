namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies the outcome of matching an incoming response.</summary>
public enum QueryMatch
{
    /// <summary>The response completed an active query.</summary>
    Matched,

    /// <summary>The response duplicated a recently completed query.</summary>
    Duplicate,

    /// <summary>The response followed timeout or cancellation.</summary>
    Late,

    /// <summary>No active or recent query matches the response.</summary>
    Unknown,
}
