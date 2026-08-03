// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase;

using Text;

using TextControl = SharpVision.Controls.Display.Text;

/// <summary>Small composable helpers for building example-rich showcase pages.</summary>
internal static class Doc
{
    /// <summary>Builds one progressive documentation section with ordered examples.</summary>
    /// <param name="icon">The intentional emoji prefix that identifies the section.</param>
    /// <param name="heading">The section heading.</param>
    /// <param name="descriptionMarkup">The trusted authored markup shown beneath the heading.</param>
    /// <param name="examples">The live examples in reading order.</param>
    /// <returns>A vertically stacked section.</returns>
    /// <exception cref="ArgumentException"><paramref name="icon"/>, <paramref name="heading"/>, or <paramref name="descriptionMarkup"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="examples"/> or one of its entries is null.</exception>
    internal static ControlBase Section(
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

        var introduction = new TextControl(
            $"<accent><b>{TextControl.Escape(icon)} {TextControl.Escape(heading)}</b></accent>\n" +
            descriptionMarkup)
        { Overflow = Overflow.Wrap };
        var section = new Stack { Spacing = 1 };
        section.Children.Add(introduction);

        foreach (var example in examples)
        {
            section.Children.Add(example);
        }

        return section;
    }

    /// <summary>Builds a page root: a heading with an Overview summary, then the given sections.</summary>
    /// <param name="name">The exact control/page name shown as the heading.</param>
    /// <param name="overviewMarkup">The trusted authored markup shown under the heading.</param>
    /// <param name="sections">The example/section controls, in display order.</param>
    /// <returns>A page with a fixed identity header and an independently scrolling example body.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="overviewMarkup"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="sections"/> is null.</exception>
    internal static Dock Page(string name, string overviewMarkup, params ControlBase[] sections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(overviewMarkup);
        ArgumentNullException.ThrowIfNull(sections);

        var heading = new TextControl(
            $"<accent><b>{TextControl.Escape(name)}</b></accent>\n" +
            overviewMarkup)
        { Overflow = Overflow.Wrap };
        var header = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { heading }
        };
        var body = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            Padding = new Thickness(1),
            Spacing = 1
        };

        foreach (var section in sections)
        {
            ArgumentNullException.ThrowIfNull(section);
            body.Children.Add(section);
        }

        Dock.SetSide(header, DockSide.Top);
        return new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { header, body }
        };
    }

    /// <summary>Builds one example block: a bold heading and dim description above a live specimen.</summary>
    /// <param name="heading">The example heading.</param>
    /// <param name="descriptionMarkup">The trusted authored markup describing what the specimen demonstrates.</param>
    /// <param name="specimen">The live control specimen.</param>
    /// <param name="source">An optional compact C# excerpt that reproduces the specimen's essential setup.</param>
    /// <returns>A vertically stacked example block.</returns>
    /// <exception cref="ArgumentException"><paramref name="heading"/>, <paramref name="descriptionMarkup"/>, or a supplied <paramref name="source"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="specimen"/> is null.</exception>
    internal static ControlBase Example(
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

        return block;
    }

    /// <summary>Wraps a specimen in a rounded bordered card.</summary>
    /// <param name="child">The specimen to frame.</param>
    /// <returns>A bordered card.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    internal static Dock Card(ControlBase child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return new Dock
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { child }
        };
    }

    /// <summary>Stacks children horizontally with standard spacing.</summary>
    /// <param name="children">The children in order.</param>
    /// <returns>A horizontal stack.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> is null.</exception>
    internal static Stack Row(params ControlBase[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        var row = new Stack { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var child in children)
        {
            row.Children.Add(child);
        }

        return row;
    }

    /// <summary>Stacks children vertically with standard spacing.</summary>
    /// <param name="children">The children in order.</param>
    /// <returns>A vertical stack.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> is null.</exception>
    internal static Stack Column(params ControlBase[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        var column = new Stack { Spacing = 1 };
        foreach (var child in children)
        {
            column.Children.Add(child);
        }

        return column;
    }

    /// <summary>Removes mnemonic markers while preserving escaped literal ampersands.</summary>
    /// <param name="caption">The non-null authored access-text caption.</param>
    /// <returns>The visible caption text.</returns>
    internal static string PlainCaption(string caption)
    {
        ArgumentNullException.ThrowIfNull(caption);
        var result = new StringBuilder(caption.Length);

        for (var index = 0; index < caption.Length; index++)
        {
            if (caption[index] != '&')
            {
                _ = result.Append(caption[index]);
                continue;
            }

            if (index + 1 >= caption.Length)
            {
                _ = result.Append('&');
                continue;
            }

            if (index + 1 < caption.Length && caption[index + 1] == '&')
            {
                _ = result.Append('&');
                index++;
            }
        }

        return result.ToString();
    }
}
