// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies a role color cannot enter a renderable cell style.</summary>
public sealed class CellStyleRoleGuardTests
{
    /// <summary>Verifies a role foreground is rejected before state assignment.</summary>
    [Fact]
    public void Constructor_WhenForegroundIsRole_Throws() =>
        Should.Throw<ArgumentException>(() => new CellStyle(foreground: Color.Role(1)));

    /// <summary>Verifies a role background is rejected before state assignment.</summary>
    [Fact]
    public void Constructor_WhenBackgroundIsRole_Throws() =>
        Should.Throw<ArgumentException>(() => new CellStyle(background: Color.Role(1)));

    /// <summary>Verifies a role underline color is rejected before state assignment.</summary>
    [Fact]
    public void Constructor_WhenUnderlineColorIsRole_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new CellStyle(underline: Underline.Curly, underlineColor: Color.Role(1)));

    /// <summary>Verifies concrete foreground and background colors are accepted.</summary>
    [Fact]
    public void Constructor_WhenConcreteColors_Succeeds() =>
        Should.NotThrow(() => new CellStyle(foreground: Color.Rgb(1, 2, 3), background: Color.Indexed(4)));
}
