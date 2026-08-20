// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies duplicate-free chart data point collection construction and mutation.</summary>
public sealed class ChartDataPointCollectionTests
{
    /// <summary>Verifies the default constructor starts with no points.</summary>
    [Fact]
    public void Constructor_WhenCreatedWithoutPoints_IsEmpty()
    {
        // Arrange and act
        var collection = new ChartDataPointCollection();

        // Assert
        collection.ShouldBeEmpty();
    }

    /// <summary>Verifies a null source enumerable fails before creating any points.</summary>
    [Fact]
    public void Constructor_WhenPointsIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => new ChartDataPointCollection(null!));

    /// <summary>Verifies a source enumerable containing a repeated reference fails.</summary>
    [Fact]
    public void Constructor_WhenPointsContainDuplicateReference_Throws()
    {
        // Arrange
        var point = new ChartDataPoint("CPU", 1);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => new ChartDataPointCollection([point, point]));
    }

    /// <summary>Verifies unique points are copied in enumeration order.</summary>
    [Fact]
    public void Constructor_WhenPointsAreUnique_CopiesInEnumerationOrder()
    {
        // Arrange
        var first = new ChartDataPoint("A", 1);
        var second = new ChartDataPoint("B", 2);

        // Act
        var collection = new ChartDataPointCollection([first, second]);

        // Assert
        collection.ShouldBe([first, second]);
    }

    /// <summary>Verifies Add rejects a null point before mutating the collection.</summary>
    [Fact]
    public void Add_WhenItemIsNull_ThrowsBeforeMutation()
    {
        // Arrange
        var collection = new ChartDataPointCollection();

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => collection.Add(null!));
        collection.ShouldBeEmpty();
    }

    /// <summary>Verifies the indexer rejects a duplicate reference assigned to a different
    /// position before mutating the collection.</summary>
    [Fact]
    public void Indexer_WhenAssignedDuplicateReference_ThrowsBeforeMutation()
    {
        // Arrange
        var first = new ChartDataPoint("A", 1);
        var second = new ChartDataPoint("B", 2);
        var collection = new ChartDataPointCollection([first, second]);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => collection[0] = second);
        collection.ShouldBe([first, second]);
    }

    /// <summary>Verifies the indexer rejects a null replacement before mutating the collection.</summary>
    [Fact]
    public void Indexer_WhenAssignedNull_ThrowsBeforeMutation()
    {
        // Arrange
        var point = new ChartDataPoint("A", 1);
        var collection = new ChartDataPointCollection([point]);

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => collection[0] = null!);
        collection.ShouldBe([point]);
    }

    /// <summary>Verifies re-assigning a position its own current reference is a no-op that
    /// publishes no collection-changed notification.</summary>
    [Fact]
    public void Indexer_WhenAssignedSameReferenceAtSamePosition_PublishesNoNotification()
    {
        // Arrange
        var point = new ChartDataPoint("A", 1);
        var collection = new ChartDataPointCollection([point]);
        var raised = 0;
        collection.CollectionChanged += (_, _) => raised++;

        // Act
        collection[0] = point;

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies the indexer replaces a position with a genuinely different, unique point.</summary>
    [Fact]
    public void Indexer_WhenAssignedDifferentUniqueReference_ReplacesPosition()
    {
        // Arrange
        var first = new ChartDataPoint("A", 1);
        var second = new ChartDataPoint("B", 2);
        var replacement = new ChartDataPoint("C", 3);
        var collection = new ChartDataPointCollection([first, second])
        {
            // Act
            [0] = replacement
        };

        // Assert
        collection.ShouldBe([replacement, second]);
    }
}
