// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies SplitPane's validated public state, transitions, and pane ownership.</summary>
public sealed class SplitPaneTests
{
    /// <summary>Verifies the divider starts horizontal, responsive, resizable, and sequentially focusable.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasTwoPaneDefaults()
    {
        // Arrange and act
        var pane = new SplitPane();

        // Assert
        pane.Orientation.ShouldBe(Orientation.Horizontal);
        pane.FirstPaneLength.ShouldBe(Length.Percent(50));
        pane.IsResizable.ShouldBeTrue();
        pane.SmallChange.ShouldBe(1);
        pane.LargeChange.ShouldBe(10);
        pane.IsFocusable.ShouldBeTrue();
        pane.IsTabStop.ShouldBeTrue();
        pane.TabNavigation.ShouldBe(TabNavigation.Continue);
        pane.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
    }

    /// <summary>Verifies fixed and responsive authored leading-pane lengths both commit.</summary>
    [Fact]
    public void FirstPaneLength_WhenCellsOrPercentIsAssigned_CommitsAuthoredLength()
    {
        // Arrange
        var pane = new SplitPane { FirstPaneLength = Length.Cells(12) };

        // Act and assert
        pane.FirstPaneLength.ShouldBe(Length.Cells(12));

        pane.FirstPaneLength = Length.Percent(35.5);
        pane.FirstPaneLength.ShouldBe(Length.Percent(35.5));
    }

    /// <summary>Verifies unsupported lengths, unknown orientation, and negative changes fail before mutation.</summary>
    [Fact]
    public void Properties_WhenAssignmentIsInvalid_PreservePreviousState()
    {
        // Arrange
        var pane = new SplitPane
        {
            Orientation = Orientation.Vertical,
            FirstPaneLength = Length.Cells(7),
            IsResizable = false,
            SmallChange = 2,
            LargeChange = 8
        };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => pane.FirstPaneLength = Length.Auto);
        _ = Should.Throw<ArgumentException>(() => pane.FirstPaneLength = Length.Star(1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => pane.Orientation = (Orientation) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => pane.SmallChange = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => pane.LargeChange = -1);

        pane.Orientation.ShouldBe(Orientation.Vertical);
        pane.FirstPaneLength.ShouldBe(Length.Cells(7));
        pane.IsResizable.ShouldBeFalse();
        pane.SmallChange.ShouldBe(2);
        pane.LargeChange.ShouldBe(8);
    }

    /// <summary>Verifies assigning the committed length again publishes no property or typed event.</summary>
    [Fact]
    public void FirstPaneLength_WhenEquivalentValueIsAssigned_IsSilent()
    {
        // Arrange
        var pane = new SplitPane { FirstPaneLength = Length.Cells(6) };
        var propertyChanges = 0;
        var splitChanges = 0;
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SplitPane.FirstPaneLength))
            {
                propertyChanges++;
            }
        };
        pane.SplitChanged += (_, _) => splitChanges++;

        // Act
        pane.FirstPaneLength = Length.Cells(6);

        // Assert
        propertyChanges.ShouldBe(0);
        splitChanges.ShouldBe(0);
    }

    /// <summary>Verifies a changed length is observable before its typed previous/current payload.</summary>
    [Fact]
    public void FirstPaneLength_WhenValueChanges_RaisesPostCommitSplitChanged()
    {
        // Arrange
        var pane = new SplitPane();
        SplitChangedEventArgs? observed = null;
        Length? liveLength = null;
        var publications = new List<string>();
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SplitPane.FirstPaneLength))
            {
                publications.Add("property");
            }
        };
        pane.SplitChanged += (_, eventArgs) =>
        {
            publications.Add("split");
            observed = eventArgs;
            liveLength = pane.FirstPaneLength;
        };

        // Act
        pane.FirstPaneLength = Length.Cells(9);

        // Assert
        var change = observed.ShouldNotBeNull();
        change.PreviousLength.ShouldBe(Length.Percent(50));
        change.Length.ShouldBe(Length.Cells(9));
        liveLength.ShouldBe(Length.Cells(9));
        publications.ShouldBe(["property", "split"]);
    }

    /// <summary>Verifies a newer reentrant length owns the final state and typed transition stream.</summary>
    [Fact]
    public void FirstPaneLength_WhenPropertyObserverCommitsNewerLength_SuppressesStaleTypedEvent()
    {
        // Arrange
        var pane = new SplitPane();
        var observations = new List<(Length EventLength, Length LiveLength)>();
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SplitPane.FirstPaneLength) &&
                pane.FirstPaneLength == Length.Cells(4))
            {
                pane.FirstPaneLength = Length.Percent(25);
            }
        };
        pane.SplitChanged += (_, eventArgs) =>
            observations.Add((eventArgs.Length, pane.FirstPaneLength));

        // Act
        pane.FirstPaneLength = Length.Cells(4);

        // Assert
        pane.FirstPaneLength.ShouldBe(Length.Percent(25));
        observations.ShouldBe([(Length.Percent(25), Length.Percent(25))]);
    }

    /// <summary>Verifies property and typed observers are both attempted and the earlier failure wins.</summary>
    [Fact]
    public void FirstPaneLength_WhenPropertyAndTypedObserversThrow_RethrowsPropertyFailureAfterTypedPublication()
    {
        // Arrange
        var pane = new SplitPane();
        var publications = new List<string>();
        var propertyFailure = new InvalidOperationException("property failed");
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SplitPane.FirstPaneLength))
            {
                publications.Add("property");
                throw propertyFailure;
            }
        };
        pane.SplitChanged += (_, _) =>
        {
            publications.Add("split");
            throw new NotSupportedException("split failed");
        };

        // Act
        var thrown = Should.Throw<InvalidOperationException>(
            () => pane.FirstPaneLength = Length.Cells(4));

        // Assert
        thrown.ShouldBeSameAs(propertyFailure);
        pane.FirstPaneLength.ShouldBe(Length.Cells(4));
        publications.ShouldBe(["property", "split"]);
    }

    /// <summary>Verifies disposal clears typed subscribers before owned-pane disposal can publish
    /// any child-driven divider reconciliation.</summary>
    [Fact]
    public void Dispose_WhenCalled_ClearsSplitSubscribersAndPaneVisibilityParticipation()
    {
        // Arrange
        var first = new ProbeControl();
        var pane = new SplitPane { Children = { first, new ProbeControl() } };
        new LayoutEngine().Layout(pane, new Size(11, 2));
        var splitChanges = 0;
        var tabStopChanges = 0;
        pane.SplitChanged += (_, _) => splitChanges++;
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.CanTabStop))
            {
                tabStopChanges++;
            }
        };

        // Act
        pane.Dispose();

        // Assert
        pane.IsDisposed.ShouldBeTrue();
        first.IsDisposed.ShouldBeTrue();
        splitChanges.ShouldBe(0);
        tabStopChanges.ShouldBe(1);
    }

    /// <summary>Verifies only a laid-out, resizable divider between two visible panes is a Tab stop.</summary>
    [Fact]
    public void CanTabStop_WhenDividerAvailabilityChanges_TracksOwnedInteractionState()
    {
        // Arrange
        var pane = new SplitPane();
        var first = new ProbeControl();
        var second = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(second);
        var engine = new LayoutEngine();

        // Act and assert: geometry
        pane.CanTabStop.ShouldBeFalse();
        engine.Layout(pane, new Size(11, 2));
        pane.CanTabStop.ShouldBeTrue();
        engine.Layout(pane, new Size(11, 0));
        pane.CanTabStop.ShouldBeFalse();
        engine.Layout(pane, new Size(11, 2));
        pane.CanTabStop.ShouldBeTrue();

        // Act and assert: resizability and pane visibility
        pane.IsResizable = false;
        pane.CanTabStop.ShouldBeFalse();
        pane.IsResizable = true;
        pane.CanTabStop.ShouldBeTrue();
        first.Visibility = Visibility.Hidden;
        pane.CanTabStop.ShouldBeFalse();
        first.Visibility = Visibility.Visible;
        pane.CanTabStop.ShouldBeTrue();
        second.Visibility = Visibility.Collapsed;
        pane.CanTabStop.ShouldBeFalse();
        second.Visibility = Visibility.Visible;
        pane.CanTabStop.ShouldBeTrue();

        // Act and assert: structure
        _ = pane.Children.Remove(second);
        pane.CanTabStop.ShouldBeFalse();
        pane.Children.Add(second);
        pane.CanTabStop.ShouldBeTrue();
    }

    /// <summary>Verifies SplitPane publishes one notification for each divider-owned eligibility transition.</summary>
    [Fact]
    public void CanTabStop_WhenOwnedInteractionTransitions_PublishesExactlyOncePerTransition()
    {
        // Arrange
        var pane = new SplitPane
        {
            Children = { new ProbeControl(), new ProbeControl() }
        };
        var publications = 0;
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.CanTabStop))
            {
                publications++;
            }
        };

        // Act and assert
        new LayoutEngine().Layout(pane, new Size(11, 2));
        publications.ShouldBe(1);

        pane.IsResizable = false;
        publications.ShouldBe(2);

        pane.IsResizable = false;
        publications.ShouldBe(2);

        pane.IsResizable = true;
        publications.ShouldBe(3);

        pane.Children[0].Visibility = Visibility.Hidden;
        publications.ShouldBe(4);

        pane.Children[0].Visibility = Visibility.Visible;
        publications.ShouldBe(5);
    }

    /// <summary>Verifies owner availability remains ControlBase notification territory.</summary>
    [Fact]
    public void CanTabStop_WhenOwnerAvailabilityChanges_PublishesOneInheritedNotification()
    {
        // Arrange
        var pane = new SplitPane
        {
            Children = { new ProbeControl(), new ProbeControl() }
        };
        new LayoutEngine().Layout(pane, new Size(11, 2));
        var publications = new List<string?>();
        pane.PropertyChanged += (_, eventArgs) => publications.Add(eventArgs.PropertyName);

        // Act
        pane.IsEnabled = false;

        // Assert
        pane.CanTabStop.ShouldBeFalse();
        publications.Count(name => name == nameof(ControlBase.CanTabStop)).ShouldBe(1);

        // Act
        publications.Clear();
        pane.IsEnabled = true;
        pane.Visibility = Visibility.Hidden;

        // Assert
        pane.CanTabStop.ShouldBeFalse();
        publications.Count(name => name == nameof(ControlBase.CanTabStop)).ShouldBe(2);
    }

    /// <summary>Verifies inherited focus and tab setters do not receive a duplicate SplitPane notification.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanTabStop_WhenInheritedEligibilitySetterChanges_PublishesOneNotification(bool tabStopSetter)
    {
        // Arrange
        var pane = new SplitPane
        {
            Children = { new ProbeControl(), new ProbeControl() }
        };
        new LayoutEngine().Layout(pane, new Size(11, 2));
        var publications = new List<string?>();
        pane.PropertyChanged += (_, eventArgs) => publications.Add(eventArgs.PropertyName);

        // Act
        if (tabStopSetter)
        {
            pane.IsTabStop = false;
        }
        else
        {
            pane.IsFocusable = false;
        }

        // Assert
        pane.CanTabStop.ShouldBeFalse();
        publications.Count(name => name == nameof(ControlBase.CanTabStop)).ShouldBe(1);
    }

    /// <summary>Verifies required divider reconciliation still runs after a throwing resizability observer.</summary>
    [Fact]
    public void IsResizable_WhenPropertyObserverThrows_StillRefreshesCanTabStop()
    {
        // Arrange
        var pane = new SplitPane
        {
            Children = { new ProbeControl(), new ProbeControl() }
        };
        new LayoutEngine().Layout(pane, new Size(11, 2));
        var failure = new InvalidOperationException("observer failed");
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SplitPane.IsResizable))
            {
                throw failure;
            }
        };

        // Act
        var thrown = Should.Throw<InvalidOperationException>(() => pane.IsResizable = false);

        // Assert
        thrown.ShouldBeSameAs(failure);
        pane.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies a newer reentrant resizability transition restores final divider eligibility
    /// after both committed dependent-state transitions publish.</summary>
    [Fact]
    public void IsResizable_WhenPropertyObserverCommitsNewerValue_RestoresFinalEligibility()
    {
        // Arrange
        var pane = new SplitPane
        {
            Children = { new ProbeControl(), new ProbeControl() }
        };
        new LayoutEngine().Layout(pane, new Size(11, 2));
        var tabStopChanges = 0;
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SplitPane.IsResizable) && !pane.IsResizable)
            {
                pane.IsResizable = true;
            }
            else if (eventArgs.PropertyName == nameof(ControlBase.CanTabStop))
            {
                tabStopChanges++;
            }
        };

        // Act
        pane.IsResizable = false;

        // Assert
        pane.IsResizable.ShouldBeTrue();
        pane.CanTabStop.ShouldBeTrue();
        tabStopChanges.ShouldBe(2);
    }

    /// <summary>Verifies transition payloads reject lengths that SplitPane cannot author.</summary>
    [Fact]
    public void SplitChangedEventArgs_WhenLengthIsUnsupported_ThrowsBeforeConstruction()
    {
        // Arrange
        void CreateWithInvalidPrevious() => _ = new SplitChangedEventArgs(Length.Auto, Length.Cells(1));
        void CreateWithInvalidCurrent() => _ = new SplitChangedEventArgs(Length.Cells(1), Length.Star(1));

        // Act and assert
        _ = Should.Throw<ArgumentException>(CreateWithInvalidPrevious);
        _ = Should.Throw<ArgumentException>(CreateWithInvalidCurrent);
    }

    /// <summary>Verifies the public pane collection owns exactly two children and rejects a third atomically.</summary>
    [Fact]
    public void Children_WhenThirdPaneIsAdded_RejectsItBeforeOwnershipMutation()
    {
        // Arrange
        var pane = new SplitPane();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var third = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        _ = Should.Throw<InvalidOperationException>(() => pane.Children.Add(third));

        // Assert
        pane.Children.Count.ShouldBe(2);
        pane.Children[0].ShouldBeSameAs(first);
        pane.Children[1].ShouldBeSameAs(second);
        first.Parent.ShouldBeSameAs(pane);
        second.Parent.ShouldBeSameAs(pane);
        third.Parent.ShouldBeNull();
    }

    /// <summary>Verifies a valid attached length mutation requires the owning dispatcher.</summary>
    [Fact]
    public async Task FirstPaneLength_WhenAttachedAndMutatedOffDispatcher_ThrowsBeforeMutationAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var pane = new SplitPane();
        await dispatcher.InvokeAsync(
            () => pane.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        _ = Should.Throw<InvalidOperationException>(() => pane.FirstPaneLength = Length.Cells(3));

        // Assert
        pane.FirstPaneLength.ShouldBe(Length.Percent(50));
        await dispatcher.InvokeAsync(
            pane.Dispose,
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a horizontal split allocates border boxes around one retained divider cell.</summary>
    [Fact]
    public void Layout_WhenHorizontalWithTwoPanes_ArrangesAroundDivider()
    {
        // Arrange
        var pane = new SplitPane();
        var first = new ProbeControl(new Size(1, 1));
        var second = new ProbeControl(new Size(1, 1));
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(11, 3));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 5, 3));
        second.Bounds.ShouldBe(new Rect(6, 0, 5, 3));
        pane.LogicalDividerBounds.ShouldBe(new Rect(5, 0, 1, 3));
        pane.VisibleDividerBounds.ShouldBe(pane.LogicalDividerBounds);
    }

    /// <summary>Verifies vertical orientation maps the same two-track allocation onto height.</summary>
    [Fact]
    public void Layout_WhenVerticalWithTwoPanes_ArrangesFirstAboveSecond()
    {
        // Arrange
        var pane = new SplitPane { Orientation = Orientation.Vertical };
        var first = new ProbeControl(new Size(1, 1));
        var second = new ProbeControl(new Size(1, 1));
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(3, 11));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 3, 5));
        second.Bounds.ShouldBe(new Rect(0, 6, 3, 5));
        pane.LogicalDividerBounds.ShouldBe(new Rect(0, 5, 3, 1));
    }

    /// <summary>Verifies cells size a border box while pane margins move the divider outside it.</summary>
    [Fact]
    public void Layout_WhenCellLengthAndMarginsAreAuthored_KeepsMarginsOutsideBorderBoxes()
    {
        // Arrange
        var pane = new SplitPane { FirstPaneLength = Length.Cells(5) };
        var first = new ProbeControl { Margin = new Thickness(1, 0, 1, 0) };
        var second = new ProbeControl { Margin = new Thickness(1, 0, 0, 0) };
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(11, 2));

        // Assert
        first.Bounds.ShouldBe(new Rect(1, 0, 5, 2));
        pane.LogicalDividerBounds.ShouldBe(new Rect(7, 0, 1, 2));
        second.Bounds.ShouldBe(new Rect(9, 0, 2, 2));
    }

    /// <summary>Verifies percentage and cell requests respond differently to a larger finite slot.</summary>
    [Theory]
    [InlineData(true, 50d, 5, 10)]
    [InlineData(false, 4d, 4, 4)]
    public void Layout_WhenWidthChanges_OnlyPercentageLengthTracksThePool(
        bool percentage,
        double value,
        int expectedAtEleven,
        int expectedAtTwentyOne)
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = percentage
                ? Length.Percent(value)
                : Length.Cells((int) value)
        };
        var first = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(new ProbeControl());
        var engine = new LayoutEngine();

        // Act and assert
        engine.Layout(pane, new Size(11, 1));
        first.Bounds.Width.ShouldBe(expectedAtEleven);

        engine.Layout(pane, new Size(21, 1));
        first.Bounds.Width.ShouldBe(expectedAtTwentyOne);
    }

    /// <summary>Verifies each pane's limits constrain the shared pool and the retained feasible range.</summary>
    [Fact]
    public void Layout_WhenBothPanesHaveLimits_UsesTheirJointFeasibleAllocation()
    {
        // Arrange
        var pane = new SplitPane { FirstPaneLength = Length.Percent(80) };
        var first = new ProbeControl { MinWidth = Length.Cells(3), MaxWidth = Length.Cells(7) };
        var second = new ProbeControl { MinWidth = Length.Cells(4), MaxWidth = Length.Cells(6) };
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(11, 1));

        // Assert
        first.Bounds.Width.ShouldBe(6);
        second.Bounds.Width.ShouldBe(4);
        pane.MinimumFirstPaneExtent.ShouldBe(4);
        pane.MaximumFirstPaneExtent.ShouldBe(6);
    }

    /// <summary>Verifies percentage limits use the divider-excluded pool as their containing axis.</summary>
    [Fact]
    public void Layout_WhenPaneMaximumIsPercentage_UsesDividerExcludedPoolAsLimitBase()
    {
        // Arrange
        var pane = new SplitPane { FirstPaneLength = Length.Percent(100) };
        var first = new ProbeControl { MaxWidth = Length.Percent(50) };
        var second = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(11, 1));

        // Assert
        first.Bounds.Width.ShouldBe(5);
        second.Bounds.Width.ShouldBe(5);
    }

    /// <summary>Verifies Hidden remains a participant while Collapsed removes its track and divider.</summary>
    [Fact]
    public void Layout_WhenVisibilityChanges_HiddenParticipatesAndCollapsedDoesNot()
    {
        // Arrange
        var pane = new SplitPane();
        var first = new ProbeControl { Visibility = Visibility.Hidden };
        var second = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(second);
        var engine = new LayoutEngine();

        // Act and assert
        engine.Layout(pane, new Size(11, 2));
        first.Bounds.Width.ShouldBe(5);
        second.Bounds.ShouldBe(new Rect(6, 0, 5, 2));

        first.Visibility = Visibility.Collapsed;
        engine.Layout(pane, new Size(11, 2));
        first.Bounds.ShouldBe(default);
        second.Bounds.ShouldBe(new Rect(0, 0, 11, 2));
        pane.LogicalDividerBounds.ShouldBe(default);
    }

    /// <summary>Verifies an unbounded split uses both intrinsic requests and one divider cell.</summary>
    [Fact]
    public void Measure_WhenPrimaryAxisIsUnbounded_UsesIntrinsicPaneRequests()
    {
        // Arrange
        var pane = new SplitPane();
        pane.Children.Add(new ProbeControl(new Size(3, 2)));
        pane.Children.Add(new ProbeControl(new Size(4, 1)));

        // Act
        pane.Measure(new Constraint(width: null, height: null));

        // Assert
        pane.DesiredSize.ShouldBe(new Size(8, 2));
    }

    /// <summary>Verifies a single participant gets the complete outer slot and no divider.</summary>
    [Fact]
    public void Layout_WhenOnlyOnePaneParticipates_FillsContentWithoutDivider()
    {
        // Arrange
        var pane = new SplitPane();
        var only = new ProbeControl(new Size(3, 2)) { Margin = new Thickness(1) };
        pane.Children.Add(only);

        // Act
        new LayoutEngine().Layout(pane, new Size(8, 4));

        // Assert
        only.Bounds.ShouldBe(new Rect(1, 1, 6, 2));
        pane.LogicalDividerBounds.ShouldBe(default);
    }

    /// <summary>Verifies an empty split contributes no intrinsic extent.</summary>
    [Fact]
    public void Measure_WhenNoPaneParticipates_ReturnsZeroDesiredSize()
    {
        // Arrange
        var pane = new SplitPane();

        // Act
        pane.Measure(new Constraint(width: null, height: null));

        // Assert
        pane.DesiredSize.ShouldBe(default);
    }

    /// <summary>Verifies finite margin oversubscription produces contained zero-width border boxes.</summary>
    [Fact]
    public void Layout_WhenMarginsConsumeFinitePool_TruncatesThemWithoutEscapingBounds()
    {
        // Arrange
        var pane = new SplitPane { FirstPaneLength = Length.Cells(2) };
        var first = new ProbeControl { Margin = new Thickness(4, 0, 0, 0) };
        var second = new ProbeControl { Margin = new Thickness(4, 0, 0, 0) };
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(4, 1));

        // Assert
        first.Bounds.ShouldBe(new Rect(3, 0, 0, 1));
        pane.LogicalDividerBounds.ShouldBe(new Rect(3, 0, 1, 1));
        second.Bounds.ShouldBe(new Rect(4, 0, 0, 1));
    }

    /// <summary>Verifies a one-cell primary box preserves the divider and contains zero-width panes.</summary>
    [Fact]
    public void Layout_WhenPrimaryBoxIsOneCell_DividerConsumesTheOnlyCell()
    {
        // Arrange
        var pane = new SplitPane();
        var first = new ProbeControl();
        var second = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(1, 2));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 0, 2));
        pane.LogicalDividerBounds.ShouldBe(new Rect(0, 0, 1, 2));
        second.Bounds.ShouldBe(new Rect(1, 0, 0, 2));
    }

    /// <summary>Verifies zero primary bounds produce neither divider geometry nor escaping slots.</summary>
    [Fact]
    public void Layout_WhenPrimaryBoxIsEmpty_ProducesEmptyContainedGeometry()
    {
        // Arrange
        var pane = new SplitPane();
        var first = new ProbeControl();
        var second = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(0, 2));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 0, 2));
        second.Bounds.ShouldBe(new Rect(0, 0, 0, 2));
        pane.LogicalDividerBounds.ShouldBe(default);
    }

    /// <summary>Verifies a zero cross axis suppresses the logical divider despite finite track allocation.</summary>
    [Fact]
    public void Layout_WhenCrossBoxIsEmpty_ProducesNoDividerGeometry()
    {
        // Arrange
        var pane = new SplitPane();
        var first = new ProbeControl();
        var second = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(11, 0));

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 5, 0));
        second.Bounds.ShouldBe(new Rect(6, 0, 5, 0));
        pane.LogicalDividerBounds.ShouldBe(default);
    }

    /// <summary>Verifies intrinsic owner sizing performs the same unbounded two-track allocation.</summary>
    [Fact]
    public void Measure_WhenOwnerAutoSizes_UsesNaturalMarginInclusiveSplitExtent()
    {
        // Arrange
        var pane = new SplitPane { AutoSize = true };
        pane.Children.Add(new ProbeControl(new Size(3, 2)) { Margin = new Thickness(1, 0) });
        pane.Children.Add(new ProbeControl(new Size(4, 1)));

        // Act
        pane.Measure(new Constraint(20, 10));

        // Assert
        pane.DesiredSize.ShouldBe(new Size(10, 2));
    }

    /// <summary>Verifies owner border and padding keep tracks inside the content box.</summary>
    [Fact]
    public void Layout_WhenOwnerHasChrome_ArrangesDividerAndPanesInsideContentBox()
    {
        // Arrange
        var pane = new SplitPane
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1)
        };
        var first = new ProbeControl();
        var second = new ProbeControl();
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(11, 5));

        // Assert
        first.Bounds.ShouldBe(new Rect(2, 2, 3, 1));
        pane.LogicalDividerBounds.ShouldBe(new Rect(5, 2, 1, 1));
        second.Bounds.ShouldBe(new Rect(6, 2, 3, 1));
    }

    /// <summary>Verifies primary scrolling keeps percentage sizing viewport-based while trailing Star stays intrinsic.</summary>
    [Fact]
    public void Layout_WhenPrimaryAxisScrolls_UsesViewportPoolAndRetainsTrailingIntrinsicExtent()
    {
        // Arrange
        var pane = new SplitPane
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            FirstPaneLength = Length.Percent(100)
        };
        var first = new ProbeControl(new Size(1, 1));
        var second = new ProbeControl(new Size(2, 1));
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(6, 2));

        // Assert
        pane.Viewport.ShouldBe(new Size(6, 2));
        pane.Extent.ShouldBe(new Size(8, 1));
        first.Bounds.ShouldBe(new Rect(0, 0, 5, 2));
        second.Bounds.ShouldBe(new Rect(6, 0, 2, 2));
    }

    /// <summary>Verifies a zero-width scrolling viewport still discovers intrinsic trailing extent safely.</summary>
    [Fact]
    public void Layout_WhenPrimaryScrollViewportIsEmpty_UsesZeroPercentageBase()
    {
        // Arrange
        var pane = new SplitPane
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            FirstPaneLength = Length.Percent(100)
        };
        var first = new ProbeControl();
        var second = new ProbeControl(new Size(2, 1));
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(0, 2));

        // Assert
        pane.Viewport.Width.ShouldBe(0);
        pane.Extent.Width.ShouldBe(3);
        first.Bounds.Width.ShouldBe(0);
        second.Bounds.ShouldBe(new Rect(1, 0, 2, 2));
        pane.VisibleDividerBounds.Width.ShouldBe(0);
    }

    /// <summary>Verifies an always-visible opposite rail narrows the percentage viewport base before extent discovery.</summary>
    [Fact]
    public void Layout_WhenOppositeRailIsAlwaysVisible_UsesReservedViewportForPrimaryPercentage()
    {
        // Arrange
        var pane = new SplitPane
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            FirstPaneLength = Length.Percent(100)
        };
        var first = new ProbeControl(new Size(1, 1));
        var second = new ProbeControl(new Size(1, 1));
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(6, 2));

        // Assert
        pane.Viewport.Width.ShouldBe(5);
        pane.Extent.Width.ShouldBe(6);
        first.Bounds.Width.ShouldBe(4);
        second.Bounds.ShouldBe(new Rect(5, 0, 1, 2));
    }

    /// <summary>Verifies an automatically induced opposite rail reruns allocation at its narrower viewport.</summary>
    [Fact]
    public void Layout_WhenOppositeRailIsInduced_UsesSettledViewportForPrimaryPercentage()
    {
        // Arrange
        var pane = new SplitPane
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
            FirstPaneLength = Length.Percent(100)
        };
        var first = new ProbeControl(new Size(1, 3));
        var second = new ProbeControl(new Size(1, 3));
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(6, 2));

        // Assert
        pane.Viewport.Width.ShouldBe(5);
        pane.Extent.ShouldBe(new Size(6, 3));
        first.Bounds.Width.ShouldBe(4);
        second.Bounds.X.ShouldBe(5);
    }

    /// <summary>Verifies cross-axis scrolling does not turn the finite split axis into an unbounded allocation.</summary>
    [Fact]
    public void Layout_WhenOnlyCrossAxisScrolls_PreservesFinitePrimaryAllocation()
    {
        // Arrange
        var pane = new SplitPane
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never
        };
        var first = new ProbeControl(new Size(1, 3));
        var second = new ProbeControl(new Size(1, 3));
        pane.Children.Add(first);
        pane.Children.Add(second);

        // Act
        new LayoutEngine().Layout(pane, new Size(11, 1));

        // Assert
        pane.Extent.ShouldBe(new Size(11, 3));
        first.Bounds.ShouldBe(new Rect(0, 0, 5, 3));
        second.Bounds.ShouldBe(new Rect(6, 0, 5, 3));
    }

    /// <summary>Verifies final arrangement remeasures a scrolling pane whose text height depends on its width.</summary>
    [Fact]
    public void Arrange_WhenFinalHorizontalSlotNarrows_RemeasuresScrollablePaneExtent()
    {
        // Arrange
        var pane = new SplitPane();
        var scrollable = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never
        };
        var text = new ControlText("abcdefghij") { Overflow = Overflow.WrapAnywhere };
        scrollable.Children.Add(text);
        pane.Children.Add(scrollable);
        pane.Children.Add(new ProbeControl());
        pane.Measure(new Constraint(11, 4));
        text.DesiredSize.ShouldBe(new Size(5, 2));

        // Act
        pane.Arrange(new Rect(0, 0, 7, 4), widthResolved: true, heightResolved: true);

        // Assert
        scrollable.Bounds.ShouldBe(new Rect(0, 0, 3, 4));
        scrollable.Viewport.ShouldBe(new Size(3, 4));
        scrollable.Extent.ShouldBe(new Size(3, 4));
        text.DesiredSize.ShouldBe(new Size(3, 4));
        text.Bounds.ShouldBe(new Rect(0, 0, 3, 4));
    }

    /// <summary>Verifies a direct width-dependent pane refreshes desired geometry for its final slot.</summary>
    [Fact]
    public void Arrange_WhenFinalHorizontalSlotNarrows_RemeasuresWrappingText()
    {
        // Arrange
        var pane = new SplitPane();
        var text = new ControlText("abcdefghij") { Overflow = Overflow.WrapAnywhere };
        pane.Children.Add(text);
        pane.Children.Add(new ProbeControl());
        pane.Measure(new Constraint(11, 4));
        text.DesiredSize.ShouldBe(new Size(5, 2));

        // Act
        pane.Arrange(new Rect(0, 0, 7, 4), widthResolved: true, heightResolved: true);

        // Assert
        text.DesiredSize.ShouldBe(new Size(3, 4));
        text.Bounds.ShouldBe(new Rect(0, 0, 3, 4));
    }

    /// <summary>Verifies vertical splits remeasure a Wrap whose cross extent depends on final height.</summary>
    [Fact]
    public void Arrange_WhenFinalVerticalSlotNarrows_RemeasuresVerticalWrap()
    {
        // Arrange
        var pane = new SplitPane { Orientation = Orientation.Vertical };
        var wrap = new Wrap { Orientation = Orientation.Vertical };

        for (var index = 0; index < 4; index++)
        {
            wrap.Children.Add(new ProbeControl(new Size(1, 1)));
        }

        pane.Children.Add(wrap);
        pane.Children.Add(new ProbeControl());
        pane.Measure(new Constraint(4, 11));
        wrap.DesiredSize.ShouldBe(new Size(1, 4));

        // Act
        pane.Arrange(new Rect(0, 0, 4, 7), widthResolved: true, heightResolved: true);

        // Assert
        wrap.DesiredSize.ShouldBe(new Size(2, 3));
        wrap.Bounds.ShouldBe(new Rect(0, 0, 4, 3));
    }

    /// <summary>Verifies a cross-scrolling split commits reflowed height before its viewport becomes scrollable.</summary>
    [Fact]
    public void Arrange_WhenCrossScrollingSplitNarrows_CommitsReflowedExtentBeforeScrolling()
    {
        // Arrange
        var pane = new SplitPane
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never
        };
        var text = new ControlText("abcdefghij") { Overflow = Overflow.WrapAnywhere };
        pane.Children.Add(text);
        pane.Children.Add(new ProbeControl());
        pane.Measure(new Constraint(11, 2));
        text.DesiredSize.ShouldBe(new Size(5, 2));

        // Act
        pane.Arrange(new Rect(0, 0, 7, 2), widthResolved: true, heightResolved: true);

        // Assert
        pane.Viewport.ShouldBe(new Size(7, 2));
        pane.Extent.ShouldBe(new Size(7, 4));
        text.DesiredSize.ShouldBe(new Size(3, 4));
        pane.ScrollBy(0, 1).ShouldBeTrue();
        pane.VerticalOffset.ShouldBe(1);
    }

    /// <summary>Verifies primary scrolling refreshes a trailing intrinsic track after cross-axis contraction.</summary>
    [Fact]
    public void Arrange_WhenPrimaryScrollingSplitContractsCrossAxis_RefreshesTrailingStarAndExtent()
    {
        // Arrange
        var pane = new SplitPane
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            FirstPaneLength = Length.Percent(100)
        };
        var first = new ProbeControl();
        var trailing = new Wrap { Orientation = Orientation.Vertical };

        for (var index = 0; index < 4; index++)
        {
            trailing.Children.Add(new ProbeControl(new Size(1, 1)));
        }

        pane.Children.Add(first);
        pane.Children.Add(trailing);
        pane.Measure(new Constraint(6, 4));
        trailing.DesiredSize.ShouldBe(new Size(1, 4));

        // Act
        pane.Arrange(new Rect(0, 0, 6, 2), widthResolved: true, heightResolved: true);

        // Assert
        pane.Viewport.ShouldBe(new Size(6, 2));
        pane.Extent.ShouldBe(new Size(8, 2));
        first.Bounds.ShouldBe(new Rect(0, 0, 5, 2));
        trailing.DesiredSize.ShouldBe(new Size(2, 2));
        trailing.Bounds.ShouldBe(new Rect(6, 0, 2, 2));
        pane.ScrollBy(1, 0).ShouldBeTrue();
    }
}
