namespace SharpVision.Terminal.Unicode;

/// <summary>Identifies the printable terminal width of one extended grapheme cluster.</summary>
public enum CellWidth
{
    /// <summary>The cluster is a contextual control and consumes no cell.</summary>
    Control,

    /// <summary>The cluster occupies one terminal cell.</summary>
    Narrow,

    /// <summary>The cluster occupies two terminal cells.</summary>
    Wide,
}
