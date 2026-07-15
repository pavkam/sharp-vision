// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Guards the showcase navigation component's public authoring role.</summary>
public sealed class NavigationItemRoleTests
{
    /// <summary>Verifies navigation entries use inherited single content instead of child capacity.</summary>
    [Fact]
    public void Type_WhenInspected_UsesSingleContentPressableRole()
    {
        var type = typeof(NavigationItem);

        typeof(ContentControl).IsAssignableFrom(type).ShouldBeTrue();
        typeof(Container).IsAssignableFrom(type).ShouldBeFalse();
        type.GetProperty("Children").ShouldBeNull();
        var item = new NavigationItem(0, "Button");
        var content = type.GetProperty(nameof(ContentControl.Content)).ShouldNotBeNull().GetValue(item);
        content.ShouldBeOfType<ControlText>().Content.ShouldBe("Button");
    }
}
