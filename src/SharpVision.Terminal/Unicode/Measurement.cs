namespace SharpVision.Terminal.Unicode;

/// <summary>Reports whole-span terminal cell measurement.</summary>
/// <param name="Cells">The printable terminal-cell count.</param>
/// <param name="Graphemes">The extended-grapheme count.</param>
/// <param name="Controls">The contextual control-cluster count.</param>
public readonly record struct Measurement(int Cells, int Graphemes, int Controls);
