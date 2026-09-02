// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies TrackCollection mutation semantics through a Grid owner: indexer reads and
/// writes, insertion, removal by value and index, enumeration, copying, no-op suppression,
/// placement validation before any mutation, and disposed-owner rejection.</summary>
public sealed class TrackCollectionConditionTests
{
    private static Grid CreateGrid(params Track[] columns)
    {
        var grid = new Grid();

        foreach (var column in columns)
        {
            grid.Columns.Add(column);
        }

        grid.Clear(Invalidation.All);
        return grid;
    }

    /// <summary>Verifies the indexer reads tracks by position, assigning a different track
    /// invalidates measure, and out-of-range reads and writes throw without invalidating.</summary>
    [Fact]
    public void Indexer_WhenReadOrWritten_ReportsTracksAndValidatesRange()
    {
        // Arrange
        var grid = CreateGrid(Track.Auto(), Track.Cells(4));

        // Assert reads
        grid.Columns[1].ShouldBe(Track.Cells(4));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => grid.Columns[2]);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => grid.Columns[-1] = Track.Auto());
        grid.Pending.ShouldBe(Invalidation.None);

        // Act write a different track
        grid.Columns[1] = Track.Star(2);

        // Assert
        grid.Columns[1].ShouldBe(Track.Star(2));
        grid.Pending.HasFlag(Invalidation.Measure).ShouldBeTrue();
    }

    /// <summary>Verifies Contains, IndexOf, and IsReadOnly reflect the stored tracks by value.</summary>
    [Fact]
    public void Queries_WhenTracksAreStored_ReportMembershipByValue()
    {
        // Arrange
        var grid = CreateGrid(Track.Cells(2), Track.Percent(50), Track.Cells(2));

        // Assert
        grid.Columns.IsReadOnly.ShouldBeFalse();
        grid.Columns.Contains(Track.Percent(50)).ShouldBeTrue();
        grid.Columns.Contains(Track.Percent(51)).ShouldBeFalse();
        grid.Columns.IndexOf(Track.Cells(2)).ShouldBe(0);
        grid.Columns.IndexOf(Track.Auto()).ShouldBe(-1);
        grid.Columns.Count.ShouldBe(3);
    }

    /// <summary>Verifies Insert places the track at the index, shifts the rest, and invalidates,
    /// while an out-of-range index throws before mutating.</summary>
    [Fact]
    public void Insert_WhenIndexIsValidOrOutOfRange_ShiftsOrThrowsBeforeMutation()
    {
        // Arrange
        var grid = CreateGrid(Track.Auto(), Track.Cells(4));

        // Act
        grid.Columns.Insert(1, Track.Star(1));

        // Assert
        grid.Columns.ShouldBe([Track.Auto(), Track.Star(1), Track.Cells(4)]);
        grid.Pending.HasFlag(Invalidation.Measure).ShouldBeTrue();

        // Act out of range
        grid.Clear(Invalidation.All);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => grid.Columns.Insert(4, Track.Auto()));

        // Assert
        grid.Columns.Count.ShouldBe(3);
        grid.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies Remove drops the first matching track and invalidates, reports false for
    /// a missing track without invalidating, and RemoveAt validates its index before mutating.</summary>
    [Fact]
    public void Remove_WhenTrackIsPresentOrMissing_RemovesFirstMatchOrLeavesCollectionUntouched()
    {
        // Arrange
        var grid = CreateGrid(Track.Cells(2), Track.Auto(), Track.Cells(2));

        // Act remove by value
        grid.Columns.Remove(Track.Cells(2)).ShouldBeTrue();

        // Assert the first occurrence went
        grid.Columns.ShouldBe([Track.Auto(), Track.Cells(2)]);
        grid.Pending.HasFlag(Invalidation.Measure).ShouldBeTrue();

        // Act remove missing
        grid.Clear(Invalidation.All);
        grid.Columns.Remove(Track.Percent(10)).ShouldBeFalse();
        grid.Pending.ShouldBe(Invalidation.None);

        // Act RemoveAt out of range then valid
        _ = Should.Throw<ArgumentOutOfRangeException>(() => grid.Columns.RemoveAt(2));
        grid.Columns.Count.ShouldBe(2);
        grid.Pending.ShouldBe(Invalidation.None);
        grid.Columns.RemoveAt(0);

        // Assert
        grid.Columns.ShouldBe([Track.Cells(2)]);
        grid.Pending.HasFlag(Invalidation.Measure).ShouldBeTrue();
    }

    /// <summary>Verifies both enumerators yield the stored order and CopyTo validates its array.</summary>
    [Fact]
    public void Enumeration_WhenAccessedThroughEitherInterface_YieldsStoredOrder()
    {
        // Arrange
        var grid = CreateGrid(Track.Auto(), Track.Star(1));
        System.Collections.IEnumerable tracks = grid.Rows;
        grid.Rows.Add(Track.Cells(1));
        grid.Rows.Add(Track.Percent(25));

        // Act
        var generic = grid.Rows.ToArray();
        var nonGeneric = tracks.Cast<object>().ToArray();
        var target = new Track[3];
        grid.Rows.CopyTo(target, 1);

        // Assert
        generic.ShouldBe([Track.Cells(1), Track.Percent(25)]);
        nonGeneric.ShouldBe([Track.Cells(1), Track.Percent(25)]);
        target.ShouldBe([default, Track.Cells(1), Track.Percent(25)]);
        _ = Should.Throw<ArgumentNullException>(() => grid.Rows.CopyTo(null!, 0));
    }

    /// <summary>Verifies clearing an already-empty collection neither invalidates nor validates
    /// placement, while clearing a populated one invalidates.</summary>
    [Fact]
    public void Clear_WhenEmptyOrPopulated_SuppressesOrPublishesInvalidation()
    {
        // Arrange
        var grid = new Grid();
        var child = new ProbeControl(new Size(1, 1));
        Grid.SetColumn(child, 3);
        grid.Children.Add(child);
        grid.Clear(Invalidation.All);

        // Act clear the empty definitions (the child sits in an implicit track, so no validation runs)
        grid.Columns.Clear();

        // Assert
        grid.Pending.ShouldBe(Invalidation.None);

        // Act populate and clear
        Grid.SetColumn(child, 0);
        grid.Columns.Add(Track.Auto());
        grid.Clear(Invalidation.All);
        grid.Columns.Clear();

        // Assert
        grid.Columns.ShouldBeEmpty();
        grid.Pending.HasFlag(Invalidation.Measure).ShouldBeTrue();
    }

    /// <summary>Verifies every shrinking mutation is rejected before it happens when a child's
    /// committed placement or span would no longer fit the definitions.</summary>
    [Fact]
    public void Mutation_WhenAChildWouldFallOutsideTheNewDefinitions_ThrowsBeforeMutation()
    {
        // Arrange
        var grid = CreateGrid(Track.Auto(), Track.Auto(), Track.Auto());
        var placed = new ProbeControl(new Size(1, 1));
        Grid.SetColumn(placed, 1);
        Grid.SetColumnSpan(placed, 2);
        grid.Children.Add(placed);
        grid.Clear(Invalidation.All);

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => grid.Columns.RemoveAt(2));
        _ = Should.Throw<InvalidOperationException>(() => grid.Columns.Remove(Track.Auto()));
        _ = Should.Throw<InvalidOperationException>(grid.Columns.Clear);
        grid.Columns.Count.ShouldBe(3);
        grid.Pending.ShouldBe(Invalidation.None);

        // Act replacing or growing keeps the count valid
        grid.Columns[2] = Track.Cells(5);
        grid.Columns.Insert(0, Track.Cells(1));

        // Assert
        grid.Columns.Count.ShouldBe(4);
        grid.Columns[3].ShouldBe(Track.Cells(5));
    }

    /// <summary>Verifies a disposed owner rejects definition mutation with ObjectDisposedException
    /// while reads still work.</summary>
    [Fact]
    public void Mutation_WhenOwnerIsDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var grid = CreateGrid(Track.Auto());
        grid.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => grid.Columns.Add(Track.Auto()));
        _ = Should.Throw<ObjectDisposedException>(() => grid.Columns[0] = Track.Cells(1));
        _ = Should.Throw<ObjectDisposedException>(() => grid.Columns.RemoveAt(0));
        _ = Should.Throw<ObjectDisposedException>(grid.Columns.Clear);
        grid.Columns.Count.ShouldBe(1);
        grid.Columns[0].ShouldBe(Track.Auto());
    }
}
