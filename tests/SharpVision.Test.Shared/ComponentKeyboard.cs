// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Test.Shared;

/// <summary>Drives a mounted component with real terminal keyboard bytes.</summary>
public sealed class ComponentKeyboard
{
    private readonly ComponentSurface _surface;

    /// <summary>Initializes a keyboard driver for one non-null mounted surface.</summary>
    /// <param name="surface">The owning component surface.</param>
    /// <exception cref="ArgumentNullException"><paramref name="surface"/> is null.</exception>
    public ComponentKeyboard(ComponentSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
    }

    /// <summary>Presses one supported key through its real terminal encoding.</summary>
    /// <param name="code">The defined key code to press.</param>
    /// <returns>A task completed after routed key behavior and rendering settle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="code"/> is undefined.</exception>
    /// <exception cref="NotSupportedException"><paramref name="code"/> has no component-driver encoding yet.</exception>
    public Task PressAsync(Code code)
        => PressAsync(code, Modifiers.None);

    /// <summary>Presses one supported key and modifier combination through real terminal encoding.</summary>
    /// <param name="code">The defined key code to press.</param>
    /// <param name="modifiers">The exact supported modifier flags.</param>
    /// <returns>A task completed after routed key behavior and rendering settle.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A code or modifier flag is undefined.</exception>
    /// <exception cref="NotSupportedException">The combination has no component-driver encoding yet.</exception>
    public Task PressAsync(Code code, Modifiers modifiers)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "The keyboard code is undefined.");
        }

        const Modifiers known = Modifiers.Shift | Modifiers.Alt | Modifiers.Control |
                                Modifiers.Super | Modifiers.Hyper | Modifiers.Meta | Modifiers.CapsLock |
                                Modifiers.NumLock;

        return (modifiers & ~known) != 0
            ? throw new ArgumentOutOfRangeException(
                nameof(modifiers),
                modifiers,
                "The keyboard modifiers are undefined.")
            : (code, modifiers) switch
            {
                (Code.Tab, Modifiers.None) => _surface.SendAsync("\t"u8.ToArray(), "press Tab"),
                (Code.Tab, Modifiers.Shift) => _surface.SendAsync("\u001b[Z"u8.ToArray(), "press Shift+Tab"),
                (Code.Tab, _) => _surface.SendAsync(
                    Encoding.ASCII.GetBytes($"\u001b[9;{KittyModifiers(modifiers)}u"),
                    $"press {modifiers}+Tab"),
                (Code.Escape, Modifiers.None) => _surface.SendAsync("\u001b[27u"u8.ToArray(), "press Escape"),
                (Code.Escape, _) => _surface.SendAsync(
                    Encoding.ASCII.GetBytes($"\u001b[27;{KittyModifiers(modifiers)}u"),
                    "press modified Escape"),
                (Code.Up, Modifiers.None) => _surface.SendAsync("\u001b[A"u8.ToArray(), "press Up"),
                (Code.Down, Modifiers.None) => _surface.SendAsync("\u001b[B"u8.ToArray(), "press Down"),
                (Code.Left, Modifiers.None) => _surface.SendAsync("\u001b[D"u8.ToArray(), "press Left"),
                (Code.Right, Modifiers.None) => _surface.SendAsync("\u001b[C"u8.ToArray(), "press Right"),
                (Code.Left, Modifiers.Shift) => _surface.SendAsync("\u001b[1;2D"u8.ToArray(), "press Shift+Left"),
                (Code.Home, Modifiers.None) => _surface.SendAsync("\u001b[H"u8.ToArray(), "press Home"),
                (Code.Home, _) => _surface.SendAsync(
                    Encoding.ASCII.GetBytes($"\u001b[1;{KittyModifiers(modifiers)}H"),
                    $"press {modifiers}+Home"),
                (Code.End, Modifiers.None) => _surface.SendAsync("\u001b[F"u8.ToArray(), "press End"),
                (Code.End, _) => _surface.SendAsync(
                    Encoding.ASCII.GetBytes($"\u001b[1;{KittyModifiers(modifiers)}F"),
                    $"press {modifiers}+End"),
                (Code.PageUp, Modifiers.None) => _surface.SendAsync("\u001b[5~"u8.ToArray(), "press PageUp"),
                (Code.PageDown, Modifiers.None) => _surface.SendAsync("\u001b[6~"u8.ToArray(), "press PageDown"),
                (Code.Backspace, Modifiers.None) => _surface.SendAsync(new byte[] { 0x7f }, "press Backspace"),
                (Code.Backspace, _) => _surface.SendAsync(
                    Encoding.ASCII.GetBytes($"\u001b[127;{KittyModifiers(modifiers)}u"),
                    $"press {modifiers}+Backspace"),
                (Code.Delete, Modifiers.None) => _surface.SendAsync("\u001b[3~"u8.ToArray(), "press Delete"),
                (Code.Enter, Modifiers.None) => _surface.SendAsync("\u001b[13u"u8.ToArray(), "press Enter"),
                _ => throw new NotSupportedException(
                    $"Component keyboard encoding for {modifiers}+{code} is not supported.")
            };
    }

    private static int KittyModifiers(Modifiers modifiers) => 1 + (int) modifiers;

    /// <summary>Types non-empty printable text through its owned UTF-8 terminal bytes.</summary>
    /// <param name="value">The non-null text containing no terminal controls.</param>
    /// <returns>A task completed after all decoded text and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or contains a terminal control.</exception>
    public Task TypeAsync(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Length == 0 || Width.Measure(value).Controls > 0
            ? throw new ArgumentException(
                "Typed text must be non-empty and contain no terminal controls.",
                nameof(value))
            : _surface.SendAsync(Encoding.UTF8.GetBytes(value), "type text");
    }

    /// <summary>Pastes one owned string through a complete bracketed-paste transaction.</summary>
    /// <param name="value">The non-null paste payload.</param>
    /// <returns>A task completed after the atomic paste and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public Task PasteAsync(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var payload = Encoding.UTF8.GetBytes(value);
        var bytes = new byte[checked(12 + payload.Length)];
        "\u001b[200~"u8.CopyTo(bytes);
        payload.CopyTo(bytes, 6);
        "\u001b[201~"u8.CopyTo(bytes.AsSpan(6 + payload.Length));
        return _surface.SendAsync(bytes, "paste text");
    }

    /// <summary>Completes one character through distinct Kitty press and release input actions.</summary>
    /// <param name="value">The Unicode scalar character to complete.</param>
    /// <returns>A task completed after both routed actions and rendering settle.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is a control scalar.</exception>
    public async Task CompleteCharacterAsync(Rune value)
    {
        await PressCharacterAsync(value);
        await ReleaseCharacterAsync(value);
    }

    /// <summary>Presses one printable scalar through a Kitty keyboard press action.</summary>
    /// <param name="value">The Unicode scalar character to press.</param>
    /// <returns>A task completed after the routed press and rendering settle.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is a control scalar.</exception>
    public Task PressCharacterAsync(Rune value) => SendCharacterAsync(value, 1, "press");

    /// <summary>Releases one printable scalar through a Kitty keyboard release action.</summary>
    /// <param name="value">The Unicode scalar character to release.</param>
    /// <returns>A task completed after the routed release and rendering settle.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is a control scalar.</exception>
    public Task ReleaseCharacterAsync(Rune value) => SendCharacterAsync(value, 3, "release");

    private Task SendCharacterAsync(Rune value, int eventType, string action)
        => Rune.GetUnicodeCategory(value) == UnicodeCategory.Control
            ? throw new ArgumentException("A keyboard character cannot be a control scalar.", nameof(value))
            : _surface.SendAsync(Encode(value, eventType), $"{action} {value}");

    private static byte[] Encode(Rune value, int eventType) => Encoding.ASCII.GetBytes(
        FormattableString.Invariant($"\u001b[{value.Value};1:{eventType}u"));
}
