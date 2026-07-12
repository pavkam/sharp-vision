using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

internal readonly struct ResolvedAppearance
{
    internal TerminalStyle Style { get; init; }

    internal bool HasOpaqueFill { get; init; }
}
