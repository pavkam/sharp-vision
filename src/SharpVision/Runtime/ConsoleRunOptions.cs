namespace SharpVision.Runtime;

/// <summary>Configures one interactive console screen run.</summary>
public sealed record ConsoleRunOptions
{
    /// <summary>Gets the optional message written when standard input or output is redirected.</summary>
    public string? RedirectedMessage { get; init; }
}
