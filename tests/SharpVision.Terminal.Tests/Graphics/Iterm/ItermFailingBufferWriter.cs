// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Iterm;
/// <summary>Fails every buffer request to prove destination exception fidelity.</summary>
internal sealed class ItermFailingBufferWriter: IBufferWriter<byte>
{
    /// <inheritdoc />
    public void Advance(int count) => throw new InvalidOperationException("destination failure");

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0) =>
        throw new InvalidOperationException("destination failure");

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0) =>
        throw new InvalidOperationException("destination failure");
}
