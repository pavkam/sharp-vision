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
}
