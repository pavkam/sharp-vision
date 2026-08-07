// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Examples;

/// <summary>Proves the Snake example composes its game glyphs with the board field.</summary>
public sealed class SnakeSurfaceTests
{
    /// <summary>Verifies an apple preserves the background already painted for the soil cell.</summary>
    [Fact]
    public async Task Render_WhenAppleIsDrawn_PreservesSoilBackgroundAsync()
    {
        // Arrange
        var state = new Snake.GameState(width: 40, height: 20, difficulty: 0);
        var board = new Snake.SnakeBoard
        {
            State = state,
            ShowBoard = true
        };
        var occupied = state.Body.Concat(state.Obstacles).Concat(state.Apples.Select(static apple => apple.Position));
        var blank = Enumerable.Range(0, state.Height)
            .SelectMany(y => Enumerable.Range(0, state.Width).Select(x => new Point(x, y)))
            .First(point => !occupied.Contains(point));
        var apple = state.Apples[0].Position;

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            board,
            new Size(state.Width + 2, state.Height + 2),
            TestContext.Current.CancellationToken);

        // Assert
        var soilBackground = surface.Cell(new Point(blank.X + 1, blank.Y + 1)).Style.Background;
        var appleBackground = surface.Cell(new Point(apple.X + 1, apple.Y + 1)).Style.Background;
        appleBackground.ShouldBe(soilBackground);
    }
}
