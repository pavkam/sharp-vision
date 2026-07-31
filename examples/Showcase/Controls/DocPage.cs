// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

using TextControl = SharpVision.Controls.Display.Text;

/// <summary>Builds a page root: a heading with an Overview summary, then the given sections.</summary>
internal sealed class DocPage: CompositeControl
{
    /// <summary>Initializes one showcase documentation page.</summary>
    /// <param name="name">The exact control/page name shown as the heading.</param>
    /// <param name="overviewMarkup">The trusted authored markup shown under the heading.</param>
    /// <param name="sections">The example/section controls, in display order.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="overviewMarkup"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="sections"/> or one of its entries is null.</exception>
    internal DocPage(string name, string overviewMarkup, params Control[] sections)
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
        InitializeContent(new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { header, body }
        });
    }
}
