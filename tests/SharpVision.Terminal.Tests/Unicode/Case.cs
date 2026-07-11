namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>Stores one parsed Unicode grapheme conformance case.</summary>
/// <param name="Value">The UTF-16 source text.</param>
/// <param name="Boundaries">The expected UTF-16 boundary offsets.</param>
internal sealed record Case(string Value, int[] Boundaries);
