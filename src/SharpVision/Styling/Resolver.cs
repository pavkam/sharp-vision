using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Styling;

/// <summary>Resolves deterministic field overlays into semantic terminal style.</summary>
public static class Resolver
{
    private const State _allStates =
        State.Hovered | State.Focused | State.Checked | State.Pressed | State.Disabled;

    /// <summary>Overlays all active state definitions in documented precedence.</summary>
    /// <param name="style">The optional resource; null resolves to an empty appearance.</param>
    /// <param name="state">The defined active state flag set.</param>
    /// <returns>The combined optional appearance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The state contains unknown flags.</exception>
    public static Appearance Resolve(Style? style, State state)
    {
        if ((state & ~_allStates) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The state contains unknown flags.");
        }

        if (style is null)
        {
            return default;
        }

        var result = style.TryGet(State.Normal, out var normal) ? normal : default;
        result = Apply(style, state, State.Hovered, result);
        result = Apply(style, state, State.Focused, result);
        result = Apply(style, state, State.Checked, result);
        result = Apply(style, state, State.Pressed, result);
        return Apply(style, state, State.Disabled, result);
    }

    /// <summary>Converts optional appearance fields to a complete terminal style.</summary>
    /// <param name="appearance">The resolved optional appearance.</param>
    /// <returns>A complete semantic terminal style.</returns>
    public static TerminalStyle ToTerminal(Appearance appearance) => new(
        appearance.Foreground ?? Color.Default,
        appearance.Background ?? Color.Default,
        appearance.Attributes ?? Attributes.None);

    private static Appearance Apply(
        Style style,
        State active,
        State candidate,
        Appearance current) =>
        (active & candidate) != 0 && style.TryGet(candidate, out var overlay)
            ? current.Overlay(overlay)
            : current;
}
