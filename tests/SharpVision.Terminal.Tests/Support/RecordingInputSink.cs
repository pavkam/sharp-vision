// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;

using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;

/// <summary>
/// Retains typed input callbacks for deterministic decoder assertions.
/// </summary>
internal sealed class RecordingInputSink: IInputSink
{
    /// <summary>Gets key strokes in delivery order.</summary>
    internal List<Stroke> Strokes { get; } = [];

    /// <summary>Gets text values in delivery order.</summary>
    internal List<Text> Text { get; } = [];

    /// <summary>Gets pointer values in delivery order.</summary>
    internal List<Pointer> Pointers { get; } = [];

    /// <summary>Gets owned paste values in delivery order.</summary>
    internal List<Paste> Pastes { get; } = [];

    /// <summary>Gets focus values in delivery order.</summary>
    internal List<Focus> Focus { get; } = [];

    /// <summary>Gets redacted diagnostics in delivery order.</summary>
    internal List<Diagnostic> Diagnostics { get; } = [];

    /// <inheritdoc/>
    public void Input(in Stroke value) => Strokes.Add(value);

    /// <inheritdoc/>
    public void Input(in Text value) => Text.Add(value);

    /// <inheritdoc/>
    public void Input(in Pointer value) => Pointers.Add(value);

    /// <inheritdoc/>
    public void Input(Paste value) => Pastes.Add(value);

    /// <inheritdoc/>
    public void Input(in Focus value) => Focus.Add(value);

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => Diagnostics.Add(value);
}
