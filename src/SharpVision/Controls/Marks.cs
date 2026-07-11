using System.Text;

using SharpVision.Terminal.Unicode;

namespace SharpVision.Controls;

/// <summary>Defines immutable printable narrow glyphs for CheckBox states.</summary>
public readonly record struct Marks
{
    /// <summary>Initializes and validates unchecked, checked, and indeterminate marks.</summary>
    /// <exception cref="ArgumentException">A mark is a control or is not one cell wide.</exception>
    public Marks(Rune uncheckedMark, Rune checkedMark, Rune indeterminateMark)
    {
        Unchecked = Validate(uncheckedMark, nameof(uncheckedMark));
        Checked = Validate(checkedMark, nameof(checkedMark));
        Indeterminate = Validate(indeterminateMark, nameof(indeterminateMark));
    }

    /// <summary>Gets the default Unicode checkbox marks.</summary>
    public static Marks Default { get; } = new(
        new Rune('☐'),
        new Rune('☑'),
        new Rune('◩'));

    /// <summary>Gets the unchecked mark.</summary>
    public Rune Unchecked { get; }

    /// <summary>Gets the checked mark.</summary>
    public Rune Checked { get; }

    /// <summary>Gets the indeterminate mark.</summary>
    public Rune Indeterminate { get; }

    private static Rune Validate(Rune value, string name)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = Width.Measure(buffer[..length]);
        return measurement.Cells == 1 && measurement.Controls == 0
            ? value
            : throw new ArgumentException(
                "A checkbox mark must be printable and one cell wide.",
                name);
    }
}
