namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies one terminal query completion state.</summary>
internal enum Outcome
{
    /// <summary>A response completed the query.</summary>
    Completed,

    /// <summary>The caller cancelled the query.</summary>
    Cancelled,

    /// <summary>The response deadline elapsed.</summary>
    TimedOut,

    /// <summary>Capability evidence proved the query unsupported.</summary>
    Unsupported,
}
