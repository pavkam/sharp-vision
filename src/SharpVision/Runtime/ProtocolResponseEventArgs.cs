using SharpVision.Terminal.Protocols;

namespace SharpVision.Runtime;

/// <summary>Provides one dispatcher-affine typed terminal protocol response.</summary>
public sealed class ProtocolResponseEventArgs: EventArgs
{
    /// <summary>Initializes an event payload for one immutable decoded response.</summary>
    /// <param name="response">The typed response received from the terminal.</param>
    public ProtocolResponseEventArgs(Response response) => Response = response;

    /// <summary>Gets the immutable decoded terminal response.</summary>
    public Response Response { get; }
}
