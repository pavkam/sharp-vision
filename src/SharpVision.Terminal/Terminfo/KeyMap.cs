// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

using Input;

/// <summary>
/// Owns an immutable key-binding snapshot without performing input matching or
/// decoding.
/// </summary>
internal sealed class KeyMap
{
    private readonly KeyBinding[] _bindings;

    /// <summary>Initializes an owned conflict-free key-binding snapshot.</summary>
    /// <param name="bindings">The non-null bindings to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A binding is uninitialized or one exact byte sequence has conflicting logical bindings.
    /// </exception>
    public KeyMap(IReadOnlyList<KeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var unique = new List<KeyBinding>(bindings.Count);

        for (var index = 0; index < bindings.Count; index++)
        {
            var binding = bindings[index];

            if (binding.Sequence.IsEmpty)
            {
                throw new ArgumentException(
                    "Key bindings must contain a non-empty terminal byte sequence.",
                    nameof(bindings));
            }

            var isDuplicate = false;

            foreach (var existing in unique)
            {
                if (!existing.Sequence.Span.SequenceEqual(binding.Sequence.Span))
                {
                    continue;
                }

                if (existing.Code != binding.Code || existing.Modifiers != binding.Modifiers)
                {
                    throw new ArgumentException(
                        "One terminal byte sequence cannot identify conflicting logical keys.",
                        nameof(bindings));
                }

                isDuplicate = true;
                break;
            }

            if (!isDuplicate)
            {
                if (binding.Signature is { } signature)
                {
                    foreach (var existing in unique)
                    {
                        if (existing.Signature is not { } existingSignature ||
                            !existingSignature.Equals(signature))
                        {
                            continue;
                        }

                        if (existing.Code != binding.Code || existing.Modifiers != binding.Modifiers)
                        {
                            throw new ArgumentException(
                                "Equivalent terminal key signatures cannot identify conflicting logical keys.",
                                nameof(bindings));
                        }
                    }
                }

                unique.Add(binding);
            }
        }

        _bindings = [.. unique];
        Bindings = Array.AsReadOnly(_bindings);
        FallbackBindings = Array.AsReadOnly(
            _bindings.Where(static binding => !binding.Signature.HasValue).ToArray());

        foreach (var binding in _bindings)
        {
            if (binding.Signature is not { } signature)
            {
                continue;
            }

            RequiresEightBitCsi |=
                signature.Kind == KeySignatureKind.Csi && binding.Sequence.Span[0] == 0x9b;
            RequiresEightBitSs3 |=
                signature.Kind == KeySignatureKind.Ss3 && binding.Sequence.Span[0] == 0x8f;
        }
    }

    /// <summary>Gets the stable empty key map.</summary>
    public static KeyMap Empty { get; } = new(Array.Empty<KeyBinding>());

    /// <summary>Gets the owned bindings in their first-occurrence order.</summary>
    public IReadOnlyList<KeyBinding> Bindings { get; }

    /// <summary>Gets bindings that require bounded byte-prefix matching.</summary>
    public IReadOnlyList<KeyBinding> FallbackBindings { get; }

    /// <summary>Gets whether the map contains a structural eight-bit CSI spelling.</summary>
    public bool RequiresEightBitCsi { get; }

    /// <summary>Gets whether the map contains a structural eight-bit SS3 spelling.</summary>
    public bool RequiresEightBitSs3 { get; }

    /// <summary>Looks up one structural parser signature.</summary>
    /// <param name="signature">The parsed signature.</param>
    /// <param name="binding">The described logical binding when found.</param>
    /// <returns>Whether the map contains that signature.</returns>
    public bool TryGet(in KeySignature signature, out KeyBinding binding)
    {
        foreach (var candidate in _bindings)
        {
            if (candidate.Signature is { } value && value.Equals(signature))
            {
                binding = candidate;
                return true;
            }
        }

        binding = default;
        return false;
    }

    /// <summary>Looks up one borrowed parser callback without allocating a signature.</summary>
    /// <param name="kind">The parser signature family.</param>
    /// <param name="parameters">Borrowed CSI parameter bytes.</param>
    /// <param name="intermediates">Borrowed Escape or CSI intermediate bytes.</param>
    /// <param name="final">The callback final or control byte.</param>
    /// <param name="binding">The described logical binding when found.</param>
    /// <returns>Whether the map contains that signature.</returns>
    public bool TryGet(
        KeySignatureKind kind,
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        out KeyBinding binding)
    {
        foreach (var candidate in _bindings)
        {
            if (candidate.Signature is not { } signature ||
                signature.Kind != kind ||
                signature.Final != final ||
                !signature.Parameters.SequenceEqual(parameters) ||
                !signature.Intermediates.SequenceEqual(intermediates))
            {
                continue;
            }

            binding = candidate;
            return true;
        }

        binding = default;
        return false;
    }

    /// <summary>
    /// Gets whether an exact retained cursor, Home, or End binding uses the SS3
    /// application-mode spelling selected by <c>smkx</c>. SS3 F1 through F4 are
    /// normal-mode function-key spellings and deliberately do not qualify.
    /// </summary>
    public bool RequiresApplicationMode
    {
        get
        {
            foreach (var binding in _bindings)
            {
                var sequence = binding.Sequence.Span;

                if (sequence.Length == 3 &&
                    sequence[0] == 0x1b &&
                    sequence[1] == (byte) 'O' &&
                    MatchesApplicationCursor(binding.Code, sequence[2]))
                {
                    return true;
                }

                if (sequence.Length == 2 &&
                    sequence[0] == 0x8f &&
                    MatchesApplicationCursor(binding.Code, sequence[1]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static bool MatchesApplicationCursor(Code code, byte final) =>
        (code, final) switch
        {
            (Code.Up, (byte) 'A') => true,
            (Code.Down, (byte) 'B') => true,
            (Code.Right, (byte) 'C') => true,
            (Code.Left, (byte) 'D') => true,
            (Code.Home, (byte) 'H') => true,
            (Code.End, (byte) 'F') => true,
            _ => false
        };
}
