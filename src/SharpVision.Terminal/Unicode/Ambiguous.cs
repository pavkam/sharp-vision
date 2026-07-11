namespace SharpVision.Terminal.Unicode;

/// <summary>Selects the terminal width of Unicode East Asian Ambiguous scalars.</summary>
public enum Ambiguous
{
    /// <summary>Measure ambiguous scalars as one terminal cell.</summary>
    Narrow,

    /// <summary>Measure ambiguous scalars as two terminal cells.</summary>
    Wide,
}
