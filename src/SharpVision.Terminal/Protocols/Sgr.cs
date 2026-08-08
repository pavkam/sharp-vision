// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
[PublicAPI]
public static class Sgr
{
    /// <summary>Applies one rendition attribute.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="rendition">The rendition attribute.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rendition"/> is unknown.
    /// </exception>
    public static void Apply(ProtocolWriter writer, Rendition rendition)
    {
        ValidateRendition(rendition);
        WriteNumber(writer, (int) rendition);
    }

    /// <summary>Resets all rendition attributes and colors.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void Reset(ProtocolWriter writer) => Apply(writer, Rendition.Reset);

    /// <summary>Applies a foreground color.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="color">The validated color.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="color"/> has an unknown representation.
    /// </exception>
    public static void Foreground(ProtocolWriter writer, Color color) =>
        WriteColor(writer, color, selector: 38, reset: 39);

    /// <summary>Applies a background color.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="color">The validated color.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="color"/> has an unknown representation.
    /// </exception>
    public static void Background(ProtocolWriter writer, Color color) =>
        WriteColor(writer, color, selector: 48, reset: 49);

    /// <summary>Applies or resets the underline color.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="color">The default or RGB underline color.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="color"/> has an unknown representation.
    /// </exception>
    public static void UnderlineColor(ProtocolWriter writer, Color color) =>
        WriteColor(writer, color, selector: 58, reset: 59);

    /// <summary>Applies one 256-color palette position to the foreground.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="position">The palette position from zero through 255.</param>
    internal static void ForegroundPalette(ProtocolWriter writer, int position) =>
        WritePaletteColor(writer, position, selector: 38);

    /// <summary>Applies one 256-color palette position to the background.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="position">The palette position from zero through 255.</param>
    internal static void BackgroundPalette(ProtocolWriter writer, int position) =>
        WritePaletteColor(writer, position, selector: 48);

    /// <summary>Applies one 256-color palette position to the underline.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="position">The palette position from zero through 255.</param>
    internal static void UnderlineColorPalette(ProtocolWriter writer, int position) =>
        WritePaletteColor(writer, position, selector: 58);

    /// <summary>Applies one xterm-compatible underline variant.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="underline">The typed underline presentation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="underline"/> is unknown.
    /// </exception>
    public static void Apply(ProtocolWriter writer, Underline underline)
    {
        underline.ValidateDefined(nameof(underline), "The underline style is unknown.");

        Span<byte> parameters = [(byte) '4', (byte) ':', (byte) ('0' + (int) underline)];
        writer.Csi(parameters, [], (byte) 'm');
    }

    /// <summary>Applies one classic ANSI or aixterm foreground color.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="color">The typed 16-color palette entry.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="color"/> is unknown.
    /// </exception>
    public static void Foreground(ProtocolWriter writer, BasicColor color) =>
        WriteBasicColor(writer, color, foreground: true);

    /// <summary>Applies one classic ANSI or aixterm background color.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="color">The typed 16-color palette entry.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="color"/> is unknown.
    /// </exception>
    public static void Background(ProtocolWriter writer, BasicColor color) =>
        WriteBasicColor(writer, color, foreground: false);

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
            Rendition.NotStrike or
            Rendition.Overline or
            Rendition.NotOverline))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rendition), rendition, "The rendition value is unknown.");
        }
    }

    private static void WriteBasicColor(ProtocolWriter writer, BasicColor color, bool foreground)
    {
        color.ValidateDefined(nameof(color), "The basic color is unknown.");

        var index = (int) color;
        var value = index < 8
            ? (foreground ? 30 : 40) + index
            : (foreground ? 90 : 100) + index - 8;
        WriteNumber(writer, value);
    }

    private static void WriteColor(ProtocolWriter writer, Color color, int selector, int reset)
    {
        Span<byte> parameters = stackalloc byte[32];
        var length = 0;

        if (color.IsDefault)
        {
            length += Append(reset, parameters);
        }
        else if (color.IsRgb)
        {
            length += Append(selector, parameters);
            parameters[length++] = (byte) ';';
            parameters[length++] = (byte) '2';
            parameters[length++] = (byte) ';';
            length += Append(color.Red, parameters[length..]);
            parameters[length++] = (byte) ';';
            length += Append(color.Green, parameters[length..]);
            parameters[length++] = (byte) ';';
            length += Append(color.Blue, parameters[length..]);
        }
        else
        {
            throw new ArgumentException(
                "SGR encoding requires a concrete terminal color.",
                nameof(color));
        }

        writer.Csi(parameters[..length], [], (byte) 'm');
    }

    private static void WritePaletteColor(ProtocolWriter writer, int position, int selector)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, byte.MaxValue);
        Span<byte> parameters = stackalloc byte[12];
        var length = Append(selector, parameters);
        parameters[length++] = (byte) ';';
        parameters[length++] = (byte) '5';
        parameters[length++] = (byte) ';';
        length += Append(position, parameters[length..]);
        writer.Csi(parameters[..length], [], (byte) 'm');
    }

    private static void WriteNumber(ProtocolWriter writer, int value)
    {
        Span<byte> parameters = stackalloc byte[10];
        var length = Append(value, parameters);
        writer.Csi(parameters[..length], [], (byte) 'm');
    }
}
