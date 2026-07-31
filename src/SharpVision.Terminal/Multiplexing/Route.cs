// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Multiplexing;

using Input;

using Kitty;

using Protocols;

/// <summary>Applies one immutable bounded typed multiplexer route.</summary>
[PublicAPI]
public sealed class Route
{
    private const int _screenFramingBytes = 4;
    private const int _tmuxFramingBytes = 9;

    /// <summary>Initializes one route from explicit policy.</summary>
    /// <param name="policy">The non-null owned immutable policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public Route(Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Policy = policy;
    }

    /// <summary>Gets the route's explicit policy.</summary>
    public Policy Policy { get; }

    /// <summary>Gets whether this route can carry a bounded typed capability-query batch.</summary>
    internal bool CanRouteCapabilityQueries =>
        Policy.Allows(Operation.CapabilityQueries) && Policy.HasSafeScreenTopology;

    /// <summary>Gets whether OSC and DCS query families retain their terminators on this route.</summary>
    internal bool SupportsStringTerminatedQueries => !Policy.ContainsScreen;

    /// <summary>Gets whether bounded graphics strings can reach the explicit outer terminal.</summary>
    internal bool CanRouteGraphics => Policy.Allows(Operation.Graphics) && !Policy.ContainsScreen;

    /// <summary>Gets the exact largest inner graphics frame for a known ESC count.</summary>
    /// <param name="escapeBytes">The non-negative ESC byte count in the complete inner frame.</param>
    /// <returns>A positive inner byte bound, or zero when graphics cannot be routed.</returns>
    internal int GetMaximumGraphicsFrameBytes(int escapeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(escapeBytes);

        if (!CanRouteGraphics)
        {
            return 0;
        }

        var overhead = 0L;
        var escapes = (long) escapeBytes;

        for (var index = Policy.Layers.Count - 1; index >= 0; index--)
        {
            Debug.Assert(Policy.Layers[index] == Kind.Tmux, "Graphics routes reject Screen.");
            overhead = checked(overhead + escapes + _tmuxFramingBytes);
            escapes = checked((escapes * 2) + 2);
        }

        var maximum = Policy.MaxEnvelopeBytes - overhead;
        return maximum is > 0 and <= int.MaxValue ? (int) maximum : 0;
    }

    /// <summary>Writes one already validated bounded capability-query batch through every authorized layer.</summary>
    /// <param name="destination">The non-null synchronous byte destination.</param>
    /// <param name="queries">The complete typed query batch produced by the capability negotiator.</param>
    /// <returns>True when the complete route was encoded; otherwise false without destination mutation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    internal bool TryWriteCapabilityQueries(
        IBufferWriter<byte> destination,
        ReadOnlySpan<byte> queries)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return CanRouteCapabilityQueries &&
               !queries.IsEmpty &&
               queries.Length <= Policy.MaxEnvelopeBytes &&
               (!Policy.ContainsScreen || IsCsiQueryBatch(queries)) &&
               TryWritePassthrough(destination, queries, allowScreen: true);
    }

    /// <summary>Writes one complete bounded graphics string through authorized tmux layers.</summary>
    /// <param name="destination">The non-null synchronous destination.</param>
    /// <param name="commands">The complete backend-produced APC, DCS, or OSC string.</param>
    /// <returns>True on complete atomic encoding; otherwise false without destination mutation.</returns>
    internal bool TryWriteGraphics(IBufferWriter<byte> destination, ReadOnlySpan<byte> commands)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return CanRouteGraphics &&
               !commands.IsEmpty &&
               commands.Length <= Policy.MaxEnvelopeBytes &&
               TryWritePassthrough(destination, commands, allowScreen: false);
    }

    private bool TryWritePassthrough(
        IBufferWriter<byte> destination,
        ReadOnlySpan<byte> value,
        bool allowScreen)
    {
        // Every accepted layer's encoded length is bounded by Policy.MaxEnvelopeBytes (checked
        // below before it is ever written), so two pooled buffers sized to that one bound —
        // ping-ponged as the read side and the write side of each layer — replace what used to be
        // one fresh heap array per layer plus a starting and ending copy (see #41).
        using var bufferA = new BoundedWriter(Policy.MaxEnvelopeBytes);
        using var bufferB = new BoundedWriter(Policy.MaxEnvelopeBytes);
        var current = value;
        var useA = true;

        try
        {
            for (var index = Policy.Layers.Count - 1; index >= 0; index--)
            {
                if (Policy.Layers[index] == Kind.Screen &&
                    (!allowScreen || !IsCsiQueryBatch(current)))
                {
                    return false;
                }

                var encodedLength = EncodedLength(Policy.Layers[index], current);

                if (encodedLength > Policy.MaxEnvelopeBytes)
                {
                    return false;
                }

                var next = useA ? bufferA : bufferB;
                next.Reset(encodedLength);

                if (Policy.Layers[index] == Kind.Tmux)
                {
                    TmuxWriter.WritePassthrough(next, current);
                }
                else
                {
                    Screen.WritePassthrough(next, current);
                }

                current = next.WrittenSpan;
                useA = !useA;
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        var output = destination.GetSpan(current.Length);
        current.CopyTo(output);
        destination.Advance(current.Length);
        return true;
    }

    /// <summary>Unwraps one complete authorized query reply through every configured layer.</summary>
    /// <param name="envelope">The complete bounded outer envelope.</param>
    /// <param name="reply">The newly owned inner reply on success.</param>
    /// <returns>True when every configured layer and the final reply are valid.</returns>
    public bool TryUnwrapReply(ReadOnlySpan<byte> envelope, out ReadOnlyMemory<byte> reply)
    {
        reply = default;

        if (!CanRouteCapabilityQueries ||
            envelope.IsEmpty ||
            envelope.Length > Policy.MaxEnvelopeBytes)
        {
            return false;
        }

        var current = envelope.ToArray();

        for (var index = 0; index < Policy.Layers.Count; index++)
        {
            var next = new ArrayBufferWriter<byte>();
            var unwrapped = Policy.Layers[index] == Kind.Tmux
                ? TmuxWriter.TryUnwrapEnvelope(current, next)
                : Screen.TryUnwrap(current, next);

            if (!unwrapped || next.WrittenCount > Policy.MaxEnvelopeBytes)
            {
                return false;
            }

            current = next.WrittenSpan.ToArray();
        }

        if (Policy.ContainsScreen &&
            (current.Length < 2 || current[0] != ControlBytes.Escape || current[1] != (byte) '['))
        {
            return false;
        }

        if (!IsCompleteQueryReply(current))
        {
            return false;
        }

        reply = current;
        return true;
    }

    /// <summary>Gets the exact outer prefix used to recognize routed query replies.</summary>
    internal ReadOnlySpan<byte> ReplyPrefix => Policy.Kind switch
    {
        Kind.None => [],
        Kind.Tmux => "\u001bPtmux;"u8,
        Kind.Screen => "\u001bP\u001b"u8,
        _ => throw new UnreachableException("Policy validation rejects unknown multiplexer kinds.")
    };

    /// <summary>Gets whether an outer terminator can end at the current suffix.</summary>
    /// <param name="candidate">The current candidate bytes.</param>
    /// <returns>True when the nearest layer may terminate at the suffix.</returns>
    internal bool MayEnd(ReadOnlySpan<byte> candidate)
    {
        if (candidate.Length < 2 || candidate[^1] != (byte) '\\')
        {
            return false;
        }

        var escapes = 0;

        for (var index = candidate.Length - 2; index >= 0 && candidate[index] == ControlBytes.Escape; index--)
        {
            escapes++;
        }

        return Policy.Kind == Kind.Screen ? escapes > 0 : (escapes & 1) != 0;
    }

    /// <summary>Gets whether a rejected candidate reached its complete outer recovery boundary.</summary>
    /// <param name="candidate">The bounded complete-or-partial route candidate.</param>
    /// <returns>True when no further byte belongs to the rejected envelope.</returns>
    internal bool IsCompleteRecoveryEnvelope(ReadOnlySpan<byte> candidate)
    {
        if (Policy.Kind == Kind.Tmux)
        {
            return MayEnd(candidate);
        }

        var payload = new ArrayBufferWriter<byte>();

        if (!Screen.TryUnwrap(candidate, payload))
        {
            return false;
        }

        var inner = payload.WrittenSpan;

        return inner.Length < 2 ||
               inner[0] != ControlBytes.Escape ||
               inner[1] switch
               {
                   (byte) 'P' => inner.EndsWith("\u001b\\"u8),
                   (byte) ']' => inner[^1] == 0x07 || inner.EndsWith("\u001b\\"u8),
                   _ => true
               };
    }

    private static bool IsCompleteQueryReply(ReadOnlySpan<byte> reply)
    {
        var sink = new ReplyValidationSink();
        using var decoder = new Decoder(sink);
        decoder.Decode(reply);
        decoder.Complete();
        return sink.IsValid;
    }

    private static bool IsCsiQueryBatch(ReadOnlySpan<byte> input)
    {
        var position = 0;

        while (position < input.Length)
        {
            if (position + 2 >= input.Length ||
                input[position] != ControlBytes.Escape ||
                input[position + 1] != (byte) '[')
            {
                return false;
            }

            position += 2;
            var intermediates = false;
            var complete = false;

            while (position < input.Length)
            {
                var value = input[position++];

                if (value is >= 0x40 and <= 0x7e)
                {
                    complete = true;
                    break;
                }

                if (value is >= 0x30 and <= 0x3f && !intermediates)
                {
                    continue;
                }

                if (value is >= 0x20 and <= 0x2f)
                {
                    intermediates = true;
                    continue;
                }

                return false;
            }

            if (!complete)
            {
                return false;
            }
        }

        return position > 0;
    }

    private static int EncodedLength(Kind kind, ReadOnlySpan<byte> input)
    {
        if (kind == Kind.Screen)
        {
            return checked(input.Length + _screenFramingBytes);
        }

        var escapes = 0;

        foreach (var value in input)
        {
            if (value == ControlBytes.Escape)
            {
                escapes++;
            }
        }

        return checked(input.Length + escapes + _tmuxFramingBytes);
    }
}
