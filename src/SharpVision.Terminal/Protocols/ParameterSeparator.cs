namespace SharpVision.Terminal.Protocols;

/// <summary>Describes the delimiter following one CSI parameter field.</summary>
public enum ParameterSeparator
{
    /// <summary>The field is the final field.</summary>
    None,

    /// <summary>A semicolon begins the next independent parameter.</summary>
    Semicolon,

    /// <summary>A colon begins the next subparameter.</summary>
    Colon,
}
