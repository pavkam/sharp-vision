// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

using Input;

/// <summary>Associates one exact retained terminal byte sequence with a logical key.</summary>
internal readonly record struct KeyBinding
{
    private readonly byte[]? _sequence;

    /// <summary>Initializes one validated immutable key binding.</summary>
    /// <param name="sequence">The non-empty exact terminal bytes to copy.</param>
    /// <param name="code">The logical key produced by the bytes.</param>
    /// <param name="modifiers">The logical modifiers encoded by the bytes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="sequence"/> is empty or begins parser control grammar without
    /// forming one complete supported signature.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> is undefined or <paramref name="modifiers"/> contains an unknown flag.
    /// </exception>
    public KeyBinding(
        ReadOnlySpan<byte> sequence,
        Code code,
        Modifiers modifiers = Modifiers.None) : this(
        sequence,
        code,
        modifiers,
        ParserLimits.Default)
    {
    }

    /// <summary>Initializes one immutable key binding against active parser limits.</summary>
    /// <param name="sequence">The non-empty exact terminal bytes to copy.</param>
    /// <param name="code">The logical key produced by the bytes.</param>
    /// <param name="modifiers">The logical modifiers encoded by the bytes.</param>
    /// <param name="limits">The non-null parser limits that must admit structural signatures.</param>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sequence"/> is empty, unreachable, or exceeds a structural parser limit.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> is undefined or <paramref name="modifiers"/> contains an unknown flag.
    /// </exception>
    public KeyBinding(
        ReadOnlySpan<byte> sequence,
        Code code,
        Modifiers modifiers,
        ParserLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (sequence.IsEmpty)
        {
            throw new ArgumentException("A terminal key sequence cannot be empty.", nameof(sequence));
        }

        code.ValidateDefined(nameof(code), "The logical key code is unknown.");

        if ((modifiers & ~_allModifiers) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifiers),
                modifiers,
                "The logical key modifier set contains unknown flags.");
        }

        var hasSignature = KeySignature.TryCreate(sequence, limits, out var signature);

        if (!hasSignature && IsParserControl(sequence[0]))
        {
            throw new ArgumentException(
                "A parser-control-prefixed key string must be one complete parser signature.",
                nameof(sequence));
        }

        _sequence = sequence.ToArray();
        Code = code;
        Modifiers = modifiers;
        Signature = hasSignature ? signature : null;
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

    /// <summary>Gets the owned exact terminal key bytes without text decoding.</summary>
    public ReadOnlyMemory<byte> Sequence => _sequence ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Gets the logical key produced by the bytes.</summary>
    public Code Code { get; }

    /// <summary>Gets the logical modifiers encoded by the bytes.</summary>
    public Modifiers Modifiers { get; }

    /// <summary>Gets the parser signature, or null when longest-match byte decoding is required.</summary>
    public KeySignature? Signature { get; }

    private static bool IsParserControl(byte value) =>
        value is < 0x20 or 0x7f or (>= 0x80 and <= 0x9f);
}
