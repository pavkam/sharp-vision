// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies Face construction preserves its invariants.</summary>
public sealed class FaceTests
{
    /// <summary>Verifies transparent foregrounds are rejected before a face is constructed.</summary>
    [Fact]
    public void Constructor_WhenForegroundIsTransparent_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new Face(
            Color.Transparent,
            Color.Default,
            TerminalAttributes.None,
            Underline.None,
            Color.Default));
    }

    /// <summary>Verifies a face that names no access-key color carries the theme-wide mnemonic
    /// color, so every face authored before the channel existed behaves exactly as it did.</summary>
    [Fact]
    public void Constructor_WhenAccessKeyColorIsOmitted_DefaultsToTheSemanticHotkey()
    {
        var face = new Face(Color.Default, Color.Transparent, TerminalAttributes.None, Underline.None, Color.Default);

        face.AccessKeyColor.ShouldBe((ControlColor) SemanticColor.Hotkey);
    }

    /// <summary>Verifies the access-key channel is a paint channel: transparent is rejected by the
    /// constructor and by the init accessor a <c>with</c> expression goes through.</summary>
    [Fact]
    public void Constructor_WhenAccessKeyColorIsTransparent_ThrowsArgumentException()
    {
        var face = new Face(Color.Default, Color.Transparent, TerminalAttributes.None, Underline.None, Color.Default, Color.Rgb(1, 2, 3));

        face.AccessKeyColor.ShouldBe((ControlColor) Color.Rgb(1, 2, 3));
        _ = Should.Throw<ArgumentException>(() => new Face(
            Color.Default,
            Color.Transparent,
            TerminalAttributes.None,
            Underline.None,
            Color.Default,
            Color.Transparent));
        _ = Should.Throw<ArgumentException>(() => face with { AccessKeyColor = Color.Transparent });
    }

    /// <summary>Verifies a partial face contribution carries the access-key channel through both
    /// application and later overlay, like every other channel.</summary>
    [Fact]
    public void FaceOverlay_WhenAccessKeyColorIsSupplied_AppliesAndOverlaysIt()
    {
        var face = new Face(Color.Default, Color.Transparent, TerminalAttributes.None, Underline.None, Color.Default);
        var earlier = new FaceOverlay(accessKeyColor: Color.Rgb(1, 1, 1));
        var later = new FaceOverlay(accessKeyColor: Color.Rgb(2, 2, 2));

        earlier.Apply(face).AccessKeyColor.ShouldBe((ControlColor) Color.Rgb(1, 1, 1));
        earlier.Overlay(later).AccessKeyColor.ShouldBe((ControlColor?) Color.Rgb(2, 2, 2));
        earlier.Overlay(new FaceOverlay()).AccessKeyColor.ShouldBe((ControlColor?) Color.Rgb(1, 1, 1));
        new FaceOverlay().Apply(face).AccessKeyColor.ShouldBe((ControlColor) SemanticColor.Hotkey);
    }
}
