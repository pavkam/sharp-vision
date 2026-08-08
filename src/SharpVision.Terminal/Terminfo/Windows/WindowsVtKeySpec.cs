// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Windows;

using Input;

/// <summary>Defines one immutable built-in Windows VT key sequence.</summary>
internal sealed class WindowsVtKeySpec
{
    /// <summary>Initializes one validated built-in key specification.</summary>
    /// <param name="sequence">The non-empty exact terminal sequence represented as immutable text.</param>
    /// <param name="code">The defined logical key.</param>
    /// <param name="modifiers">The known logical modifiers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sequence"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sequence"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> is undefined or <paramref name="modifiers"/> contains an unknown flag.
    /// </exception>
    public WindowsVtKeySpec(
        string sequence,
        Code code,
        Modifiers modifiers = Modifiers.None)
    {
        ArgumentException.ThrowIfNullOrEmpty(sequence);

        code.ValidateDefined(nameof(code), "The logical key code is unknown.");

        if ((modifiers & ~_allModifiers) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifiers),
                modifiers,
                "The logical key modifier set contains unknown flags.");
        }

        Sequence = sequence;
        Code = code;
        Modifiers = modifiers;
    }

    private const Modifiers _allModifiers =
        Modifiers.Shift |
        Modifiers.Alt |
        Modifiers.Control |
        Modifiers.Super |
        Modifiers.Hyper |
        Modifiers.Meta |
        Modifiers.CapsLock |
        Modifiers.NumLock;

    /// <summary>Gets the exact immutable terminal sequence text.</summary>
    public string Sequence { get; }

    /// <summary>Gets the logical key produced by the sequence.</summary>
    public Code Code { get; }

    /// <summary>Gets the logical modifiers encoded by the sequence.</summary>
    public Modifiers Modifiers { get; }
}
