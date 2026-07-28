// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines optional member-wise contributions to a complete Button presentation.</summary>
[PublicAPI]
public readonly record struct ButtonStyleSet
{
    /// <summary>Initializes a partial Button presentation contribution.</summary>
    /// <param name="padding">The optional replacement internal content padding.</param>
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    public ButtonStyleSet(Thickness? padding = null, AppearanceProfileSet? appearance = null)
    {
        Padding = padding;
        Appearance = appearance;
    }

    /// <summary>Gets the optional replacement internal content padding.</summary>
    public Thickness? Padding { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete Button presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    /// <exception cref="ArgumentException">The composed appearance has a reachable state with both a visible shadow and an enabled border side.</exception>
    public ButtonStyle Apply(ButtonStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new ButtonStyle(Padding ?? baseline.Padding, appearance);
    }
}

