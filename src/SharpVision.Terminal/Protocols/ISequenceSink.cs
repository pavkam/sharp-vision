namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Receives synchronous borrowed views of parsed ECMA-48 sequences.
/// </summary>
/// <remarks>
/// Every span is valid only until its callback returns. Implementations must
/// copy data that needs to outlive the call. Callbacks run synchronously on the
/// parser's caller thread and must not invoke the same parser recursively.
/// </remarks>
public interface ISequenceSink
{
    /// <summary>Receives bytes that are not terminal controls.</summary>
    /// <param name="value">Borrowed text bytes in transport order.</param>
    public void Text(ReadOnlySpan<byte> value);

    /// <summary>Receives one C0 or configured eight-bit control.</summary>
    /// <param name="value">The raw control byte.</param>
    public void Control(byte value);

    /// <summary>Receives a completed ESC sequence.</summary>
    /// <param name="intermediates">Borrowed intermediate bytes.</param>
    /// <param name="final">The final byte.</param>
    public void Escape(ReadOnlySpan<byte> intermediates, byte final);

    /// <summary>Receives a completed CSI sequence.</summary>
    /// <param name="parameters">Borrowed raw parameter bytes.</param>
    /// <param name="intermediates">Borrowed intermediate bytes.</param>
    /// <param name="final">The final byte.</param>
    public void Csi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final);

    /// <summary>Receives a completed OSC, APC, PM, or SOS sequence.</summary>
    /// <param name="kind">The string sequence family.</param>
    /// <param name="value">Borrowed payload bytes.</param>
    /// <param name="terminator">The observed terminator.</param>
    public void Sequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator);

    /// <summary>Receives a completed DCS sequence.</summary>
    /// <param name="parameters">Borrowed raw parameter bytes.</param>
    /// <param name="intermediates">Borrowed intermediate bytes.</param>
    /// <param name="final">The DCS final byte.</param>
    /// <param name="value">Borrowed payload bytes.</param>
    /// <param name="terminator">The observed terminator.</param>
    public void Dcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator);

    /// <summary>Receives a structured non-sensitive parser diagnostic.</summary>
    /// <param name="value">The diagnostic value.</param>
    public void Report(in Diagnostic value);
}
