// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Keyboard;

/// <summary>Encodes Kitty keyboard query and mode-stack commands.</summary>
[PublicAPI]
public static class KittyKeyboard
{
    private const Enhancement _all =
        Enhancement.Disambiguate |
        Enhancement.EventTypes |
        Enhancement.AlternateKeys |
        Enhancement.AllKeys |
        Enhancement.AssociatedText;

    /// <summary>Queries the current progressive enhancement flags.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void Query(Writer writer) => writer.Csi("?"u8, [], (byte) 'u');

    /// <summary>Pushes current flags and replaces them with a validated set.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="flags">The flags to push.</param>
    /// <exception cref="ArgumentOutOfRangeException">Unknown flags are present.</exception>
    /// <exception cref="ArgumentException">Associated text lacks all-key reporting.</exception>
    public static void Push(Writer writer, Enhancement flags)
    {
        Validate(flags);
        Span<byte> parameters = stackalloc byte[12];
        parameters[0] = (byte) '>';
        var formatted = Utf8Formatter.TryFormat((int) flags, parameters[1..], out var written);
        Debug.Assert(formatted, "A 32-bit enhancement value must fit its stack buffer.");
        writer.Csi(parameters[..(written + 1)], [], (byte) 'u');
    }

    /// <summary>Pops one or more entries from the terminal's keyboard flag stack.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="count">The positive pop count; one uses the canonical omitted form.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public static void Pop(Writer writer, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        Span<byte> parameters = stackalloc byte[12];
        parameters[0] = (byte) '<';
        var length = 1;

        if (count != 1)
        {
            var formatted = Utf8Formatter.TryFormat(count, parameters[1..], out var written);
            Debug.Assert(formatted, "A positive 32-bit pop count must fit its stack buffer.");
            length += written;
        }

        writer.Csi(parameters[..length], [], (byte) 'u');
    }

    /// <summary>Applies enhancement flags without changing the terminal stack.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="flags">The flags to apply.</param>
    /// <param name="mode">The replace, set, or clear operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Flags or mode are unknown.</exception>
    /// <exception cref="ArgumentException">Associated text lacks all-key reporting.</exception>
    public static void Set(Writer writer, Enhancement flags, EnhancementMode mode)
    {
        Validate(flags);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The enhancement mode is unknown.");
        }

        Span<byte> parameters = stackalloc byte[16];
        parameters[0] = (byte) '=';
        var formatted = Utf8Formatter.TryFormat((int) flags, parameters[1..], out var written);
        Debug.Assert(formatted, "A 32-bit enhancement value must fit its stack buffer.");
        var position = written + 1;
        parameters[position++] = (byte) ';';
        formatted = Utf8Formatter.TryFormat((int) mode, parameters[position..], out written);
        Debug.Assert(formatted, "An enhancement mode must fit its stack buffer.");
        position += written;
        writer.Csi(parameters[..position], [], (byte) 'u');
    }

    private static void Validate(Enhancement flags)
    {
        if ((flags & ~_all) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flags), flags, "Unknown enhancement flags are set.");
        }

        if ((flags & Enhancement.AssociatedText) != 0 &&
            (flags & Enhancement.AllKeys) == 0)
        {
            throw new ArgumentException(
                "Associated text requires all-key reporting.",
                nameof(flags));
        }
    }
}
