using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Input;

/// <summary>Adapts borrowed parser callbacks to one stateful input decoder.</summary>
internal readonly struct Adapter(Decoder owner): ISequenceSink
{
    /// <inheritdoc/>
    public void Text(ReadOnlySpan<byte> value) => owner.AcceptText(value);

    /// <inheritdoc/>
    public void Control(byte value) => owner.AcceptControl(value);

    /// <inheritdoc/>
    public void Escape(ReadOnlySpan<byte> intermediates, byte final) =>
        owner.AcceptEscape(intermediates, final);

    /// <inheritdoc/>
    public void Csi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final) => owner.AcceptCsi(parameters, intermediates, final);

    /// <inheritdoc/>
    public void Sequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        _ = value;
        _ = terminator;
        owner.AcceptSequence(kind);
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
        owner.AcceptDcs();
    }

    /// <inheritdoc/>
    public void Report(in Diagnostic value) => owner.AcceptDiagnostic(in value);
}

