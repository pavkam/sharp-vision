namespace SharpVision.Terminal.Unicode;

/// <summary>Creates allocation-free Unicode 17 extended-grapheme enumerators.</summary>
public static class Graphemes
{
    /// <summary>Enumerates extended grapheme clusters in borrowed UTF-16 text.</summary>
    /// <param name="value">The span borrowed until enumeration ends.</param>
    /// <returns>An allocation-free enumerable.</returns>
    public static GraphemeEnumerable Enumerate(ReadOnlySpan<char> value) => new(value);
}
