namespace SharpVision.Fonts;

/// <summary>Owns one normalized fixed-height FIGfont glyph.</summary>
internal sealed class FigletGlyph
{
    /// <summary>Initializes one internally validated glyph.</summary>
    /// <param name="rows">The owned equally wide rows.</param>
    internal FigletGlyph(string[] rows)
    {
        Rows = rows;
        Width = rows.Length == 0 ? 0 : rows[0].Length;
    }

    /// <summary>Gets the owned equally wide rows.</summary>
    internal string[] Rows { get; }

    /// <summary>Gets the normalized UTF-16 row width.</summary>
    internal int Width { get; }
}
