// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

using SharpVision.Controls.SyntaxHighlighting;

using Text = SharpVision.Controls.Display.Text;

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

        var text = new Text(
            $"<b>{Text.Escape(heading)}</b>\n<d>{descriptionMarkup}</d>")
        {
            Overflow = Overflow.Wrap
        };

        var specimenSurface = new GroupBox
        {
            HeaderText = "Example",
            UseMnemonic = false,
            Padding = new Thickness(1),
            Content = new Stack { Children = { specimen } }
        };
        var block = new Stack { Spacing = 1 };
        block.Children.Add(text);
        block.Children.Add(specimenSurface);

        if (source is not null)
        {
            // Sized to the recipe's own line count rather than a fixed constant, so a short
            // one-liner does not reserve the same vertical space as a dozen-line excerpt; longer
            // recipes still get real scroll bars past the cap instead of growing unboundedly.
            // CodeView always reserves its own top and bottom border row regardless of theme, so
            // the requested height adds 2 rows beyond the line count actually shown - omitting
            // this made every recipe one visible content row short of its own line count, forcing
            // a scroll bar even a two- or three-line recipe should never need. The Expander
            // already supplies its own border, so the recipe needs no bordered wrapper of its own.
            const int borderRows = 2;
            var lineCount = source.Count(static c => c == '\n') + 1;
            var code = new CodeView
            {
                Code = source,
                Language = "C#",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = Length.Cells(Math.Clamp(lineCount + borderRows, 3 + borderRows, 20 + borderRows)),
                ScrollBars = ScrollBars.Both,
                ShowScrollBars = ShowScrollBars.WhenNeeded
            };
            block.Children.Add(new Expander
            {
                HeaderText = "C# recipe",
                Content = code,
                IsExpanded = false,
                UseMnemonic = false
            });
        }

        InitializeContent(block);
    }
}
