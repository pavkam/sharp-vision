using System.Text;

using SharpVision.Terminal.Unicode;

namespace SharpVision.Controls;

/// <summary>Defines immutable printable narrow Runes for every Border segment.</summary>
public readonly record struct Glyphs
{
    /// <summary>Initializes and validates all physical border glyphs.</summary>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public Glyphs(
        Rune topLeft,
        Rune top,
        Rune topRight,
        Rune right,
        Rune bottomRight,
        Rune bottom,
        Rune bottomLeft,
        Rune left)
    {
        TopLeft = Validate(topLeft, nameof(topLeft));
        Top = Validate(top, nameof(top));
        TopRight = Validate(topRight, nameof(topRight));
        Right = Validate(right, nameof(right));
        BottomRight = Validate(bottomRight, nameof(bottomRight));
        Bottom = Validate(bottom, nameof(bottom));
        BottomLeft = Validate(bottomLeft, nameof(bottomLeft));
        Left = Validate(left, nameof(left));
    }

    /// <summary>Gets the default Unicode box-drawing set.</summary>
    public static Glyphs Default { get; } = new(
        new Rune('┌'),
        new Rune('─'),
        new Rune('┐'),
        new Rune('│'),
        new Rune('┘'),
        new Rune('─'),
        new Rune('└'),
        new Rune('│'));

    /// <summary>Gets the top-left glyph.</summary>
    public Rune TopLeft { get; }

    /// <summary>Gets the top edge glyph.</summary>
    public Rune Top { get; }

    /// <summary>Gets the top-right glyph.</summary>
    public Rune TopRight { get; }

    /// <summary>Gets the right edge glyph.</summary>
    public Rune Right { get; }

    /// <summary>Gets the bottom-right glyph.</summary>
    public Rune BottomRight { get; }

    /// <summary>Gets the bottom edge glyph.</summary>
    public Rune Bottom { get; }

    /// <summary>Gets the bottom-left glyph.</summary>
    public Rune BottomLeft { get; }

    /// <summary>Gets the left edge glyph.</summary>
    public Rune Left { get; }

    private static Rune Validate(Rune value, string name)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = Width.Measure(buffer[..length]);

        return measurement.Cells == 1 && measurement.Controls == 0
            ? value
            : throw new ArgumentException(
                "A border glyph must be printable and one cell wide.",
                name);
    }
}
