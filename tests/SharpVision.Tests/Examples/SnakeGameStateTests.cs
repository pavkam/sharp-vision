// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Examples;

using Snake;

/// <summary>Verifies Snake's pure game-state logic stays bounded on every valid board size.</summary>
public sealed class SnakeGameStateTests
{
    /// <summary>Verifies a board narrower than the seeded body's minimum width is rejected.</summary>
    [Fact]
    public void Constructor_WhenWidthIsBelowMinimum_ThrowsArgumentOutOfRange() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new GameState(3, 10, 0));

    /// <summary>Verifies a non-positive height is rejected.</summary>
    [Fact]
    public void Constructor_WhenHeightIsBelowMinimum_ThrowsArgumentOutOfRange() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new GameState(10, 0, 0));

    /// <summary>
    /// Verifies a board whose seeded body already fills every playable cell
    /// (so zero cells remain for any apple) still constructs instead of
    /// spinning forever in EnsureApples. This is the reachable case at the
    /// documented Width>=4 minimum: a 4-wide, 1-tall board's body occupies the
    /// entire row.
    /// </summary>
    [Fact(Timeout = 5_000)]
    public void Constructor_WhenBoardCannotHoldAppleTarget_CompletesWithoutHanging()
    {
        var state = new GameState(4, 1, 0);

        state.Apples.Count.ShouldBe(0);
        state.Body.Count.ShouldBe(4);
    }

    /// <summary>Verifies EnsureApples never requests more apples than the board's free cells allow.</summary>
    [Fact(Timeout = 5_000)]
    public void EnsureApples_WhenNoEmptyCellRemains_StopsAtAvailableCapacity()
    {
        var state = new GameState(4, 2, 0);

        // The body occupies 4 of the 8 cells; the remaining 4 are free, so
        // apple placement must terminate at or below that count rather than
        // spinning toward an unreachable requested target.
        state.Apples.Count.ShouldBeLessThanOrEqualTo(4);
    }

    /// <summary>Verifies every seeded body segment lands on the board at the minimum width.</summary>
    [Fact]
    public void Reset_WhenCalledOnMinimumBoard_PlacesBodyInsideBounds()
    {
        var state = new GameState(4, 1, 0);

        foreach (var point in state.Body)
        {
            point.X.ShouldBeInRange(0, state.Width - 1);
            point.Y.ShouldBeInRange(0, state.Height - 1);
        }
    }

    /// <summary>Verifies every seeded body segment lands on the board at an ordinary production size.</summary>
    [Fact]
    public void Reset_WhenCalledOnOrdinaryBoard_PlacesBodyInsideBounds()
    {
        var state = new GameState(10, 6, 0);

        foreach (var point in state.Body)
        {
            point.X.ShouldBeInRange(0, state.Width - 1);
            point.Y.ShouldBeInRange(0, state.Height - 1);
        }
    }

    /// <summary>Verifies a normal-sized board reaches its full requested apple target.</summary>
    [Fact]
    public void EnsureApples_WhenBoardHasAmpleSpace_ReachesRequestedTarget()
    {
        var state = new GameState(40, 20, 0);

        state.Apples.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    /// <summary>Verifies re-seeding after death keeps every body segment inside bounds on the minimum board.</summary>
    [Fact]
    public void Die_WhenLivesRemain_ReSeedsInsideBounds()
    {
        // The board is exactly as wide as the seeded body, so an ordinary
        // forward tick immediately drives the head past the right wall.
        var state = new GameState(4, 1, 0);

        var result = state.Tick();

        result.ShouldBe(TickResult.Died);
        state.IsGameOver.ShouldBeFalse();

        foreach (var point in state.Body)
        {
            point.X.ShouldBeInRange(0, state.Width - 1);
            point.Y.ShouldBeInRange(0, state.Height - 1);
        }
    }
}
