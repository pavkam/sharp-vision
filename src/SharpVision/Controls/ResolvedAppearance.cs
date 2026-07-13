namespace SharpVision.Controls;

using TerminalStyle = Terminal.Rendering.Style;

internal readonly struct ResolvedAppearance
{
    internal TerminalStyle Style { get; init; }

    internal bool HasOpaqueFill { get; init; }
}
