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
}
