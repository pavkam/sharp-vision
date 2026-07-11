namespace SharpVision.Terminal.Protocols;

/// <summary>Represents a validated default, indexed, or RGB terminal color.</summary>
public readonly record struct Color
{
    /// <summary>Gets the terminal default color.</summary>
    public static Color Default { get; } = new(ColorKind.Default, 0, 0, 0);

    private Color(ColorKind kind, byte red, byte green, byte blue)
    {
        Kind = kind;
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>Gets the representation used by this color.</summary>
    public ColorKind Kind { get; }

    /// <summary>Gets the palette index or RGB red component.</summary>
    public byte Red { get; }

    /// <summary>Gets the RGB green component.</summary>
    public byte Green { get; }

    /// <summary>Gets the RGB blue component.</summary>
    public byte Blue { get; }

    /// <summary>Creates a 256-color palette reference.</summary>
    /// <param name="index">The palette index from 0 through 255.</param>
    /// <returns>The validated indexed color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside 0 through 255.
    /// </exception>
    public static Color Indexed(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, byte.MaxValue);
        return new Color(ColorKind.Indexed, (byte) index, 0, 0);
    }

    /// <summary>Creates a 24-bit RGB color.</summary>
    /// <param name="red">The red component from 0 through 255.</param>
    /// <param name="green">The green component from 0 through 255.</param>
    /// <param name="blue">The blue component from 0 through 255.</param>
    /// <returns>The validated RGB color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A component is outside 0 through 255.
    /// </exception>
    public static Color Rgb(int red, int green, int blue)
    {
        ValidateComponent(red, nameof(red));
        ValidateComponent(green, nameof(green));
        ValidateComponent(blue, nameof(blue));
        return new Color(ColorKind.Rgb, (byte) red, (byte) green, (byte) blue);
    }

    private static void ValidateComponent(int value, string parameterName)
    {
        if (value is < byte.MinValue or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, value, "A color component must be from 0 through 255.");
        }
    }
}
