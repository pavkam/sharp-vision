// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

using InputDiagnostic = Diagnostic;

/// <summary>Counts every decoded terminal input value.</summary>
internal sealed class CountingInputSink: IInputSink
{
    /// <summary>Gets the total callback count.</summary>
    internal int Count { get; private set; }

    /// <inheritdoc/>
    public void Input(in Stroke value) => Count++;

    /// <inheritdoc/>
    public void Input(in InputText value) => Count++;

    /// <inheritdoc/>
    public void Input(in Pointer value) => Count++;

    /// <inheritdoc/>
    public void Input(Paste value) => Count++;

    /// <inheritdoc/>
    public void Input(in Focus value) => Count++;

    /// <inheritdoc/>
    public void Input(in InputDiagnostic value) => Count++;
}
