// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

using SharpVision.Terminal.Input;

/// <summary>Receives typed input, terminal replies, and owned extension strings.</summary>
public interface IProtocolSink: IInputSink
{
    /// <summary>Receives one recognized immutable terminal response.</summary>
    /// <param name="value">The owned numeric response.</param>
    public void Response(in Response value);

    /// <summary>Receives one completed owned terminal string.</summary>
    /// <param name="value">The non-null copied sequence.</param>
    public void Sequence(ProtocolSequence value);
}
