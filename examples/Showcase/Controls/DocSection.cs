// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Builds one progressive documentation section with ordered examples.</summary>
internal sealed class DocSection: CompositeControlBase
{
    /// <summary>Initializes one documentation section with an introduction and ordered examples.</summary>
    /// <param name="icon">The intentional emoji prefix that identifies the section.</param>
    /// <param name="heading">The section heading.</param>
    /// <param name="descriptionMarkup">The trusted authored markup shown beneath the heading.</param>
    /// <param name="examples">The live examples in reading order.</param>
    /// <exception cref="ArgumentException"><paramref name="icon"/>, <paramref name="heading"/>, or <paramref name="descriptionMarkup"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="examples"/> or one of its entries is null.</exception>
    internal DocSection(
        string icon,
        string heading,
        string descriptionMarkup,
        params ControlBase[] examples)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionMarkup);
        ArgumentNullException.ThrowIfNull(examples);

        foreach (var example in examples)
        {
            ArgumentNullException.ThrowIfNull(example);
        }

        var introduction = new Text(
            $"<accent><b>{Text.Escape(icon)} {Text.Escape(heading)}</b></accent>\n" +
            descriptionMarkup)
        { Overflow = Overflow.Wrap };
        var section = new Stack { Spacing = 1 };
        section.Children.Add(introduction);

        foreach (var example in examples)
        {
            section.Children.Add(example);
        }

        InitializeContent(section);
    }
}
