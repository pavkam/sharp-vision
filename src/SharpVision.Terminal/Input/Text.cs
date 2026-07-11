using System.Text;

namespace SharpVision.Terminal.Input;

/// <summary>Represents one decoded Unicode scalar intended for text input.</summary>
/// <param name="Value">The valid Unicode scalar value.</param>
public readonly record struct Text(Rune Value);
