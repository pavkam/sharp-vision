namespace SharpVision.Terminal.Protocols;

/// <summary>Describes the result of reading one CSI parameter field.</summary>
public enum ParameterStatus
{
    /// <summary>No field remains.</summary>
    End,

    /// <summary>An empty field requests the command-defined default.</summary>
    Default,

    /// <summary>A numeric field was read successfully.</summary>
    Value,

    /// <summary>A field contains a byte outside the decimal grammar.</summary>
    Invalid,

    /// <summary>A numeric field exceeds the configured magnitude.</summary>
    Overflow,

    /// <summary>Reading another field would exceed the configured count.</summary>
    Limit,
}
