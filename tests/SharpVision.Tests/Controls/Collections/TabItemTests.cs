// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TabItem header validation and content ownership.</summary>
public sealed class TabItemTests
{
    /// <summary>Verifies a new page rejects invalid header text before mutation.</summary>
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidHeader_PreserveDefaults()
    {
        var item = new TabItem();

        item.HeaderText.ShouldBe(string.Empty);
        item.Content.ShouldBeNull();
        item.IsClosable.ShouldBeFalse();

        _ = Should.Throw<ArgumentNullException>(() => item.HeaderText = null!);

        item.HeaderText.ShouldBe(string.Empty);
    }

    /// <summary>Verifies a header carrying a terminal control character is rejected instead of
    /// silently dropping post-newline text or shifting later cells at render time.</summary>
    [Theory]
    [InlineData("Save\nAs")]
    [InlineData("Save\rAs")]
    [InlineData("Save\tAs")]
    public void HeaderText_WhenContainingControlCharacter_Throws(string header)
    {
        var item = new TabItem();

        _ = Should.Throw<ArgumentException>(() => item.HeaderText = header);

        item.HeaderText.ShouldBe(string.Empty);
    }

    /// <summary>Verifies direct and owning-TabControl-inherited IsEnabled changes flip a TabItem's
    /// EffectiveIsEnabled without disturbing its own IsEnabled property, and re-enabling restores it.</summary>
    [Fact]
    public void Enabled_WhenToggledDirectlyOrByOwningTabControl_UpdatesEffectiveEnabled()
    {
        var item = new TabItem { HeaderText = "Page", IsEnabled = false };

        item.EffectiveIsEnabled.ShouldBeFalse();

        item.IsEnabled = true;

        item.EffectiveIsEnabled.ShouldBeTrue();

        var tabs = new TabControl();
        tabs.Items.Add(item);

        tabs.IsEnabled = false;

        item.IsEnabled.ShouldBeTrue();
        item.EffectiveIsEnabled.ShouldBeFalse();

        tabs.IsEnabled = true;

        item.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies caller content replacement transfers ownership.</summary>
    [Fact]
    public void Content_WhenReplaced_TransfersOnlyCallerContentOwnership()
    {
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var item = new TabItem { HeaderText = "Page", Content = first };

        item.Content = second;

        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(item);
    }

}
