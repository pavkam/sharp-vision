// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Provides portable one-cell Unicode separators for <see cref="StatusBarItem"/> chrome.</summary>
[PublicAPI]
public static class StatusBarSeparatorGlyphs
{
    /// <summary>Gets an ordinary blank-cell separator.</summary>
    public static Rune Whitespace { get; } = new(' ');

    /// <summary>Gets a light vertical bar separator.</summary>
    public static Rune Bar { get; } = new('│');

    /// <summary>Gets a centered bullet separator.</summary>
    public static Rune Bullet { get; } = new('•');

    /// <summary>Gets a single right-pointing angle separator.</summary>
    public static Rune Chevron { get; } = new('›');

    /// <summary>Gets a solid diamond separator.</summary>
    public static Rune Diamond { get; } = new('◆');
}
