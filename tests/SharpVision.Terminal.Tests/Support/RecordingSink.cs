// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;

/// <summary>
/// Copies borrowed parser callbacks into stable ordered test observations.
/// </summary>
public sealed class RecordingSink: ISequenceSink
{
    /// <summary>
    /// Gets callbacks in delivery order.
    /// </summary>
    public List<Observation> Observations { get; } = [];

    /// <inheritdoc/>
    public void Text(ReadOnlySpan<byte> value) =>
        Observations.Add(new Observation("Text", value.ToArray(), [], 0, null));

    /// <inheritdoc/>
    public void Control(byte value) =>
        Observations.Add(new Observation("Control", [value], [], 0, null));

    /// <inheritdoc/>
    public void Escape(ReadOnlySpan<byte> intermediates, byte final) =>
        Observations.Add(new Observation("Escape", intermediates.ToArray(), [], final, null));

    /// <inheritdoc/>
    public void Csi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final) =>
        Observations.Add(
            new Observation("Csi", parameters.ToArray(), intermediates.ToArray(), final, null));

    /// <inheritdoc/>
    public void Sequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator) =>
        Observations.Add(
            new Observation($"{kind}:{terminator}", value.ToArray(), [], 0, null));

    /// <inheritdoc/>
    public void Dcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator) =>
        Observations.Add(
            new Observation(
                $"Dcs:{terminator}:{Convert.ToHexString(intermediates)}",
                parameters.ToArray(),
                value.ToArray(),
                final,
                null));

    /// <inheritdoc/>
    public void Report(in Diagnostic value) =>
        Observations.Add(new Observation("Diagnostic", [], [], 0, value));
}
