// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>Owns the structural parser signature of one described terminal key.</summary>
internal readonly struct KeySignature: IEquatable<KeySignature>
{
    // Removes every configured parameter/intermediate byte ceiling so IsStructurallyRepresentable
    // can isolate a pure byte-value question ("could any signature ever describe these bytes?")
    // from a length-limit question ("does the active profile admit this many of them?"). Every
    // count TryCreate ever compares against these fields is already bounded by the sequence's own
    // length, so int.MaxValue never changes which sequences pass for a reason other than length.
    private static readonly ParserLimits _unbounded = ParserLimits.Default with
    {
        MaxParameterBytes = int.MaxValue,
        MaxIntermediateBytes = int.MaxValue
    };

    private readonly byte[]? _parameters;
    private readonly byte[]? _intermediates;

    private KeySignature(
        KeySignatureKind kind,
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        Kind = kind;
        _parameters = parameters.ToArray();
        _intermediates = intermediates.ToArray();
        Final = final;
    }

    /// <summary>Gets the signature family.</summary>
    public KeySignatureKind Kind { get; }

    /// <summary>Gets the owned CSI parameter bytes.</summary>
    public ReadOnlySpan<byte> Parameters => _parameters;

    /// <summary>Gets the owned Escape or CSI intermediate bytes.</summary>
    public ReadOnlySpan<byte> Intermediates => _intermediates;

    /// <summary>Gets the control, SS3, Escape, or CSI final byte.</summary>
    public byte Final { get; }

    /// <summary>Creates one control signature from a parser callback.</summary>
    [Pure]
    public static KeySignature Control(byte value) =>
        new(KeySignatureKind.Control, [], [], value);

    /// <summary>Creates one Escape signature from a parser callback.</summary>
    [Pure]
    public static KeySignature Escape(ReadOnlySpan<byte> intermediates, byte final) =>
        new(KeySignatureKind.Escape, [], intermediates, final);

    /// <summary>Creates one CSI signature from a parser callback.</summary>
    [Pure]
    public static KeySignature Csi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final) => new(KeySignatureKind.Csi, parameters, intermediates, final);

    /// <summary>Creates one SS3 signature from a parser callback.</summary>
    [Pure]
    public static KeySignature Ss3(byte final) =>
        new(KeySignatureKind.Ss3, [], [], final);

    /// <summary>Compiles one exact description sequence when it is one parser-level signature.</summary>
    /// <param name="sequence">The non-empty terminal bytes.</param>
    /// <param name="signature">The compiled signature when representable.</param>
    /// <returns>Whether the sequence is exactly one supported parser signature.</returns>
    [Pure]
    public static bool TryCreate(ReadOnlySpan<byte> sequence, out KeySignature signature)
        => TryCreate(sequence, ParserLimits.Default, out signature);

    /// <summary>Compiles one exact description sequence against active parser limits.</summary>
    /// <param name="sequence">The non-empty terminal bytes.</param>
    /// <param name="limits">The non-null active parser limits.</param>
    /// <param name="signature">The compiled signature when representable and reachable.</param>
    /// <returns>Whether the sequence is exactly one admitted parser signature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is null.</exception>
    [Pure]
    public static bool TryCreate(
        ReadOnlySpan<byte> sequence,
        ParserLimits limits,
        out KeySignature signature)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (sequence.Length == 1 && (sequence[0] < 0x20 || sequence[0] == 0x7f))
        {
            signature = new KeySignature(KeySignatureKind.Control, [], [], sequence[0]);
            return true;
        }

        if (sequence.Length == 3 && sequence[0] == ControlBytes.Escape && sequence[1] == (byte) 'O' &&
            sequence[2] is >= 0x30 and <= 0x7e)
        {
            signature = new KeySignature(KeySignatureKind.Ss3, [], [], sequence[2]);
            return true;
        }

        if (sequence.Length == 2 && sequence[0] == 0x8f &&
            sequence[1] is >= 0x30 and <= 0x7e)
        {
            signature = new KeySignature(KeySignatureKind.Ss3, [], [], sequence[1]);
            return true;
        }

        if (TryCsi(sequence, limits, out signature))
        {
            return true;
        }

        if (sequence.Length >= 2 && sequence[0] == ControlBytes.Escape)
        {
            var body = sequence[1..];
            var final = body[^1];
            var intermediates = body[..^1];

            if (final is >= 0x30 and <= 0x7e &&
                intermediates.Length <= limits.MaxIntermediateBytes &&
                !(intermediates.IsEmpty && IsIntroducer(final)) &&
                AllInRange(intermediates, 0x20, 0x2f))
            {
                signature = new KeySignature(
                    KeySignatureKind.Escape,
                    [],
                    intermediates,
                    final);
                return true;
            }
        }

        signature = default;
        return false;
    }

    /// <summary>
    /// Determines whether a sequence could compile into some supported parser signature under the
    /// most permissive possible parameter and intermediate byte limits. A parser-control-prefixed
    /// terminal key string that fails here has byte values ECMA-48 structural grammar can never
    /// describe at any limit - such as the Linux virtual console's <c>ESC [ [ &lt;letter&gt;</c>
    /// function keys or rxvt-unicode's <c>ESC [ &lt;n&gt; $</c> Shift-modified navigation keys -
    /// which is a different, permanent failure from one that merely exceeds the active profile's
    /// configured limit and would succeed under a larger one.
    /// </summary>
    /// <param name="sequence">The non-empty terminal bytes.</param>
    /// <returns>Whether any choice of limits could compile the sequence into a signature.</returns>
    [Pure]
    public static bool IsStructurallyRepresentable(ReadOnlySpan<byte> sequence) =>
        TryCreate(sequence, _unbounded, out _);

    /// <inheritdoc/>
    public bool Equals(KeySignature other) =>
        Kind == other.Kind &&
        Final == other.Final &&
        Parameters.SequenceEqual(other.Parameters) &&
        Intermediates.SequenceEqual(other.Intermediates);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KeySignature other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Final);

        foreach (var value in Parameters)
        {
            hash.Add(value);
        }

        foreach (var value in Intermediates)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    private static bool TryCsi(
        ReadOnlySpan<byte> sequence,
        ParserLimits limits,
        out KeySignature signature)
    {
        ReadOnlySpan<byte> body;

        if (sequence.Length >= 3 && sequence[0] == ControlBytes.Escape && sequence[1] == (byte) '[')
        {
            body = sequence[2..];
        }
        else if (sequence.Length >= 2 && sequence[0] == 0x9b)
        {
            body = sequence[1..];
        }
        else
        {
            signature = default;
            return false;
        }

        var final = body[^1];

        if (final is < 0x40 or > 0x7e)
        {
            signature = default;
            return false;
        }

        var header = body[..^1];
        var intermediateStart = 0;

        while (intermediateStart < header.Length && header[intermediateStart] is >= 0x30 and <= 0x3f)
        {
            intermediateStart++;
        }

        var parameters = header[..intermediateStart];
        var intermediates = header[intermediateStart..];

        if (parameters.Length > limits.MaxParameterBytes ||
            intermediates.Length > limits.MaxIntermediateBytes ||
            !AllInRange(intermediates, 0x20, 0x2f))
        {
            signature = default;
            return false;
        }

        signature = new KeySignature(KeySignatureKind.Csi, parameters, intermediates, final);
        return true;
    }

    private static bool IsIntroducer(byte final) =>
        final is (byte) '[' or (byte) ']' or (byte) 'P' or (byte) '_' or
            (byte) '^' or (byte) 'X' or (byte) 'O';

    private static bool AllInRange(ReadOnlySpan<byte> value, byte minimum, byte maximum)
    {
        foreach (var item in value)
        {
            if (item < minimum || item > maximum)
            {
                return false;
            }
        }

        return true;
    }
}
