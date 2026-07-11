namespace SharpVision.Text;

/// <summary>Selects horizontal placement of one formatted line.</summary>
public enum Alignment
{
    /// <summary>Place content at the leading cell.</summary>
    Start,

    /// <summary>Center content using deterministic integer division.</summary>
    Center,

    /// <summary>Place content against the trailing cell.</summary>
    End,
}
