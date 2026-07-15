// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;



/// <summary>
/// Receives ordered terminal input, protocol, resize, closure, and fault events.
/// </summary>
public interface ISink: IProtocolSink
{
    /// <summary>Publishes one immutable active capability profile.</summary>
    /// <param name="value">The non-null immutable profile.</param>
    /// <exception cref="NotSupportedException">
    /// The sink did not implement negotiated profile publication.
    /// </exception>
    public void Profile(TerminalCapabilities value) =>
        throw new NotSupportedException(
            "This runtime sink does not accept capability profiles.");

    /// <summary>Receives one immutable terminal dimension change.</summary>
    /// <param name="value">The new cell and optional pixel dimensions.</param>
    public void Resize(in Dimensions value);

    /// <summary>Reports orderly terminal input closure once.</summary>
    public void Closed();

    /// <summary>Reports one terminal runtime failure.</summary>
    /// <param name="exception">The non-null original failure.</param>
    public void Fault(Exception exception);
}
