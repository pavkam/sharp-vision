// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies the immutable Breadcrumb item presentation.</summary>
public sealed class BreadcrumbItemStyleTests
{
    /// <summary>Verifies the default is the complete borderless interactive-row presentation.</summary>
    [Fact]
    public void Default_WhenRead_IsComplete()
    {
        var style = BreadcrumbItemStyle.Default;

        style.Border.Sides.ShouldBe(BorderSide.None);
        style.Shadow.IsVisible.ShouldBeFalse();
    }

    /// <summary>Verifies a changed item presentation requires rendering only.</summary>
    [Fact]
    public void Definition_WhenPresentationChanges_InvalidatesRender()
    {
        var previous = BreadcrumbItemStyle.Default;
        var current = previous with { Face = previous.Face with { Attributes = TerminalAttributes.Bold } };

        BreadcrumbItemStyle.Definition.Compare(previous, null, current, null)
            .ShouldBe(InvalidationImpact.Render);
    }
}
