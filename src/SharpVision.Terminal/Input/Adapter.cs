using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Input;

/// <summary>Adapts borrowed parser callbacks to one stateful input decoder.</summary>
internal readonly struct Adapter: ISequenceSink
{
    private readonly Decoder _owner;

    /// <summary>Initializes an adapter for one non-null decoder.</summary>
    /// <param name="owner">The stateful decoder receiving callbacks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal Adapter(Decoder owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public void Text(ReadOnlySpan<byte> value) => _owner.AcceptText(value);

    /// <inheritdoc/>
    public void Control(byte value) => _owner.AcceptControl(value);

    /// <inheritdoc/>
    public void Escape(ReadOnlySpan<byte> intermediates, byte final) =>
        _owner.AcceptEscape(intermediates, final);

    /// <inheritdoc/>
    public void Csi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final) => _owner.AcceptCsi(parameters, intermediates, final);

    /// <inheritdoc/>
    public void Sequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator) => _owner.AcceptSequence(kind, value, terminator);

    /// <inheritdoc/>
    public void Dcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator) =>
        _owner.AcceptDcs(parameters, intermediates, final, value, terminator);

    /// <inheritdoc/>
    public void Report(in Diagnostic value) => _owner.AcceptDiagnostic(in value);
}
