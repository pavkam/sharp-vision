// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies TabControl typed ownership, selection, repair, events, layout, and validation.</summary>
public sealed class TabControlTests
{
    /// <summary>Verifies the first eligible page auto-selects and every semantic item enters the private host.</summary>
    [Fact]
    public void Items_WhenPagesAreAdded_AutoSelectsFirstEligibleOwnedPage()
    {
        var disabled = Create("Disabled", "No");
        disabled.IsEnabled = false;
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = new TabControl();

        tabs.Items.Add(disabled);
        tabs.Items.Add(first);
        tabs.Items.Add(second);

        tabs.Items.ShouldBe([disabled, first, second]);
        tabs.SelectedIndex.ShouldBe(1);
        tabs.SelectedItem.ShouldBeSameAs(first);
        disabled.IsSelected.ShouldBeFalse();
        first.IsSelected.ShouldBeTrue();
        second.IsSelected.ShouldBeFalse();
        first.Parent.ShouldNotBeNull().Parent.ShouldBeSameAs(tabs);
    }

    /// <summary>Verifies changed selection publishes once after page and retained-header state commit.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_PublishesCommittedIdentityOnce()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        var observations = new List<string>();
        tabs.SelectionChanged += (_, _) => observations.Add(
            $"{tabs.SelectedIndex}:{first.IsSelected}:{second.IsSelected}");

        tabs.SelectedIndex = 1;
        tabs.SelectedIndex = 1;
        tabs.SelectedIndex = -1;

        observations.ShouldBe(["1:False:True", "-1:False:False"]);
        tabs.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies insertion preserves identity and selected removal chooses successor then predecessor.</summary>
    [Fact]
    public void Items_WhenMutated_RepairsSelectionByStableIdentityAndNearestEligibility()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var successor = Create("Successor", "Three");
        var inserted = Create("Inserted", "Zero");
        var tabs = Create(first, selected, successor);
        tabs.SelectedIndex = 1;

        tabs.Items.Insert(0, inserted);

        tabs.SelectedIndex.ShouldBe(2);
        tabs.SelectedItem.ShouldBeSameAs(selected);

        tabs.Items.Remove(selected).ShouldBeTrue();

        selected.Parent.ShouldBeNull();
        selected.IsDisposed.ShouldBeFalse();
        tabs.SelectedIndex.ShouldBe(2);
        tabs.SelectedItem.ShouldBeSameAs(successor);

        tabs.Items.Remove(successor).ShouldBeTrue();

        tabs.SelectedIndex.ShouldBe(1);
        tabs.SelectedItem.ShouldBeSameAs(first);
    }

    /// <summary>Verifies disabling or collapsing the selected page chooses the nearest eligible page.</summary>
    [Fact]
    public void Availability_WhenSelectedPageBecomesUnavailable_RepairsSelection()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, second, third);
        tabs.SelectedIndex = 1;

        second.IsEnabled = false;

        tabs.SelectedItem.ShouldBeSameAs(third);

        third.Visibility = Visibility.Collapsed;

        tabs.SelectedItem.ShouldBeSameAs(first);
        first.IsSelected.ShouldBeTrue();
    }

    /// <summary>Verifies replacement transfers ownership and Clear detaches every page while clearing selection.</summary>
    [Fact]
    public void Items_WhenSelectedPageIsReplacedOrCleared_TransfersIdentityAndDetachesWithoutDisposal()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var replacement = Create("Replacement", "New");
        var tabs = Create(first, selected);
        tabs.SelectedIndex = 1;

        tabs.Items[1] = replacement;

        selected.Parent.ShouldBeNull();
        selected.IsDisposed.ShouldBeFalse();
        tabs.SelectedItem.ShouldBeSameAs(replacement);
        replacement.IsSelected.ShouldBeTrue();

        tabs.Items.Clear();

        tabs.Items.ShouldBeEmpty();
        tabs.SelectedIndex.ShouldBe(-1);
        first.Parent.ShouldBeNull();
        replacement.Parent.ShouldBeNull();
        first.IsDisposed.ShouldBeFalse();
        replacement.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies invalid selected indexes and unavailable targets preserve the committed page.</summary>
    [Fact]
    public void SelectedIndex_WhenTargetIsInvalid_PreservesSelectionBeforeThrowing()
    {
        var first = Create("First", "One");
        var disabled = Create("Disabled", "Two");
        disabled.IsEnabled = false;
        var tabs = Create(first, disabled);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tabs.SelectedIndex = -2);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => tabs.SelectedIndex = 2);
        _ = Should.Throw<InvalidOperationException>(() => tabs.SelectedIndex = 1);

        tabs.SelectedItem.ShouldBeSameAs(first);
        first.IsSelected.ShouldBeTrue();
    }

    /// <summary>Verifies collection validation rejects invalid candidates before ownership or selection changes.</summary>
    [Fact]
    public void Items_WhenCandidateIsInvalid_PreservesCollectionOwnershipAndSelection()
    {
        var first = Create("First", "One");
        var tabs = Create(first);
        var attached = Create("Attached", "Elsewhere");
        var host = new Stack { Children = { attached } };
        var disposed = Create("Disposed", "Gone");
        disposed.Dispose();

        _ = Should.Throw<ArgumentNullException>(() => tabs.Items.Add(null!));
        _ = Should.Throw<ArgumentException>(() => tabs.Items.Add(first));
        _ = Should.Throw<ArgumentException>(() => tabs.Items.Add(attached));
        _ = Should.Throw<ObjectDisposedException>(() => tabs.Items.Add(disposed));

        tabs.Items.ShouldBe([first]);
        tabs.SelectedItem.ShouldBeSameAs(first);
        _ = first.Parent.ShouldNotBeNull();
        attached.Parent.ShouldBeSameAs(host);
    }

    /// <summary>Verifies only selected content is arranged below retained headers and separator rows.</summary>
    [Fact]
    public void Layout_WhenSelectionChanges_ExcludesOldContentAndArrangesNewContent()
    {
        var first = Create("General", "General body");
        var second = Create("界", "Wide body");
        var tabs = Create(first, second);
        var engine = new Engine();

        engine.Layout(tabs, new Size(20, 5));

        first.HeaderPart.Bounds.ShouldBe(new Rect(0, 0, 9, 1));
        second.HeaderPart.Bounds.ShouldBe(new Rect(10, 0, 4, 1));
        first.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 2, 20, 3));
        second.Content.ShouldNotBeNull().Bounds.ShouldBe(default);

        tabs.SelectedIndex = 1;
        engine.Layout(tabs, new Size(20, 5));

        first.Content.ShouldNotBeNull().Bounds.ShouldBe(default);
        second.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 2, 20, 3));
    }

    private static TabControl Create(params TabItem[] items)
    {
        var result = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        foreach (var item in items)
        {
            result.Items.Add(item);
        }

        return result;
    }

    private static TabItem Create(string header, string content) => new()
    {
        Header = header,
        Content = new ControlText(content),
    };
}
