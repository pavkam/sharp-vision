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

    /// <summary>Gets the light Unicode box-drawing set.</summary>
    public static Glyphs Light { get; } = new(
        new Rune('┌'),
        new Rune('─'),
        new Rune('┐'),
        new Rune('│'),
        new Rune('┘'),
        new Rune('─'),
        new Rune('└'),
        new Rune('│'));

    /// <summary>Gets the heavy Unicode box-drawing set.</summary>
    public static Glyphs Heavy { get; } = new(
        new Rune('┏'),
        new Rune('━'),
        new Rune('┓'),
        new Rune('┃'),
        new Rune('┛'),
        new Rune('━'),
        new Rune('┗'),
        new Rune('┃'));

    /// <summary>Gets the paired-line Unicode box-drawing set.</summary>
    public static Glyphs Paired { get; } = new(
        new Rune('╔'),
        new Rune('═'),
        new Rune('╗'),
        new Rune('║'),
        new Rune('╝'),
        new Rune('═'),
        new Rune('╚'),
        new Rune('║'));

    /// <summary>Gets the rounded light Unicode box-drawing set.</summary>
    public static Glyphs Rounded { get; } = new(
        new Rune('╭'),
        new Rune('─'),
        new Rune('╮'),
        new Rune('│'),
        new Rune('╯'),
        new Rune('─'),
        new Rune('╰'),
        new Rune('│'));

    /// <summary>Gets the portable ASCII border set.</summary>
    public static Glyphs Ascii { get; } = new(
        new Rune('+'),
        new Rune('-'),
        new Rune('+'),
        new Rune('|'),
        new Rune('+'),
        new Rune('-'),
        new Rune('+'),
        new Rune('|'));

    /// <summary>Gets a full-block border set.</summary>
    public static Glyphs Solid { get; } = Uniform(new Rune('█'));

    /// <summary>Gets a light-shade border set.</summary>
    public static Glyphs LightShade { get; } = Uniform(new Rune('░'));

    /// <summary>Gets a medium-shade border set.</summary>
    public static Glyphs MediumShade { get; } = Uniform(new Rune('▒'));

    /// <summary>Gets a dark-shade border set.</summary>
    public static Glyphs DarkShade { get; } = Uniform(new Rune('▓'));

    /// <summary>Gets the default light Unicode box-drawing set.</summary>
    public static Glyphs Default => Light;

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

    private static Glyphs Uniform(Rune value) =>
        new(value, value, value, value, value, value, value, value);
}
