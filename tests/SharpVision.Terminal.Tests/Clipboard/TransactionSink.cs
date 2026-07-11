using SharpVision.Terminal.Clipboard;
using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Tests.Clipboard;

/// <summary>Feeds parsed Kitty OSC packets into one transaction.</summary>
internal sealed class TransactionSink(KittyTransaction transaction): ISequenceSink
{
    private readonly KittyTransaction _transaction = transaction;

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
            _ = _transaction.Accept(KittyPacket.Parse(value));
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
