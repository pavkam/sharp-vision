namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Protocols;

/// <summary>Counts CSI callbacks in parser allocation measurements.</summary>
internal struct CountingSink: ISequenceSink
{
    /// <summary>Gets the number of observed CSI callbacks.</summary>
    public int CsiCount { get; private set; }

    /// <inheritdoc/>
    public readonly void Text(ReadOnlySpan<byte> value) => _ = value;

    /// <inheritdoc/>
    public readonly void Control(byte value) => _ = value;

    /// <inheritdoc/>
    public readonly void Escape(ReadOnlySpan<byte> intermediates, byte final)
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
        CsiCount++;
    }

    /// <inheritdoc/>
    public readonly void Sequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        _ = kind;
        _ = value;
        _ = terminator;
    }

    /// <inheritdoc/>
    public readonly void Dcs(
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
    public readonly void Report(in Diagnostic value) => _ = value;
}
