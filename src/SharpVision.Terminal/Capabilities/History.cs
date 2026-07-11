namespace SharpVision.Terminal.Capabilities;

/// <summary>Stores one bounded recent query outcome.</summary>
/// <param name="Outcome">The terminal query outcome.</param>
/// <param name="Until">The exclusive retention deadline.</param>
internal readonly record struct History(Outcome Outcome, DateTimeOffset Until);
