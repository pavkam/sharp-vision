namespace SharpVision.Terminal.Unicode;

/// <summary>Identifies one extended grapheme cluster inside a borrowed UTF-16 span.</summary>
/// <param name="Offset">The zero-based UTF-16 code-unit offset.</param>
/// <param name="Length">The positive UTF-16 code-unit length.</param>
/// <param name="HasInvalidData">
/// Whether the segment contains one invalid code unit represented as U+FFFD.
/// </param>
public readonly record struct Grapheme(int Offset, int Length, bool HasInvalidData);
