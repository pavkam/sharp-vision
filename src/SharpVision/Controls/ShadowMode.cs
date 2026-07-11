namespace SharpVision.Controls;

/// <summary>Defines how a Shadow changes cells in its visual overflow footprint.</summary>
public enum ShadowMode
{
    /// <summary>Preserves underlying graphemes and replaces their semantic style.</summary>
    Composite,

    /// <summary>Replaces footprint cells with the configured block or shade glyph.</summary>
    BlockGlyph,
}
