// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using Kitty.Clipboard;

/// <summary>Feeds parsed Kitty OSC packets into one transaction.</summary>
internal sealed class TransactionSink: ISequenceSink
{
    private readonly Transaction _transaction;

    /// <summary>Initializes a sink for one active transaction.</summary>
    /// <param name="transaction">The non-null transaction receiving packets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
    internal TransactionSink(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _transaction = transaction;
    }

    /// <inheritdoc/>
    public void Text(ReadOnlySpan<byte> value) => _ = value;

    /// <inheritdoc/>
    public void Control(byte value) => _ = value;

    /// <inheritdoc/>
    public void Escape(ReadOnlySpan<byte> intermediates, byte final)
    {
        _ = intermediates;
        _ = final;
    }

    /// <inheritdoc/>
    public void Csi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        _ = parameters;
        _ = intermediates;
        _ = final;
    }

    /// <inheritdoc/>
    public void Sequence(SequenceKind kind, ReadOnlySpan<byte> value, StringTerminator terminator)
    {
        _ = terminator;

        if (kind == SequenceKind.Osc)
        {
            _ = _transaction.Accept(Packet.Parse(value));
        }
    }

    /// <inheritdoc/>
    public void Dcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        _ = parameters;
        _ = intermediates;
        _ = final;
        _ = value;
        _ = terminator;
    }

    /// <inheritdoc/>
    public void Report(in Diagnostic value) => _ = value;
}
