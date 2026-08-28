// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Centralizes modifier classification for direct keyboard text and command handling.</summary>
internal static class KeyboardModifierPolicy
{
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
    internal static bool IsTextEntryEligible(Modifiers modifiers) =>
        (modifiers & ~_textEntryModifiers) == 0;

    /// <summary>Reports whether a collection gesture contains only selection and lock modifiers.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <returns>True when Control, Shift, and lock-key state are the only modifiers present.</returns>
    internal static bool IsCollectionSelectionEligible(Modifiers modifiers) =>
        (modifiers & ~_collectionSelectionModifiers) == 0;

    /// <summary>Reports whether scalar navigation carries only incidental lock-key state.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <returns>True when the normalized command is unmodified.</returns>
    internal static bool IsScalarNavigationEligible(Modifiers modifiers) =>
        MatchesCommand(modifiers, Modifiers.None);

    /// <summary>Compares a command chord after removing incidental lock-key state.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <param name="expected">The exact command modifiers required by the binding.</param>
    /// <returns>True when the normalized decoded state equals <paramref name="expected"/>.</returns>
    internal static bool MatchesCommand(Modifiers modifiers, Modifiers expected) =>
        (modifiers & ~_lockModifiers) == expected;

    /// <summary>Reports whether Tab carries only direction and incidental lock-key state.</summary>
    /// <param name="modifiers">The decoded modifier state.</param>
    /// <returns>True for forward or Shift-reverse traversal after lock normalization.</returns>
    internal static bool IsTabTraversalEligible(Modifiers modifiers) =>
        MatchesCommand(modifiers, Modifiers.None) || MatchesCommand(modifiers, Modifiers.Shift);
}
