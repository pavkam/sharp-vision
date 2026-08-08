// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using System.Text.Json;

/// <summary>Verifies JsonView parsing, validation, selection, and disclosure behavior.</summary>
public sealed class JsonViewTests
{
    /// <summary>Verifies malformed replacement text cannot partially replace the current document or selection.</summary>
    [Fact]
    [ComponentUnitEvidence(typeof(JsonView))]
    public void Json_WhenMalformed_PreservesPreviousState()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"name\":\"Alex\"}" };
        var previousJson = view.Json;
        var previousPath = view.SelectedPath;

        // Act
        Action action = () => view.Json = "{\"broken\":";

        // Assert
        _ = action.ShouldThrow<JsonException>();
        view.Json.ShouldBe(previousJson);
        view.SelectedPath.ShouldBe(previousPath);
    }

    /// <summary>Verifies the first object property becomes selected through an escaped JSON Pointer.</summary>
    [Fact]
    public void Json_WhenObjectAssigned_SelectsFirstEscapedPropertyPath()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"a/b~c\":1,\"next\":2}" };

        // Assert
        view.SelectedPath.ShouldBe("/a~1b~0c");
    }

    /// <summary>Verifies the first array entry is exposed through its JSON Pointer index.</summary>
    [Fact]
    public void Json_WhenArrayAssigned_SelectsFirstIndexPath()
    {
        // Arrange
        var view = new JsonView { Json = "[true,false]" };

        // Assert
        view.SelectedPath.ShouldBe("/0");
    }

    /// <summary>Verifies a scalar root has no synthetic selectable key.</summary>
    [Fact]
    public void Json_WhenScalarAssigned_HasNoSelection()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"value\":1}" };

        // Act
        view.Json = "42";

        // Assert
        view.SelectedPath.ShouldBeNull();
    }

    /// <summary>Verifies indentation rejects negative cells before changing the stored value.</summary>
    [Fact]
    public void Indent_WhenNegative_ThrowsBeforeMutation()
    {
        // Arrange
        var view = new JsonView();

        // Act
        Action action = () => view.Indent = -1;

        // Assert
        _ = action.ShouldThrow<ArgumentOutOfRangeException>();
        view.Indent.ShouldBe(2);
    }

    /// <summary>Verifies callers can collapse and restore one container by JSON Pointer.</summary>
    [Fact]
    public void SetExpanded_WhenContainerPathExists_ChangesVisibleEntryCount()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"author\":{\"name\":\"Alex\"},\"active\":true}" };
        var expandedCount = view.VisibleEntryCount;

        // Act
        var collapsed = view.SetExpanded("/author", false);

        // Assert
        collapsed.ShouldBeTrue();
        view.VisibleEntryCount.ShouldBe(expandedCount - 1);

        // Act
        var expanded = view.SetExpanded("/author", true);

        // Assert
        expanded.ShouldBeTrue();
        view.VisibleEntryCount.ShouldBe(expandedCount);
    }

    /// <summary>Verifies the root pointer cannot report a disclosure change that the entry projection ignores.</summary>
    [Fact]
    public void SetExpanded_WhenPathIdentifiesRoot_ThrowsBeforeMutation()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"child\":1}" };
        var visibleCount = view.VisibleEntryCount;

        // Act
        Action action = () => view.SetExpanded(string.Empty, false);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        view.VisibleEntryCount.ShouldBe(visibleCount);
    }

    /// <summary>Verifies selection changes publish old and new JSON Pointer values after replacement.</summary>
    [Fact]
    public void SelectionChanged_WhenDocumentReplacementChangesSelection_PublishesCommittedPaths()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"old\":1}" };
        JsonViewSelectionChangedEventArgs? observed = null;
        view.SelectionChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        view.Json = /*lang=json,strict*/ "{\"new\":2}";

        // Assert
        var eventArgs = observed.ShouldNotBeNull();
        eventArgs.PreviousPath.ShouldBe("/old");
        eventArgs.Path.ShouldBe("/new");
    }

    /// <summary>Verifies Json observers see the new selection and reset viewport as one committed replacement.</summary>
    [Fact]
    public void Json_WhenPropertyChangedIsRaised_ExposesCommittedSelectionAndOffsets()
    {
        // Arrange
        var properties = string.Join(',', Enumerable.Range(0, 20).Select(index => $"\"old{index}\":\"a long value\""));
        var view = new JsonView
        {
            Json = $"{{{properties}}}",
            ShowScrollBars = ShowScrollBars.Never
        };
        new LayoutEngine().Layout(view, new Size(12, 4));
        _ = view.ScrollBy(3, 2);
        string? observedPath = null;
        var observedHorizontalOffset = -1;
        var observedVerticalOffset = -1;
        view.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(JsonView.Json))
            {
                return;
            }

            observedPath = view.SelectedPath;
            observedHorizontalOffset = view.HorizontalOffset;
            observedVerticalOffset = view.VerticalOffset;
        };

        // Act
        view.Json = /*lang=json,strict*/ "{\"new\":1}";

        // Assert
        observedPath.ShouldBe("/new");
        observedHorizontalOffset.ShouldBe(0);
        observedVerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies changed overflow policies publish once through the JsonView public surface.</summary>
    [Fact]
    public void ScrollingPolicy_WhenChanged_PublishesPublicPropertyNotificationsOnce()
    {
        // Arrange
        var view = new JsonView();
        List<string?> changed = [];
        view.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        // Act
        view.ScrollBars = ScrollBars.Vertical;
        view.ScrollBars = ScrollBars.Vertical;
        view.ShowScrollBars = ShowScrollBars.Always;
        view.ShowScrollBars = ShowScrollBars.Always;

        // Assert
        changed.Count(name => name == nameof(JsonView.ScrollBars)).ShouldBe(1);
        changed.Count(name => name == nameof(JsonView.ShowScrollBars)).ShouldBe(1);
    }

    /// <summary>Verifies unchanged LineSize assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void LineSize_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var view = new JsonView();
        var notifications = 0;
        view.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(JsonView.LineSize))
            {
                notifications++;
            }
        };

        view.LineSize = 3;
        view.LineSize = 3;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies unchanged PageOverlap assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void PageOverlap_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var view = new JsonView();
        var notifications = 0;
        view.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(JsonView.PageOverlap))
            {
                notifications++;
            }
        };

        view.PageOverlap = 3;
        view.PageOverlap = 3;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies LineSize rejects a negative value.</summary>
    [Fact]
    public void LineSize_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var view = new JsonView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.LineSize = -1);
    }

    /// <summary>Verifies PageOverlap rejects a negative value.</summary>
    [Fact]
    public void PageOverlap_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var view = new JsonView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.PageOverlap = -1);
    }

    /// <summary>Verifies LineSize forwards to, and reads back from, the composed viewport.</summary>
    [Fact]
    public void LineSize_WhenSet_ForwardsToComposedViewport()
    {
        var view = new JsonView { LineSize = 3 };

        view.LineSize.ShouldBe(3);
    }

    /// <summary>Verifies PageOverlap forwards to, and reads back from, the composed viewport.</summary>
    [Fact]
    public void PageOverlap_WhenSet_ForwardsToComposedViewport()
    {
        var view = new JsonView { PageOverlap = 3 };

        view.PageOverlap.ShouldBe(3);
    }

    /// <summary>Verifies word-wrap happens during measure rather than render, so a single layout
    /// pass already reports the wrapped extent instead of leaving a stale unwrapped measurement
    /// for a follow-up pass to repair.</summary>
    [Fact]
    public void Layout_WhenStringExceedsViewport_WrapsWithinOneLayoutPass()
    {
        // Arrange
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"text\":\"alpha beta gamma\"}",
            ShowScrollBars = ShowScrollBars.Never
        };

        // Act
        new LayoutEngine().Layout(view, new Size(18, 6));

        // Assert
        view.Extent.Width.ShouldBeLessThanOrEqualTo(view.Viewport.Width);
    }

    /// <summary>Verifies a resize while scrolled raises exactly one ScrollChanged with the final
    /// settled Extent and Viewport, not one per internal arrange ReconcileProjectionWidth performs
    /// while resettling the wrapped projection to the new width - a subscriber must never observe
    /// an intermediate offset clamped against a since-superseded wrap.</summary>
    [Fact]
    public void Layout_WhenResizedWiderWhileScrolled_RaisesScrollChangedOnceWithFinalGeometry()
    {
        // Arrange
        var properties = string.Join(
            ',',
            Enumerable.Range(0, 25).Select(index => $"\"key{index}\":\"{new string('a', 60)} value {index}\""));
        var view = new JsonView
        {
            Json = $"{{{properties}}}",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var engine = new LayoutEngine();
        engine.Layout(view, new Size(14, 10));
        _ = view.ScrollBy(int.MaxValue, int.MaxValue);
        List<ScrollChangedEventArgs> observed = [];
        view.ScrollChanged += (_, eventArgs) => observed.Add(eventArgs);

        // Act - widening reflows every wrapped string onto fewer lines, shrinking Extent enough
        // that the offset scrolled to the prior bottom-right corner no longer fits.
        engine.Layout(view, new Size(50, 10));

        // Assert
        observed.Count.ShouldBe(1);
        observed[0].Extent.ShouldBe(view.Extent);
        observed[0].Viewport.ShouldBe(view.Viewport);
        observed[0].Offset.ShouldBe(new Point(view.HorizontalOffset, view.VerticalOffset));
    }

    /// <summary>Verifies both overflow axes and generated scrollbar policy are reachable from JsonView.</summary>
    [Fact]
    public void ScrollBy_WhenDocumentExceedsViewport_MovesBothOffsets()
    {
        // Arrange
        var properties = string.Join(',', Enumerable.Range(0, 20).Select(index => $"\"key{index}\":\"a long value\""));
        var view = new JsonView
        {
            Json = $"{{{properties}}}",
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never
        };
        new LayoutEngine().Layout(view, new Size(12, 4));

        // Act
        var moved = view.ScrollBy(3, 2);

        // Assert
        moved.ShouldBeTrue();
        view.HorizontalOffset.ShouldBe(3);
        view.VerticalOffset.ShouldBe(2);
        view.Extent.Width.ShouldBeGreaterThan(view.Viewport.Width);
        view.Extent.Height.ShouldBeGreaterThan(view.Viewport.Height);
    }
}
