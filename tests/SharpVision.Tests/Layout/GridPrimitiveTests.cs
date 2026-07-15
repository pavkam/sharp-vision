// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;



/// <summary>Verifies immutable Grid definitions, collections, placement, and validation.</summary>
public sealed class GridPrimitiveTests
{
    /// <summary>Verifies factories preserve exact length and limit values.</summary>
    [Fact]
    public void Factory_WhenValuesAreValid_CreatesExactTrack()
    {
        Track.Auto(minimum: 1, maximum: 8).ShouldBe(new Track(Length.Auto, 1, 8));
        Track.Cells(3).Length.ShouldBe(Length.Cells(3));
        Track.Percent(25).Length.ShouldBe(Length.Percent(25));
        Track.Star(2).Length.ShouldBe(Length.Star(2));
    }

    /// <summary>Verifies invalid limits fail before constructing a usable definition.</summary>
    [Fact]
    public void Constructor_WhenLimitsAreInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Track(Length.Auto, -1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Track(Length.Auto, 0, -1));
        _ = Should.Throw<ArgumentException>(() => new Track(Length.Auto, 3, 2));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Cells(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Percent(101));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Star(0));
    }

    /// <summary>Verifies row and column collections are permanent and invalidate real mutations.</summary>
    [Fact]
    public void Definitions_WhenCollectionMutates_InvalidatesMeasureOncePerChange()
    {
        var grid = new Grid();

        _ = grid.Rows.ShouldNotBeNull();
        _ = grid.Columns.ShouldNotBeNull();
        grid.Rows.ShouldBeEmpty();
        grid.Columns.ShouldBeEmpty();
        grid.Clear(Invalidation.All);
        grid.Rows.Add(Track.Auto());
        grid.Pending.ShouldBe(Invalidation.All);
        grid.Clear(Invalidation.All);
        grid.Rows[0] = Track.Auto();
        grid.Pending.ShouldBe(Invalidation.None);
        grid.Rows.Remove(Track.Cells(4)).ShouldBeFalse();
        grid.Pending.ShouldBe(Invalidation.None);
        grid.Rows.Clear();
        grid.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies Grid and placement defaults are deterministic.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasDocumentedGridDefaults()
    {
        var grid = new Grid();
        var child = new ProbeControl();

        grid.RowSpacing.ShouldBe(0);
        grid.ColumnSpacing.ShouldBe(0);
        Grid.GetRow(child).ShouldBe(0);
        Grid.GetColumn(child).ShouldBe(0);
        Grid.GetRowSpan(child).ShouldBe(1);
        Grid.GetColumnSpan(child).ShouldBe(1);
    }

    /// <summary>Verifies origins, spans, and spacing reject invalid values before mutation.</summary>
    [Fact]
    public void Setter_WhenValueIsInvalid_ThrowsBeforeMutation()
    {
        var grid = new Grid();
        var child = new ProbeControl();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => grid.RowSpacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => grid.ColumnSpacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetRow(child, -1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetColumn(child, -1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetRowSpan(child, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetColumnSpan(child, 0));

        grid.RowSpacing.ShouldBe(0);
        grid.ColumnSpacing.ShouldBe(0);
        Grid.GetRow(child).ShouldBe(0);
        Grid.GetColumn(child).ShouldBe(0);
        Grid.GetRowSpan(child).ShouldBe(1);
        Grid.GetColumnSpan(child).ShouldBe(1);
    }

    /// <summary>Verifies owned placement must fit explicit or implicit definitions.</summary>
    [Fact]
    public void SetPlacement_WhenChildIsOwned_ValidatesCommittedTrackRange()
    {
        var grid = new Grid();
        var child = new ProbeControl();
        grid.Children.Add(child);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetRow(child, 1));
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Auto());
        Grid.SetRow(child, 1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Grid.SetRowSpan(child, 2));

        Grid.GetRow(child).ShouldBe(1);
        Grid.GetRowSpan(child).ShouldBe(1);
    }

    /// <summary>Verifies attached placement mutation invalidates measure and requires affinity.</summary>
    [Fact]
    public async Task SetRow_WhenChildIsAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var grid = new Grid();
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Auto());
        var child = new ProbeControl();
        grid.Children.Add(child);
        grid.Clear(Invalidation.All);
        Grid.SetRow(child, 1);
        grid.Pending.ShouldBe(Invalidation.All);
        await dispatcher.InvokeAsync(
            () => grid.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => Grid.SetRow(child, 0));

        Grid.GetRow(child).ShouldBe(1);
    }

    /// <summary>Verifies off-thread definition mutation fails before changing the collection.</summary>
    [Fact]
    public async Task Add_WhenGridIsAttachedOffThread_ThrowsBeforeCollectionMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var grid = new Grid();
        await dispatcher.InvokeAsync(() =>
        {
            grid.Attach(dispatcher);
            grid.Clear(Invalidation.All);
        }, TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => grid.Rows.Add(Track.Auto()));

        grid.Rows.ShouldBeEmpty();
        grid.Pending.ShouldBe(Invalidation.None);
    }
}
