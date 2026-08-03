// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exercises the generic primary-style control facade.</summary>
public sealed class GenericStyleProbe: Control<ButtonStyle>
{
    private static readonly StyleDefinition<ButtonStyle> _definition = StyleDefinitions.Control(
        ThemeRole.Input,
        static profile => new ButtonStyle(ButtonStyle.Standard.Padding, profile),
        static style => style.Appearance,
        static (previous, _, current, _) => previous.Padding == current.Padding
            ? InvalidationImpact.None
            : InvalidationImpact.Measure);

    /// <summary>Initializes one Theme-owned generic styled control.</summary>
    public GenericStyleProbe() : base(_definition)
    {
    }

    /// <summary>Gets the number of resolved-style callbacks published by the typed base.</summary>
    public int StyleChanges { get; private set; }

    /// <inheritdoc/>
    protected override void OnStyleChanged(ButtonStyle previous, ButtonStyle current)
    {
        _ = previous;
        _ = current;
        StyleChanges++;
    }
}
