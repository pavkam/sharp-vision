// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Terminal.Rendering;
using SharpVision.Text;

/// <summary>Small composable helpers for building example-rich showcase pages.</summary>
internal static class Doc
{
    /// <summary>Builds a page root: a heading with an Overview summary, then the given sections.</summary>
    /// <param name="name">The exact control/page name shown as the heading.</param>
    /// <param name="overview">The one- or two-sentence overview shown under the heading.</param>
    /// <param name="sections">The example/section controls, in display order.</param>
    /// <returns>A vertically stacked page root.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="overview"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="sections"/> is null.</exception>
    internal static Stack Page(string name, string overview, params Control[] sections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(overview);
        ArgumentNullException.ThrowIfNull(sections);

        RichText heading = new() { Wrapping = Wrapping.Word };
        heading.Inlines.Add(new Run(name) { Attributes = Attributes.Bold });
        heading.Inlines.Add(new LineBreak());
        heading.Inlines.Add(new Run("Overview") { Attributes = Attributes.Bold });
        heading.Inlines.Add(new LineBreak());
        heading.Inlines.Add(new Run(overview));

        Stack page = new() { Padding = new Thickness(1), Spacing = 1 };
        page.Children.Add(heading);

        foreach (Control section in sections)
        {
            page.Children.Add(section);
        }

        return page;
    }

    /// <summary>Builds one example block: a bold heading and dim description above a live specimen.</summary>
    /// <param name="heading">The example heading.</param>
    /// <param name="description">The prose describing what the specimen demonstrates.</param>
    /// <param name="specimen">The live control specimen.</param>
    /// <returns>A vertically stacked example block.</returns>
    /// <exception cref="ArgumentException"><paramref name="heading"/> or <paramref name="description"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="specimen"/> is null.</exception>
    internal static Control Example(string heading, string description, Control specimen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(specimen);

        RichText text = new() { Wrapping = Wrapping.Word };
        text.Inlines.Add(new Run(heading) { Attributes = Attributes.Bold });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new Run(description) { Attributes = Attributes.Dim });

        Stack block = new() { Spacing = 1 };
        block.Children.Add(text);
        block.Children.Add(specimen);
        return block;
    }

    /// <summary>Wraps a specimen in a rounded bordered card.</summary>
    /// <param name="child">The specimen to frame.</param>
    /// <returns>A bordered card.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    internal static Border Card(Control child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return new Border
        {
            Child = child,
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            Padding = new Thickness(1, 0),
        };
    }

    /// <summary>Stacks children horizontally with standard spacing.</summary>
    /// <param name="children">The children in order.</param>
    /// <returns>A horizontal stack.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> is null.</exception>
    internal static Stack Row(params Control[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Stack row = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (Control child in children)
        {
            row.Children.Add(child);
        }

        return row;
    }

    /// <summary>Stacks children vertically with standard spacing.</summary>
    /// <param name="children">The children in order.</param>
    /// <returns>A vertical stack.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> is null.</exception>
    internal static Stack Column(params Control[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Stack column = new() { Spacing = 1 };
        foreach (Control child in children)
        {
            column.Children.Add(child);
        }

        return column;
    }
}
