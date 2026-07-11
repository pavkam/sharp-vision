using System.Buffers.Text;
using System.Diagnostics;

namespace SharpVision.Terminal.Protocols;

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
