namespace SharpVision.Terminal.Capabilities;

/// <summary>Represents one optional feature and the origin of its evidence.</summary>
/// <param name="State">The support confidence.</param>
/// <param name="Origin">The evidence origin.</param>
public readonly record struct Feature(Support State, Origin Origin)
{
    /// <summary>Gets a conservative unknown feature.</summary>
    public static Feature Unknown { get; } = new(Support.Unknown, Origin.Default);

    /// <summary>Gets whether safe behavior may actively use the feature.</summary>
    public bool IsSupported => State == Support.Supported;
}
