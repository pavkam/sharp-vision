// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies detached navigation-group state, validation, and typed ownership.</summary>
public sealed class NavigationViewGroupTests
{
    /// <summary>Verifies a group starts expanded and delegates keyboard focus to its owning view.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedInteractionDefaults()
    {
        var group = new NavigationViewGroup();

        group.Header.ShouldBe(string.Empty);
        group.IsExpanded.ShouldBeTrue();
        group.IsFocusable.ShouldBeFalse();
        group.IsTabStop.ShouldBeFalse();
        group.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies header validation and child ownership commit atomically at the public boundary.</summary>
    [Fact]
    public void HeaderAndItems_WhenMutated_ValidateTextAndTransferOwnership()
    {
        var group = new NavigationViewGroup { Header = "Tools" };
        var item = new NavigationViewItem { Text = "Build" };

        group.Items.Add(item);

        group.Header.ShouldBe("Tools");
        var presentationParent = item.Parent.ShouldNotBeNull();
        presentationParent.ShouldNotBeSameAs(group);
        group.Items.ShouldContain(item);

        _ = Should.Throw<ArgumentException>(() => group.Header = "Bad\nHeader");

        group.Header.ShouldBe("Tools");
        group.Items.Remove(item).ShouldBeTrue();
        item.Parent.ShouldBeNull();
    }

    /// <summary>Verifies a rendered enabled group registers a render-only dependency on the root
    /// theme's hotkey color, matching the fix Text's own hotkey resolution already carries.</summary>
    [Fact]
    public void Theme_WhenGroupIsRenderedAndEnabled_InvalidatesOnlyRenderForHotkeyChange()
    {
        // Arrange
        var mounted = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var replacement = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var group = new NavigationViewGroup { Header = "&Tools" };
        group.SetTheme(mounted);
        new LayoutEngine().Layout(group, new Size(20, 8));
        using Frame frame = new(new Size(20, 8));
        group.Render(frame.Canvas);
        group.Clear(Invalidation.All);

        // Act
        group.SetTheme(replacement);

        // Assert
        group.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a disabled group never resolves the hotkey color, so it does not
    /// subscribe to an unrelated root hotkey-only change.</summary>
    [Fact]
    public void Theme_WhenGroupIsRenderedAndDisabled_IgnoresHotkeyOnlyChange()
    {
        // Arrange
        var mounted = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var replacement = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var group = new NavigationViewGroup { Header = "&Tools", IsEnabled = false };
        group.SetTheme(mounted);
        new LayoutEngine().Layout(group, new Size(20, 8));
        using Frame frame = new(new Size(20, 8));
        group.Render(frame.Canvas);
        group.Clear(Invalidation.All);

        // Act
        group.SetTheme(replacement);

        // Assert
        group.Pending.ShouldBe(Invalidation.None);
    }
}
