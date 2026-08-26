// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using System.Text.Json;

using ReflectionBindingFlags = System.Reflection.BindingFlags;

/// <summary>Verifies JsonView parsing, validation, selection, and disclosure behavior.</summary>
public sealed class JsonViewTests
{
    /// <summary>Verifies malformed replacement text cannot partially replace the current document or selection.</summary>
    [Fact]
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

    /// <summary>Verifies a top-level duplicate object key cannot partially replace the current document or
    /// selection, since it would make node paths ambiguous.</summary>
    [Fact]
    public void Json_WhenTopLevelObjectHasDuplicateKey_PreservesPreviousState()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"name\":\"Alex\"}" };
        var previousJson = view.Json;
        var previousPath = view.SelectedPath;

        // Act
        Action action = () => view.Json = /*lang=json,strict*/ "{\"a\":1,\"a\":2}";

        // Assert
        _ = action.ShouldThrow<JsonException>();
        view.Json.ShouldBe(previousJson);
        view.SelectedPath.ShouldBe(previousPath);
    }

    /// <summary>Verifies a duplicate object key nested below the root is also rejected, confirming the
    /// uniqueness check applies recursively through every object encountered by BuildNode.</summary>
    [Fact]
    public void Json_WhenNestedObjectHasDuplicateKey_PreservesPreviousState()
    {
        // Arrange
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"name\":\"Alex\"}" };
        var previousJson = view.Json;
        var previousPath = view.SelectedPath;

        // Act
        Action action = () => view.Json = /*lang=json,strict*/ "{\"outer\":{\"a\":1,\"a\":2}}";

        // Assert
        _ = action.ShouldThrow<JsonException>();
        view.Json.ShouldBe(previousJson);
        view.SelectedPath.ShouldBe(previousPath);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes flip EffectiveIsEnabled and
    /// the derived focus eligibility it drives, and re-enabling restores both.</summary>
    [Fact]
    public void Enabled_WhenToggledDirectlyOrByAncestor_UpdatesEffectiveEnabledAndFocusEligibility()
    {
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"name\":\"Alex\"}", IsEnabled = false };

        view.EffectiveIsEnabled.ShouldBeFalse();
        view.CanFocus.ShouldBeFalse();

        view.IsEnabled = true;

        view.EffectiveIsEnabled.ShouldBeTrue();
        view.CanFocus.ShouldBeTrue();

        var ancestor = new Overlay { Children = { view }, IsEnabled = false };

        view.IsEnabled.ShouldBeTrue();
        view.EffectiveIsEnabled.ShouldBeFalse();
        view.CanFocus.ShouldBeFalse();

        ancestor.IsEnabled = true;

        view.EffectiveIsEnabled.ShouldBeTrue();
        view.CanFocus.ShouldBeTrue();
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

    /// <summary>Verifies the private FindOwningNode adapter's forward scan skips a line that
    /// owns no node (the array's own opening bracket) and returns the first eligible node
    /// reached while walking toward the end of the document.</summary>
    [Fact]
    public void FindOwningNode_WhenScanningForward_ReturnsFirstNodeReached()
    {
        // Arrange - lines are: 0 "[", 1 "/0", 2 "/1", 3 "/2", 4 "]"; only line 0 owns no node.
        var view = new JsonView { Json = "[0,1,2]" };
        var findOwningNode = typeof(JsonView).GetMethod(
            "FindOwningNode",
            ReflectionBindingFlags.Instance | ReflectionBindingFlags.NonPublic).ShouldNotBeNull();

        // Act
        var node = (JsonViewNode?) findOwningNode.Invoke(view, [0, 1]);

        // Assert
        node.ShouldNotBeNull().Path.ShouldBe("/0");
    }

    /// <summary>Verifies the private FindOwningNode adapter's backward scan skips a line that
    /// owns no node (the array's own closing bracket) and returns the nearest eligible node
    /// reached while walking toward the start of the document.</summary>
    [Fact]
    public void FindOwningNode_WhenScanningBackward_ReturnsNearestNodeReached()
    {
        // Arrange - lines are: 0 "[", 1 "/0", 2 "/1", 3 "/2", 4 "]"; only line 4 owns no node.
        var view = new JsonView { Json = "[0,1,2]" };
        var findOwningNode = typeof(JsonView).GetMethod(
            "FindOwningNode",
            ReflectionBindingFlags.Instance | ReflectionBindingFlags.NonPublic).ShouldNotBeNull();

        // Act
        var node = (JsonViewNode?) findOwningNode.Invoke(view, [4, -1]);

        // Assert
        node.ShouldNotBeNull().Path.ShouldBe("/2");
    }

    /// <summary>Verifies the private FindOwningNode adapter returns null, rather than throwing
    /// or looping past the collection, when no eligible node lies ahead in the scan
    /// direction.</summary>
    [Fact]
    public void FindOwningNode_WhenNoNodeLiesInScanDirection_ReturnsNull()
    {
        // Arrange - starting from the closing bracket and scanning forward runs straight off
        // the end of the document; every node lies behind it.
        var view = new JsonView { Json = "[0,1,2]" };
        var findOwningNode = typeof(JsonView).GetMethod(
            "FindOwningNode",
            ReflectionBindingFlags.Instance | ReflectionBindingFlags.NonPublic).ShouldNotBeNull();

        // Act
        var node = (JsonViewNode?) findOwningNode.Invoke(view, [4, 1]);

        // Assert
        node.ShouldBeNull();
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

    /// <summary>Verifies Indent round-trips a caller-assigned non-negative value.</summary>
    [Fact]
    public void Indent_WhenAssigned_RoundTrips()
    {
        var view = new JsonView { Indent = 4 };

        view.Indent.ShouldBe(4);
    }

    /// <summary>Verifies cached source lines rebuild from the newest reentrant indentation.</summary>
    [Fact]
    public void Indent_WhenPropertyObserverCommitsNewerValue_RebuildsNewestProjection()
    {
        var json = /*lang=json,strict*/ "{\"outer\":{\"value\":1}}";
        var view = new JsonView { Json = json };
        var expected = new JsonView { Json = json, Indent = 1 };
        view.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(JsonView.Indent) && view.Indent == 4)
            {
                view.Indent = 1;
            }
        };

        view.Indent = 4;

        view.Indent.ShouldBe(1);
        view.MeasureProjectedContent().ShouldBe(expected.MeasureProjectedContent());
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

    /// <summary>Verifies a null path is rejected before any lookup or mutation.</summary>
    [Fact]
    public void SetExpanded_WhenPathIsNull_ThrowsArgumentNullException()
    {
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"child\":1}" };

        _ = Should.Throw<ArgumentNullException>(() => view.SetExpanded(null!, false));
    }

    /// <summary>Verifies a path identifying a scalar leaf, rather than an object or array, is
    /// rejected the same way the root pointer is.</summary>
    [Fact]
    public void SetExpanded_WhenPathIdentifiesLeaf_ThrowsArgumentException()
    {
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"child\":1}" };
        var visibleCount = view.VisibleEntryCount;

        _ = Should.Throw<ArgumentException>(() => view.SetExpanded("/child", false));

        view.VisibleEntryCount.ShouldBe(visibleCount);
    }

    /// <summary>Verifies requesting the disclosure state a container is already in is a no-op that
    /// returns false and leaves the projection unchanged.</summary>
    [Fact]
    public void SetExpanded_WhenAlreadyInRequestedState_ReturnsFalse()
    {
        var view = new JsonView { Json = /*lang=json,strict*/ "{\"author\":{\"name\":\"Alex\"}}" };
        var visibleCount = view.VisibleEntryCount;

        var changed = view.SetExpanded("/author", true);

        changed.ShouldBeFalse();
        view.VisibleEntryCount.ShouldBe(visibleCount);
    }

    /// <summary>Verifies ExpandAll discloses every nested object and array entry.</summary>
    [Fact]
    public void ExpandAll_WhenSomeContainersAreCollapsed_ExpandsEveryContainer()
    {
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"author\":{\"name\":\"Alex\"},\"tags\":[1,2]}"
        };
        var fullyExpandedCount = view.VisibleEntryCount;
        _ = view.SetExpanded("/author", false);
        _ = view.SetExpanded("/tags", false);
        view.VisibleEntryCount.ShouldBeLessThan(fullyExpandedCount);

        view.ExpandAll();

        view.VisibleEntryCount.ShouldBe(fullyExpandedCount);
    }

    /// <summary>Verifies CollapseAll hides every non-root object and array entry's descendants.</summary>
    [Fact]
    public void CollapseAll_WhenCalled_CollapsesEveryContainer()
    {
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"author\":{\"name\":\"Alex\"},\"tags\":[1,2]}"
        };
        var fullyExpandedCount = view.VisibleEntryCount;

        view.CollapseAll();

        view.VisibleEntryCount.ShouldBeLessThan(fullyExpandedCount);

        view.ExpandAll();

        view.VisibleEntryCount.ShouldBe(fullyExpandedCount);
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

    /// <summary>Verifies ScrollBars rejects unknown flags before mutating the composed viewport.</summary>
    [Fact]
    public void ScrollBars_WhenSetToUndefinedFlags_ThrowsArgumentOutOfRangeException()
    {
        var view = new JsonView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.ScrollBars = (ScrollBars) 99);

        view.ScrollBars.ShouldBe(ScrollBars.Both);
    }

    /// <summary>Verifies ShowScrollBars rejects an undefined value before mutating the composed viewport.</summary>
    [Fact]
    public void ShowScrollBars_WhenSetToUndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        var view = new JsonView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.ShowScrollBars = (ShowScrollBars) 99);

        view.ShowScrollBars.ShouldBe(ShowScrollBars.WhenNeeded);
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

    /// <summary>Verifies the replayed ScrollChanged always carries the final settled Extent and
    /// Viewport across many resize-while-scrolled shapes, not just the geometry captured by
    /// whichever internal arrange happened to be the last one that actually reclamped the offset.
    /// An earlier internal arrange can populate the pending event while a later one changes
    /// Extent/Viewport further without needing another reclamp; that later geometry must still
    /// win.</summary>
    [Fact]
    public void Layout_WhenResizedWhileScrolledAcrossManyShapes_AlwaysReplaysFinalGeometry()
    {
        // Arrange
        var properties = string.Join(
            ',',
            Enumerable.Range(0, 25).Select(index => $"\"key{index}\":\"{new string('a', 60)} value {index}\""));
        var random = new Random(20260808);

        for (var trial = 0; trial < 60; trial++)
        {
            var view = new JsonView
            {
                Json = $"{{{properties}}}",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var engine = new LayoutEngine();
            var from = new Size(random.Next(8, 30), random.Next(4, 20));
            var to = new Size(random.Next(8, 30), random.Next(4, 20));
            engine.Layout(view, from);
            _ = view.ScrollBy(int.MaxValue, int.MaxValue);
            List<ScrollChangedEventArgs> observed = [];
            view.ScrollChanged += (_, eventArgs) => observed.Add(eventArgs);

            // Act
            engine.Layout(view, to);

            // Assert
            foreach (var eventArgs in observed)
            {
                eventArgs.Extent.ShouldBe(view.Extent, $"trial {trial}: {from} -> {to}");
                eventArgs.Viewport.ShouldBe(view.Viewport, $"trial {trial}: {from} -> {to}");
            }
        }
    }

    /// <summary>Verifies a settled transaction never strands a pending coalesced event for a later,
    /// unrelated transaction to replay: after a resize-while-scrolled transaction has already fired
    /// and cleared its one coalesced event, a following no-op layout pass (same size, no further
    /// scrolling) raises nothing at all, rather than replaying stale state left behind.</summary>
    [Fact]
    public void Layout_WhenTransactionFollowsASettledOne_DoesNotReplayStaleState()
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
        engine.Layout(view, new Size(50, 10));
        List<ScrollChangedEventArgs> observed = [];
        view.ScrollChanged += (_, eventArgs) => observed.Add(eventArgs);

        // Act - a no-op re-layout at the identical settled size, with no further scrolling.
        engine.Layout(view, new Size(50, 10));

        // Assert
        observed.ShouldBeEmpty();
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

    /// <summary>Verifies a fresh view carries no local style, resolving to the code-owned default.</summary>
    [Fact]
    public void Style_WhenUnassigned_ResolvesToDefault()
    {
        var view = new JsonView();

        view.Style.ShouldBeNull();
        view.ActualStyle.KeyColor.ShouldBe(JsonViewStyle.Default.KeyColor);
    }

    /// <summary>Verifies a local Style overrides the code-owned key color, and clearing it returns
    /// ownership to the theme-resolved default.</summary>
    [Fact]
    public void Style_WhenAssigned_OverridesKeyColorAndClearingRestoresTheResolvedOne()
    {
        var view = new JsonView();
        var defaultKeyColor = view.ActualStyle.KeyColor;

        view.Style = JsonViewStyle.Default with { KeyColor = Color.Rgb(0x11, 0x22, 0x33) };

        _ = view.Style.ShouldNotBeNull();
        view.ActualStyle.KeyColor.ShouldBe((ControlColor) Color.Rgb(0x11, 0x22, 0x33));

        view.Style = null;

        view.Style.ShouldBeNull();
        view.ActualStyle.KeyColor.ShouldBe(defaultKeyColor);
    }

    /// <summary>Verifies the generated scrollbar style is unpinned by default, round-trips a local
    /// assignment, reaches the generated bar, and releases back to the theme when cleared.</summary>
    [Fact]
    public void ScrollBarStyle_WhenAssignedAndCleared_ResolvesAndReleases()
    {
        var view = new JsonView();

        view.ScrollBarStyle.ShouldBeNull();
        view.ActualScrollBarStyle.ShouldBe(ScrollBar.ResolveStyle(null, view.Theme));

        view.ScrollBarStyle = ScrollBarStyle.ThinBlock;

        view.ScrollBarStyle.ShouldBe(ScrollBarStyle.ThinBlock);
        view.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinBlock);

        view.ScrollBarStyle = null;

        view.ScrollBarStyle.ShouldBeNull();
        view.ActualScrollBarStyle.ShouldBe(ScrollBar.ResolveStyle(null, view.Theme));
    }
}
