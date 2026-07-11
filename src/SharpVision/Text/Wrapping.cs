namespace SharpVision.Text;

/// <summary>Selects how logical text lines break within a finite cell width.</summary>
public enum Wrapping
{
    /// <summary>Preserve each logical line without inserting breaks.</summary>
    None,

    /// <summary>Prefer whitespace boundaries, then fall back to grapheme boundaries.</summary>
    Word,

    /// <summary>Break only between extended grapheme clusters.</summary>
    Grapheme,
}
