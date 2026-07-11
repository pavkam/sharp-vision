namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies how a direct Kitty enhancement command applies flags.</summary>
public enum EnhancementMode
{
    /// <summary>Replace all flags with the supplied set.</summary>
    Replace = 1,

    /// <summary>Set supplied flags and leave other flags unchanged.</summary>
    Set = 2,

    /// <summary>Clear supplied flags and leave other flags unchanged.</summary>
    Clear = 3,
}
