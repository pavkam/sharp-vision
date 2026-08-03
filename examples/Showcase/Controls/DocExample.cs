// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

using TextControl = SharpVision.Controls.Display.Text;

/// <summary>Builds one example block: a bold heading and dim description above a live specimen.</summary>
internal sealed class DocExample: CompositeControlBase
{
    /// <summary>Initializes one labeled showcase example with an optional collapsed source recipe.</summary>
    /// <param name="heading">The example heading.</param>
    /// <param name="descriptionMarkup">The trusted authored markup describing what the specimen demonstrates.</param>
    /// <param name="specimen">The live control specimen.</param>
    /// <param name="source">An optional compact C# excerpt that reproduces the specimen's essential setup.</param>
    /// <exception cref="ArgumentException"><paramref name="heading"/>, <paramref name="descriptionMarkup"/>, or a supplied <paramref name="source"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="specimen"/> is null.</exception>
    internal DocExample(
        string heading,
        string descriptionMarkup,
        ControlBase specimen,
        string? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionMarkup);
        ArgumentNullException.ThrowIfNull(specimen);

        if (source is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source);
        }

        var text = new TextControl(
            $"<b>{TextControl.Escape(heading)}</b>\n<d>{descriptionMarkup}</d>")
        {
            Overflow = Overflow.Wrap
        };

        var specimenSurface = new GroupBox
        {
            Header = "Example",
            UseMnemonic = false,
            Padding = new Thickness(1),
            Content = new Stack { Children = { specimen } }
        };
        var block = new Stack { Spacing = 1 };
        block.Children.Add(text);
        block.Children.Add(specimenSurface);

        if (source is not null)
        {
            var code = new TextControl($"<info><b>C#</b></info>\n{TextControl.Escape(source)}")
            {
                Overflow = Overflow.WrapAnywhere
            };
            var recipe = new Dock
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Light,
                    ThemeColor.ControlBorder,
                    Color.Transparent,
                    ThemeDecoration.Border),
                Padding = new Thickness(1, 0),
                Children = { code }
            };
            block.Children.Add(new Expander
            {
                Header = "C# recipe",
                Content = recipe,
                IsExpanded = false,
                UseMnemonic = false
            });
        }

        InitializeContent(block);
    }
}
