// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies the reveal that follows a keyboard-driven current-row commit survives a
/// selection subscriber that restructures the tree synchronously. Collapsing the row's ancestor or
/// removing the row rebuilds the realized flat list before the commit continues, so the row named
/// at entry is no longer a descendant of the items container and a naive reveal would throw an
/// ArgumentException out of the routed key handler.</summary>
public sealed class TreeViewSelectionCallbackRevealTests
{
    /// <summary>Verifies a SelectionChanged handler collapsing the newly current row's parent
    /// completes the keystroke without an exception escaping the routed key handler.</summary>
    [Fact]
    public async Task SelectionChanged_WhenHandlerCollapsesParentOfCurrentRow_CompletesAsync()
    {
        // Arrange
        var a = new TreeViewItem("A");
        var a1 = new TreeViewItem("a1");
        a.Children.Add(a1);
        a.Children.Add(new TreeViewItem("a2"));
        var tree = new TreeView
        {
            SelectionMode = TreeSelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Items = { a, new TreeViewItem("B") }
        };
        tree.SelectionChanged += (_, _) =>
        {
            if (ReferenceEquals(tree.SelectedItem, a1))
            {
                a.IsExpanded = false;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var faults = new List<string>();
        surface.Application.UnhandledException += (_, eventArgs) =>
        {
            faults.Add(eventArgs.Exception.GetType().Name + ": " + eventArgs.Exception.Message);
            eventArgs.IsHandled = true;
        };
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(a);

        // Act - Down moves current to a1; the handler collapses A mid-commit
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        a.IsExpanded.ShouldBeFalse();
        tree.SelectedItem.ShouldBeSameAs(a1);
        tree.IsDisposed.ShouldBeFalse();
        faults.ShouldBeEmpty();
    }

    /// <summary>Verifies a SelectionChanged handler removing the newly current row from its parent
    /// completes the keystroke without an exception escaping the routed key handler.</summary>
    [Fact]
    public async Task SelectionChanged_WhenHandlerRemovesCurrentRow_CompletesAsync()
    {
        // Arrange
        var a = new TreeViewItem("A");
        var a1 = new TreeViewItem("a1");
        a.Children.Add(a1);
        a.Children.Add(new TreeViewItem("a2"));
        var tree = new TreeView
        {
            SelectionMode = TreeSelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Items = { a, new TreeViewItem("B") }
        };
        tree.SelectionChanged += (_, _) =>
        {
            if (ReferenceEquals(tree.SelectedItem, a1))
            {
                _ = a.Children.Remove(a1);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var faults = new List<string>();
        surface.Application.UnhandledException += (_, eventArgs) =>
        {
            faults.Add(eventArgs.Exception.GetType().Name + ": " + eventArgs.Exception.Message);
            eventArgs.IsHandled = true;
        };
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(a);

        // Act - Down moves current to a1; the handler removes a1 mid-commit
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        a.Children.ShouldNotContain(a1);
        tree.IsDisposed.ShouldBeFalse();
        faults.ShouldBeEmpty();
    }

    /// <summary>Verifies a SelectionChanged handler that moves the selection to a row hidden
    /// under a collapsed ancestor completes the keystroke: the repaired current row is not
    /// realized, so nothing is revealed and no exception escapes the routed key handler.</summary>
    [Fact]
    public async Task SelectionChanged_WhenHandlerSelectsRowUnderCollapsedAncestor_CompletesAsync()
    {
        // Arrange
        var a = new TreeViewItem("A");
        var b = new TreeViewItem("B");
        var b1 = new TreeViewItem("b1");
        b.Children.Add(b1);
        b.IsExpanded = false;
        var tree = new TreeView
        {
            SelectionMode = TreeSelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Items = { a, b }
        };
        var redirected = false;
        tree.SelectionChanged += (_, _) =>
        {
            if (!redirected && ReferenceEquals(tree.SelectedItem, a))
            {
                redirected = true;
                tree.SelectItem(b1);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var faults = new List<string>();
        surface.Application.UnhandledException += (_, eventArgs) =>
        {
            faults.Add(eventArgs.Exception.GetType().Name + ": " + eventArgs.Exception.Message);
            eventArgs.IsHandled = true;
        };
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act - Down moves current to A; the handler redirects selection to the hidden b1
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        tree.SelectedItem.ShouldBeSameAs(b1);
        b.IsExpanded.ShouldBeFalse();
        faults.ShouldBeEmpty();
    }
}
