// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the shared scrolling contract for composite components with a private
/// scrolling host.</summary>
public sealed class ScrollableCompositeControlBaseTests
{
    /// <summary>Verifies setting the semantic owner's VerticalOffset scrolls the private host and
    /// raises the owner's own PropertyChanged, without a caller needing to know about the host.</summary>
    [Fact]
    public void VerticalOffset_WhenSetOnOwner_RaisesOwnerPropertyChangedAndScrollsHost()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));
        List<string?> changed = [];
        probe.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        // Act
        probe.VerticalOffset = 2;

        // Assert
        probe.VerticalOffset.ShouldBe(2);
        probe.ScrollableHost.VerticalOffset.ShouldBe(2);
        changed.ShouldContain(nameof(ScrollableCompositeControlProbe.VerticalOffset));
    }

    /// <summary>Verifies a committed host scroll transition is republished through the owner's own
    /// ScrollChanged with the owner - never the private host - as sender.</summary>
    [Fact]
    public void ScrollChanged_WhenHostScrolls_IsRaisedWithOwnerAsSender()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));
        object? sender = null;
        ScrollChangedEventArgs? observed = null;
        probe.ScrollChanged += (candidate, eventArgs) =>
        {
            sender = candidate;
            observed = eventArgs;
        };

        // Act
        var moved = probe.ScrollBy(0, 3);

        // Assert
        moved.ShouldBeTrue();
        sender.ShouldBeSameAs(probe);
        var eventArgs = observed.ShouldNotBeNull();
        eventArgs.Offset.ShouldBe(new Point(0, 3));
    }

    /// <summary>Verifies a second attempt to install the scrolling content host throws instead of
    /// silently replacing or duplicating the first installation.</summary>
    [Fact]
    public void InitializeScrollableContent_WhenCalledTwice_Throws()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(probe.InitializeScrollableContentAgain);
    }

    /// <summary>Verifies PageDown maps through the host's own key-scroll mapping and advances the
    /// owner's VerticalOffset by exactly one page step.</summary>
    [Fact]
    public void HandleScrollKey_WhenPageDownWithContentToScroll_ScrollsHostByPageStep()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));
        var expectedStep = Math.Max(1, probe.ScrollableHost.Viewport.Height - probe.PageOverlap);

        // Act
        var handled = probe.RaiseScrollKey(Code.PageDown);

        // Assert
        handled.ShouldBeTrue();
        probe.VerticalOffset.ShouldBe(expectedStep);
    }

    /// <summary>Verifies a key mapped to an axis the owner can still scroll overall is reported
    /// handled even when the offset is already at that axis's endpoint, matching the "handled
    /// whenever there is anything to scroll" policy a focus-owning composite relies on so the
    /// keystroke cannot escape to page an enclosing scrollable ancestor instead.</summary>
    [Fact]
    public void HandleScrollKey_WhenAtEndpointAndConsumeAtBoundary_ReturnsTrueWithoutMoving()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));
        probe.VerticalOffset = probe.ScrollableHost.MaximumVerticalOffset;
        var before = probe.VerticalOffset;

        // Act
        var handled = probe.RaiseScrollKey(Code.Down);

        // Assert
        handled.ShouldBeTrue();
        probe.VerticalOffset.ShouldBe(before);
    }

    /// <summary>Verifies a key the host maps to an axis this component cannot scroll at all -
    /// Left/Right against a vertical-only host - is left unhandled instead of being consumed for
    /// no effect or forwarded into an override that rejects a non-zero horizontal delta.</summary>
    [Fact]
    public void HandleScrollKey_WhenAxisCannotScroll_ReturnsFalse()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));

        // Act
        var handled = probe.RaiseScrollKey(Code.Left);

        // Assert
        handled.ShouldBeFalse();
        probe.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies a wheel-down record scrolls the owner by one line increment.</summary>
    [Fact]
    public void HandleScrollWheel_WhenWheelDown_ScrollsByLineSize()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));
        probe.LineSize = 2;

        // Act
        var handled = probe.RaiseScrollWheel(wheelX: 0, wheelY: -1);

        // Assert
        handled.ShouldBeTrue();
        probe.VerticalOffset.ShouldBe(2);
    }

    /// <summary>Verifies the hook observes VerticalOffset already refreshed to the newly committed
    /// host value, not the value before the host's transition.</summary>
    [Fact]
    public void OnScrollHostScrollChanged_WhenHostScrolls_ObservesRefreshedVerticalOffset()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));

        // Act
        _ = probe.ScrollBy(0, 3);

        // Assert
        probe.ScrollEventOrder.ShouldContain("hook:3");
    }

    /// <summary>Verifies the hook runs strictly before the same transition is forwarded through the
    /// owner's own public ScrollChanged, so an override can synchronously dispose or hide the owner
    /// without racing the bridge's own cached-property refresh.</summary>
    [Fact]
    public void OnScrollHostScrollChanged_WhenHostScrolls_RunsBeforeForwardedScrollChanged()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));
        probe.ScrollChanged += (_, eventArgs) => probe.ScrollEventOrder.Add($"forwarded:{eventArgs.Offset.Y}");

        // Act
        _ = probe.ScrollBy(0, 3);

        // Assert
        probe.ScrollEventOrder.ShouldBe(["hook:3", "forwarded:3"]);
    }

    /// <summary>Verifies the installed projection surface owns no style slot that would otherwise
    /// ever invalidate it, yet still repaints whenever the owner's own resolved style commits -
    /// whether that commit came from a local <c>Style</c> assignment or an inherited Theme swap.</summary>
    [Fact]
    public void ProjectionSurface_WhenOwnerStyleChanges_IsInvalidatedForRender()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProjectionProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));
        probe.Surface.Clear(Invalidation.All);

        // Act
        probe.Style = new SemanticColorStyle(SemanticColor.ControlText);

        // Assert
        probe.Surface.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a Theme swap that changes only the owner's own resolved semantic color -
    /// something the projection surface's own generic default style never references - still
    /// invalidates the surface for Render, proving the surface composes the owner's real theme
    /// impact into its own rather than reporting only what its own style would produce alone.</summary>
    [Fact]
    public void ProjectionSurface_WhenThemeSwapped_ReceivesOwnerThemeImpact()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProjectionProbe();
        new LayoutEngine().Layout(probe, new Size(10, 4));
        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create(accent: "#112233")));
        probe.Surface.Clear(Invalidation.All);

        // Act
        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create(accent: "#aabbcc")));

        // Assert
        probe.Surface.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a live resize that settles the composed projection at a new viewport width
    /// reprojects and republishes exactly one settled ScrollChanged carrying the final reconciled
    /// geometry, exercising the base's own MeasureOverride/ArrangeOverride and ScrollChanged
    /// routing once a width-dependent projection is installed.</summary>
    [Fact]
    public void InitializeWidthDependentProjection_WhenViewportWidthChanges_ReprojectsAndRaisesOneSettledScrollChanged()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProjectionProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var engine = new LayoutEngine();
        engine.Layout(probe, new Size(20, 5));
        _ = probe.ScrollBy(0, int.MaxValue);
        var reprojectionsBeforeResize = probe.ReprojectCount;
        List<ScrollChangedEventArgs> observed = [];
        probe.ScrollChanged += (_, eventArgs) => observed.Add(eventArgs);

        // Act - widening changes the settled viewport width, which reflows the probe's own
        // width-dependent row count and can strand a bottom-scrolled offset past the new extent.
        engine.Layout(probe, new Size(40, 5));

        // Assert
        probe.ReprojectCount.ShouldBeGreaterThan(reprojectionsBeforeResize);
        observed.Count.ShouldBe(1);
        observed[0].Extent.ShouldBe(probe.Extent);
        observed[0].Viewport.ShouldBe(probe.Viewport);
        observed[0].Offset.ShouldBe(new Point(probe.HorizontalOffset, probe.VerticalOffset));
    }

    /// <summary>Verifies installing width-dependent projection over a host that already forwards
    /// its own ScrollChanged directly is rejected, since the coordinator's own settled republication
    /// would otherwise double-publish alongside the host's direct forwarding.</summary>
    [Fact]
    public void InitializeWidthDependentProjection_WhenHostForwardsScrollEvent_Throws()
    {
        // Arrange
        var probe = new ScrollableCompositeControlProjectionProbe(
            forwardsScrollEvent: true,
            installWidthDependentProjection: false);

        // Act and assert
        var exception = Should.Throw<InvalidOperationException>(probe.InstallWidthDependentProjection);
        exception.Message.ShouldContain("forwardsScrollEvent");
    }
}
