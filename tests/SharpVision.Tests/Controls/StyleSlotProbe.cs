// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exercises the public style-slot authoring surface through a Button-shaped style.</summary>
public sealed class StyleSlotProbe: ControlBase, IStyled<ButtonStyle>
{
    private readonly StyleSlot<ButtonStyle> _style;

    /// <summary>Initializes a Theme-owned probe.</summary>
    public StyleSlotProbe()
    {
        var definition = StyleDefinitions.Control(
            static theme => theme.GetStyleSet(InputStyle.Default),
            Complete,
            static (previous, _, current, _) => previous.Padding == current.Padding
                ? InvalidationImpact.None
                : InvalidationImpact.Measure);
        _style = InitializeStyle(definition);
    }

    /// <summary>Gets or sets the complete local style.</summary>
    public ButtonStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved style.</summary>
    public ButtonStyle ActualStyle => _style.Actual;

    /// <summary>Gets the number of fallback completions performed by this probe.</summary>
    public int CompletionCalls { get; private set; }

    private ButtonStyle Complete(InputStyle input, VisualState state, Theme theme)
    {
        CompletionCalls++;
        return new ButtonStyle(input.Face, input.Border, input.Shadow, ButtonStyle.Standard.Padding);
    }
}
