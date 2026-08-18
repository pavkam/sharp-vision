// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exercises the framework-owned primary-style facade over a directly declared
/// <see cref="IStyled{TStyle}"/> contract.</summary>
public sealed class GenericStyleProbe: ControlBase, IStyled<ButtonStyle>
{
    private static readonly StyleDefinition<ButtonStyle> _definition = StyleDefinitions.Control(
        "test.genericStyleProbe",
        static theme => theme.GetStyleSet(InputStyle.Default),
        static (input, _) => new ButtonStyle(input.Face, input.Border, input.Shadow, ButtonStyle.Standard.Padding),
        static (previous, _, current, _) => previous.Padding == current.Padding
            ? InvalidationImpact.None
            : InvalidationImpact.Measure);

    private readonly StyleSlot<ButtonStyle> _style;

    /// <summary>Initializes one Theme-owned generic styled control.</summary>
    public GenericStyleProbe() => _style = InitializeStyle(_definition, OnStyleChanged);

    /// <summary>Gets or sets the complete local style, or null to use the definition fallback.</summary>
    public ButtonStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the cached complete local, Theme-owned, or fallback style.</summary>
    public ButtonStyle ActualStyle => _style.Actual;

    /// <summary>Gets the number of resolved-style callbacks published by the framework facade.</summary>
    public int StyleChanges { get; private set; }

    private void OnStyleChanged(ButtonStyle previous, ButtonStyle current)
    {
        _ = previous;
        _ = current;
        StyleChanges++;
    }
}
