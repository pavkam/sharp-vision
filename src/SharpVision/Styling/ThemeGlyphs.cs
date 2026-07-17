// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Represents one complete immutable semantic glyph palette.</summary>
public sealed class ThemeGlyphs
{
    /// <summary>Initializes every required semantic glyph group.</summary>
    /// <param name="chrome">The border, shadow, and window chrome glyphs.</param>
    /// <param name="progress">The progress-track, fill, and fraction glyphs.</param>
    /// <param name="disclosure">The disclosure and drop-down glyphs.</param>
    /// <param name="selection">The checkbox, radio, and menu-selection glyphs.</param>
    /// <param name="navigation">The navigation item, group, and separator glyphs.</param>
    /// <param name="scrollBars">The scrollbar button, track, and thumb glyphs.</param>
    /// <param name="separators">The general, menu, table, and tab separator glyphs.</param>
    /// <param name="text">The framework-authored text glyphs.</param>
    /// <exception cref="ArgumentNullException"><paramref name="progress"/> is null.</exception>
    public ThemeGlyphs(
        ChromeGlyphs chrome,
        ProgressGlyphs progress,
        DisclosureGlyphs disclosure,
        SelectionGlyphs selection,
        NavigationGlyphs navigation,
        ScrollBarGlyphs scrollBars,
        SeparatorGlyphs separators,
        TextGlyphs text)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Chrome = chrome;
        Progress = progress;
        Disclosure = disclosure;
        Selection = selection;
        Navigation = navigation;
        ScrollBars = scrollBars;
        Separators = separators;
        Text = text;
    }

    /// <summary>Gets border, shadow, and window chrome glyphs.</summary>
    public ChromeGlyphs Chrome { get; }
    /// <summary>Gets progress glyphs.</summary>
    public ProgressGlyphs Progress { get; }
    /// <summary>Gets disclosure glyphs.</summary>
    public DisclosureGlyphs Disclosure { get; }
    /// <summary>Gets selection glyphs.</summary>
    public SelectionGlyphs Selection { get; }
    /// <summary>Gets navigation glyphs.</summary>
    public NavigationGlyphs Navigation { get; }
    /// <summary>Gets scrollbar glyphs.</summary>
    public ScrollBarGlyphs ScrollBars { get; }
    /// <summary>Gets separator glyphs.</summary>
    public SeparatorGlyphs Separators { get; }
    /// <summary>Gets framework text glyphs.</summary>
    public TextGlyphs Text { get; }
}
