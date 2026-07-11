namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies confidence in one optional terminal feature.</summary>
public enum Support
{
    /// <summary>No reliable evidence is available.</summary>
    Unknown,

    /// <summary>Evidence says the feature is unavailable.</summary>
    Unsupported,

    /// <summary>An environment hint suggests support but must not enable it.</summary>
    Tentative,

    /// <summary>A query or explicit override proves support.</summary>
    Supported,
}
