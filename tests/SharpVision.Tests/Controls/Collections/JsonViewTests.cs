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
