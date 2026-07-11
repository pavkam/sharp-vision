using System.Buffers.Text;
using System.Diagnostics;

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Identifies a Select Graphic Rendition attribute.
/// </summary>
public enum Rendition
{
    /// <summary>Reset every rendition attribute and color.</summary>
    Reset = 0,

    /// <summary>Request bold or increased intensity.</summary>
    Bold = 1,

    /// <summary>Request faint or decreased intensity.</summary>
    Dim = 2,

    /// <summary>Request italic text.</summary>
    Italic = 3,

    /// <summary>Request underlined text.</summary>
    Underline = 4,

    /// <summary>Request slow blink.</summary>
    SlowBlink = 5,

    /// <summary>Request rapid blink.</summary>
    RapidBlink = 6,

    /// <summary>Exchange foreground and background roles.</summary>
    Reverse = 7,

    /// <summary>Request concealed text.</summary>
    Hidden = 8,

    /// <summary>Request struck-through text.</summary>
    Strike = 9,

    /// <summary>Disable bold and dim intensity.</summary>
    NormalIntensity = 22,

    /// <summary>Disable italic text.</summary>
    NotItalic = 23,

    /// <summary>Disable underline.</summary>
    NotUnderline = 24,

    /// <summary>Disable blink.</summary>
    NotBlink = 25,

    /// <summary>Disable reverse video.</summary>
    NotReverse = 27,

    /// <summary>Disable concealment.</summary>
    NotHidden = 28,

    /// <summary>Disable strike-through.</summary>
    NotStrike = 29,
}

/// <summary>
/// Identifies the encoded color representation.
/// </summary>
public enum ColorKind
{
    /// <summary>Use the terminal default color.</summary>
    Default,

    /// <summary>Use one of the terminal's 256 indexed colors.</summary>
    Indexed,

    /// <summary>Use an explicit 24-bit RGB color.</summary>
    Rgb,
}

/// <summary>
/// Represents a validated default, indexed, or RGB terminal color.
/// </summary>
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

/// <summary>
/// Encodes typed Select Graphic Rendition attributes and colors.
/// </summary>
/// <example>
/// <code>
/// Sgr.Apply(writer, Rendition.Bold);
/// Sgr.Foreground(writer, Color.Rgb(80, 160, 240));
/// </code>
/// </example>
public static class Sgr
{
    /// <summary>Applies one rendition attribute.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="rendition">The rendition attribute.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rendition"/> is unknown.
    /// </exception>
    public static void Apply(Writer writer, Rendition rendition)
    {
        ValidateRendition(rendition);
        WriteNumber(writer, (int) rendition);
    }

    /// <summary>Resets all rendition attributes and colors.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void Reset(Writer writer) => Apply(writer, Rendition.Reset);

    /// <summary>Applies a foreground color.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="color">The validated color.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="color"/> has an unknown representation.
    /// </exception>
    public static void Foreground(Writer writer, Color color) =>
        WriteColor(writer, color, foreground: true);

    /// <summary>Applies a background color.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="color">The validated color.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="color"/> has an unknown representation.
    /// </exception>
    public static void Background(Writer writer, Color color) =>
        WriteColor(writer, color, foreground: false);

    private static int Append(int value, Span<byte> destination)
    {
        var formatted = Utf8Formatter.TryFormat(value, destination, out var written);
        Debug.Assert(formatted, "The SGR scratch span is sized for an Int32.");

        return written;
    }

    private static void ValidateRendition(Rendition rendition)
    {
        if (rendition is not (
            Rendition.Reset or
            Rendition.Bold or
            Rendition.Dim or
            Rendition.Italic or
            Rendition.Underline or
            Rendition.SlowBlink or
            Rendition.RapidBlink or
            Rendition.Reverse or
            Rendition.Hidden or
            Rendition.Strike or
            Rendition.NormalIntensity or
            Rendition.NotItalic or
            Rendition.NotUnderline or
            Rendition.NotBlink or
            Rendition.NotReverse or
            Rendition.NotHidden or
            Rendition.NotStrike))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rendition), rendition, "The rendition value is unknown.");
        }
    }

    private static void WriteColor(Writer writer, Color color, bool foreground)
    {
        Span<byte> parameters = stackalloc byte[32];
        var length = 0;

        switch (color.Kind)
        {
            case ColorKind.Default:
                length += Append(foreground ? 39 : 49, parameters);
                break;

            case ColorKind.Indexed:
                length += Append(foreground ? 38 : 48, parameters);
                parameters[length++] = (byte) ';';
                parameters[length++] = (byte) '5';
                parameters[length++] = (byte) ';';
                length += Append(color.Red, parameters[length..]);
                break;

            case ColorKind.Rgb:
                length += Append(foreground ? 38 : 48, parameters);
                parameters[length++] = (byte) ';';
                parameters[length++] = (byte) '2';
                parameters[length++] = (byte) ';';
                length += Append(color.Red, parameters[length..]);
                parameters[length++] = (byte) ';';
                length += Append(color.Green, parameters[length..]);
                parameters[length++] = (byte) ';';
                length += Append(color.Blue, parameters[length..]);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(color), color.Kind, "The color representation is unknown.");
        }

        writer.Csi(parameters[..length], [], (byte) 'm');
    }

    private static void WriteNumber(Writer writer, int value)
    {
        Span<byte> parameters = stackalloc byte[10];
        var length = Append(value, parameters);
        writer.Csi(parameters[..length], [], (byte) 'm');
    }
}
