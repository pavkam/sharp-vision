// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies the shared xterm-family terminal-name recognition helper.</summary>
public sealed class TerminalNamesTests
{
    /// <summary>Verifies every documented xterm-compatible name, including a multiplexer-prefixed
    /// xterm value and the "st"/"st-" exact-prefix carve-out, is recognized.</summary>
    [Theory]
    [InlineData("xterm")]
    [InlineData("xterm-256color")]
    [InlineData("screen.xterm-256color")]
    [InlineData("alacritty")]
    [InlineData("foot")]
    [InlineData("foot-extra")]
    [InlineData("rxvt")]
    [InlineData("rxvt-unicode-256color")]
    [InlineData("wezterm")]
    [InlineData("st")]
    [InlineData("st-256color")]
    [InlineData("konsole-256color")]
    [InlineData("vte-256color")]
    [InlineData("gnome-256color")]
    [InlineData("contour")]
    [InlineData("putty-256color")]
    [InlineData("mintty")]
    [InlineData("iterm2")]
    [InlineData("ghostty")]
    public void IsXtermFamily_WhenNameIsRecognized_ReturnsTrue(string name) =>
        TerminalNames.IsXtermFamily(name).ShouldBeTrue(name);

    /// <summary>Verifies unrelated terminal names, an absent/empty name, a Kitty identity that also
    /// contains "xterm", and "stterm" — which starts with "st" but is neither the exact name nor
    /// "st-" prefixed, so it is deliberately excluded to avoid matching arbitrary names — are all
    /// rejected.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("linux")]
    [InlineData("dumb")]
    [InlineData("screen-256color")]
    [InlineData("tmux-256color")]
    [InlineData("vt100")]
    [InlineData("stterm")]
    [InlineData("xterm-kitty")]
    [InlineData("kitty")]
    public void IsXtermFamily_WhenNameIsNotRecognized_ReturnsFalse(string? name) =>
        TerminalNames.IsXtermFamily(name).ShouldBeFalse(name ?? "<null>");
}
