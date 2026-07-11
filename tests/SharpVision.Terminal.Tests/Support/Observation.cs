using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Tests.Support;

/// <summary>Represents one copied parser callback for deterministic comparisons.</summary>
/// <param name="Type">The callback type.</param>
/// <param name="First">The first borrowed byte field.</param>
/// <param name="Second">The second borrowed byte field.</param>
/// <param name="Final">The sequence final byte.</param>
/// <param name="Diagnostic">The structured diagnostic, when present.</param>
public sealed record Observation(
    string Type,
    byte[] First,
    byte[] Second,
    byte Final,
    Diagnostic? Diagnostic);
