namespace SharpVision.Styling;

/// <summary>Selects whether a control body fill preserves or replaces existing cells.</summary>
public enum FillMode
{
    /// <summary>Preserves existing canvas cells under the control body.</summary>
    Transparent,

    /// <summary>Fills every arranged body cell with the active background.</summary>
    Opaque,
}
