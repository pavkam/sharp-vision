// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies NavigationViewItem's StartAffix/EndAffix measure reservation and invalidation
/// grading, detached from any owning NavigationView.</summary>
public sealed class NavigationViewItemTests
{
    /// <summary>Verifies documented affix defaults for an empty item.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasNoAffixes()
    {
        var item = new NavigationViewItem();

        item.StartAffix.ShouldBeNull();
        item.EndAffix.ShouldBeNull();
    }

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus the
    /// shared style gap, over an equivalent affix-less item.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedExtraWidth)
    {
        var baseline = new NavigationViewItem { Text = "Page" };
        new LayoutEngine().Layout(baseline, new Size(40, 3));

        var item = new NavigationViewItem
        {
            Text = "Page",
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };
        new LayoutEngine().Layout(item, new Size(40, 3));

        item.DesiredSize.Width.ShouldBe(baseline.DesiredSize.Width + expectedExtraWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        using var item = new NavigationViewItem { Text = "Page" };
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("!");

        item.Pending.ShouldBe(Invalidation.All);
        item.Clear(Invalidation.All);

        item.StartAffix = null;

        item.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only.</summary>
    [Fact]
    public void StartAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        using var item = new NavigationViewItem { Text = "Page", StartAffix = new Affix("|") };
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("/");

        item.Pending.ShouldBe(Invalidation.Render);
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("/", "?", SemanticColor.Warning);

        item.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change invalidates Measure again, not just Render.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        using var item = new NavigationViewItem { Text = "Page", EndAffix = new Affix("!") };
        item.Clear(Invalidation.All);

        // U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        item.EndAffix = new Affix("世");

        item.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        var affix = new Affix("!");
        using var item = new NavigationViewItem { Text = "Page", StartAffix = affix };
        item.Clear(Invalidation.All);

        item.StartAffix = affix;

        item.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies a local style's AffixGap difference is graded Measure, not just Render -
    /// the same distinction <see cref="ButtonStyle"/>'s own comparer draws - isolated
    /// from every other style member by starting from an already-assigned identical style.</summary>
    [Fact]
    public void Style_WhenAffixGapChanges_InvalidatesMeasure()
    {
        using var item = new NavigationViewItem
        {
            Text = "Page",
            Style = NavigationViewItemStyle.Default
        };
        item.Clear(Invalidation.All);

        item.Style = NavigationViewItemStyle.Default with { AffixGap = 2 };

        item.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a local style difference that does not touch AffixGap still invalidates
    /// rendering only, unaffected by the new AffixGap-specific comparer branch.</summary>
    [Fact]
    public void Style_WhenMarkerChangesButAffixGapDoesNot_InvalidatesRenderOnly()
    {
        using var item = new NavigationViewItem
        {
            Text = "Page",
            Style = NavigationViewItemStyle.Default
        };
        item.Clear(Invalidation.All);

        item.Style = NavigationViewItemStyle.Default with { IdleMarker = new Rune('*') };

        item.Pending.ShouldBe(Invalidation.Render);
    }
}
