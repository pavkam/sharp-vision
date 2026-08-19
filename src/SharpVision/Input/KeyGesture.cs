// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Identifies one immutable keyboard chord that can be compared against a keyboard transition.</summary>
/// <remarks>
/// A gesture is a value type independent of any single binding. <see cref="Matches"/> lets a
/// consumer test a decoded <see cref="Stroke"/> against it directly; <c>MenuItem.Shortcut</c> uses
/// this to route matching strokes to the item regardless of focus.
/// </remarks>
[PublicAPI]
public readonly record struct KeyGesture
{
    private const Modifiers _allModifiers =
        Modifiers.Shift |
        Modifiers.Alt |
        Modifiers.Control |
        Modifiers.Super |
        Modifiers.Hyper |
        Modifiers.Meta |
        Modifiers.CapsLock |
        Modifiers.NumLock;

    /// <summary>Initializes a validated key gesture.</summary>
    /// <param name="code">The logical key.</param>
    /// <param name="modifiers">The active modifiers.</param>
    /// <param name="character">The character for <see cref="Code.Character"/>.</param>
    /// <remarks>
    /// <see cref="Modifiers.CapsLock"/> and <see cref="Modifiers.NumLock"/> are accepted but stripped
    /// from the stored, compared, and displayed gesture, since a lock key is state rather than part of
    /// a chord. The character for <see cref="Code.Character"/> is stored case-folded to uppercase, so
    /// two gestures that differ only in the case of the same letter compare and hash equal.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> is unknown or <see cref="Code.Unknown"/>, or <paramref name="modifiers"/>
    /// contains unknown flags.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Character presence does not agree with <paramref name="code"/>.
    /// </exception>
    public KeyGesture(Code code, Modifiers modifiers = Modifiers.None, Rune? character = null)
    {
        if (!Enum.IsDefined(code) || code == Code.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "The logical code is unknown.");
        }

        ArgumentOutOfRangeException.ThrowIfUndefinedFlags(modifiers, _allModifiers, nameof(modifiers), "The modifier set contains unknown flags.");

        if (code == Code.Character != character.HasValue)
        {
            throw new ArgumentException(
                "Only a character code must carry a Unicode scalar.",
                nameof(character));
        }

        Code = code;
        Modifiers = modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock);
        Character = character is { } value ? Rune.ToUpperInvariant(value) : null;
    }

    /// <summary>Gets the logical key.</summary>
    public Code Code { get; }

    /// <summary>Gets the active modifiers.</summary>
    public Modifiers Modifiers { get; }

    /// <summary>Gets the character for <see cref="Code.Character"/>.</summary>
    public Rune? Character { get; }

    /// <summary>Reports whether a completed keyboard transition activates this gesture.</summary>
    /// <param name="stroke">The decoded keyboard transition.</param>
    /// <returns>
    /// True when <paramref name="stroke"/> is a <see cref="KeyAction.Press"/> whose code,
    /// character (for <see cref="Code.Character"/>), and modifiers - lock keys excluded - match
    /// this gesture exactly.
    /// </returns>
    [Pure]
    public bool Matches(in Stroke stroke) =>
        stroke.Action == KeyAction.Press &&
        stroke.Code == Code &&
        (stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) == Modifiers &&
        (Code != Code.Character ||
         (stroke.Character is { } character && Rune.ToUpperInvariant(character) == Character));

    /// <summary>Formats this gesture as conventional display text, for example "Ctrl+Shift+S".</summary>
    /// <returns>The non-null, non-empty display text.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();

        if ((Modifiers & Modifiers.Control) != 0)
        {
            _ = builder.Append("Ctrl+");
        }

        if ((Modifiers & Modifiers.Alt) != 0)
        {
            _ = builder.Append("Alt+");
        }

        if ((Modifiers & Modifiers.Shift) != 0)
        {
            _ = builder.Append("Shift+");
        }

        if ((Modifiers & Modifiers.Super) != 0)
        {
            _ = builder.Append("Super+");
        }

        if ((Modifiers & Modifiers.Hyper) != 0)
        {
            _ = builder.Append("Hyper+");
        }

        if ((Modifiers & Modifiers.Meta) != 0)
        {
            _ = builder.Append("Meta+");
        }

        _ = Code == Code.Character
            ? builder.Append(Character!.Value.ToString())
            : builder.Append(Code);

        return builder.ToString();
    }
}
