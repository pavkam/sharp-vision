// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Centralizes modifier classification for direct keyboard text and command handling.</summary>
/// <remarks>
/// Every classification here first strips or requires specific bits of a decoded
/// <see cref="Modifiers"/> chord and compares the remainder; none of them touch the key code
/// itself. A caller combines the result with its own code check (for example, <c>Code.Down</c>) to
/// decide whether one particular keystroke is eligible for one particular kind of handling.
/// </remarks>
[PublicAPI]
public static class KeyboardModifierPolicy
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

    private const Modifiers _textEntryModifiers =
        Modifiers.Shift |
        Modifiers.CapsLock |
        Modifiers.NumLock;

    private const Modifiers _collectionSelectionModifiers =
        Modifiers.Control |
        Modifiers.Shift |
        Modifiers.CapsLock |
        Modifiers.NumLock;

    private const Modifiers _lockModifiers = Modifiers.CapsLock | Modifiers.NumLock;

    /// <summary>Reports whether direct character input carries no application-command modifier.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <returns>True when only Shift and lock-key state may accompany the character.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modifiers"/> sets an unknown flag.</exception>
    public static bool IsTextEntryEligible(Modifiers modifiers)
    {
        ArgumentOutOfRangeException.ThrowIfUndefinedFlags(modifiers, _allModifiers, nameof(modifiers), "The modifier set contains unknown flags.");
        return (modifiers & ~_textEntryModifiers) == 0;
    }

    /// <summary>Reports whether a collection gesture contains only selection and lock modifiers.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <returns>True when Control, Shift, and lock-key state are the only modifiers present.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modifiers"/> sets an unknown flag.</exception>
    public static bool IsCollectionSelectionEligible(Modifiers modifiers)
    {
        ArgumentOutOfRangeException.ThrowIfUndefinedFlags(modifiers, _allModifiers, nameof(modifiers), "The modifier set contains unknown flags.");
        return (modifiers & ~_collectionSelectionModifiers) == 0;
    }

    /// <summary>Reports whether scalar navigation carries only incidental lock-key state.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <returns>True when the normalized command is unmodified.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modifiers"/> sets an unknown flag.</exception>
    public static bool IsScalarNavigationEligible(Modifiers modifiers) =>
        MatchesCommand(modifiers, Modifiers.None);

    /// <summary>Compares a command chord after removing incidental lock-key state.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <param name="expected">The exact command modifiers required by the binding.</param>
    /// <returns>True when the normalized decoded state equals <paramref name="expected"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="modifiers"/> or <paramref name="expected"/> sets an unknown flag.
    /// </exception>
    public static bool MatchesCommand(Modifiers modifiers, Modifiers expected)
    {
        ArgumentOutOfRangeException.ThrowIfUndefinedFlags(modifiers, _allModifiers, nameof(modifiers), "The modifier set contains unknown flags.");
        ArgumentOutOfRangeException.ThrowIfUndefinedFlags(expected, _allModifiers, nameof(expected), "The modifier set contains unknown flags.");
        return (modifiers & ~_lockModifiers) == expected;
    }

    /// <summary>Reports whether Tab carries only direction and incidental lock-key state.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <returns>True for forward or Shift-reverse traversal after lock normalization.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modifiers"/> sets an unknown flag.</exception>
    public static bool IsTabTraversalEligible(Modifiers modifiers) =>
        MatchesCommand(modifiers, Modifiers.None) || MatchesCommand(modifiers, Modifiers.Shift);
}
