// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Single source of truth for overlay visual states and their resolution order.</summary>
/// <remarks>
/// Both the mutable-style validation and the resolver derive their overlay knowledge from here so
/// the set and precedence of states cannot drift across the codebase. Overlays are listed in
/// ascending precedence: when two equally specific state values define the same property, the one
/// later in <see cref="PrecedenceOrder"/> wins.
/// </remarks>
internal static class VisualStates
{
    /// <summary>Overlay states in ascending precedence (later overrides earlier at equal specificity).</summary>
    internal static readonly State[] PrecedenceOrder =
    [
        State.Hovered,
        State.Focused,
        State.Selected,
        State.Checked,
        State.Indeterminate,
        State.Pressed,
        State.Disabled,
    ];

    /// <summary>Every overlay flag combined; the complement identifies unknown bits.</summary>
    internal static readonly State Overlays = Combine(PrecedenceOrder);

    /// <summary>Gets the ascending precedence rank of one overlay flag.</summary>
    /// <param name="overlay">A single overlay flag.</param>
    /// <returns>The index in <see cref="PrecedenceOrder"/>, or -1 when not an overlay.</returns>
    internal static int RankOf(State overlay) => Array.IndexOf(PrecedenceOrder, overlay);

    private static State Combine(State[] states)
    {
        var result = State.Normal;

        foreach (var state in states)
        {
            result |= state;
        }

        return result;
    }
}
