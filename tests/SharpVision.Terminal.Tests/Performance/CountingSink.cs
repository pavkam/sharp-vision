// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Performance;

using SharpVision.Terminal.Input;

/// <summary>Counts every input callback in allocation measurements.</summary>
internal sealed class CountingSink: IInputSink
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
    public void Input(in TerminalFocus value) => Count++;

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => Count++;
}
