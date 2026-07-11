namespace SharpVision.Terminal.Rendering;

/// <summary>Defines the supported straight-segment dash patterns.</summary>
public enum LinePattern
{
    /// <summary>Uses an unbroken stroke.</summary>
    Solid,

    /// <summary>Uses two dashes per character cell.</summary>
    DoubleDash,

    /// <summary>Uses three dashes per character cell.</summary>
    TripleDash,

    /// <summary>Uses four dashes per character cell.</summary>
    QuadrupleDash,
}
