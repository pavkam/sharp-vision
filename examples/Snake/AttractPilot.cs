// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Chooses directions for the self-playing attract-mode snake on the title screen.</summary>
/// <remarks>
/// The pilot is a pure, deterministic policy over the public <see cref="GameState"/> surface: it
/// greedily approaches the nearest non-poison apple while rejecting moves that hit a wall, an
/// obstacle, or the body, and it penalizes cells whose every onward exit is already blocked. The
/// demo snake still dies eventually — the owner simply restarts it, which reads as an arcade loop.
/// </remarks>
public static class AttractPilot
{
    private static readonly Direction[] _directions =
    [
        Direction.Up,
        Direction.Down,
        Direction.Left,
        Direction.Right
    ];

    /// <summary>Chooses the next direction for the given live game.</summary>
    /// <param name="state">The non-null game whose snake the pilot steers.</param>
    /// <returns>
    /// The safest apple-seeking direction, or the current heading when every candidate is fatal.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    public static Direction ChooseDirection(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var head = state.Head;
        var best = state.Heading;
        var bestScore = int.MaxValue;

        foreach (var candidate in _directions)
        {
            if (IsOpposite(state.Heading, candidate))
            {
                continue;
            }

            var next = Step(head, candidate);

            if (IsBlocked(state, next))
            {
                continue;
            }

            // A candidate whose onward exits are all blocked is a one-step trap. It stays legal
            // (the pilot may be forced into it) but ranks behind every open alternative.
            var trapped = CountExits(state, next, head) == 0;
            var score = (trapped ? 1_000 : 0) + DistanceToNearestApple(state, next);

            if (candidate == state.Heading)
            {
                score--;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static bool IsBlocked(GameState state, Point cell)
    {
        if (cell.X < 0 || cell.X >= state.Width || cell.Y < 0 || cell.Y >= state.Height)
        {
            return true;
        }

        if (state.Obstacles.Contains(cell))
        {
            return true;
        }

        foreach (var segment in state.Body)
        {
            if (segment == cell)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountExits(GameState state, Point cell, Point entered)
    {
        var exits = 0;

        foreach (var direction in _directions)
        {
            var next = Step(cell, direction);

            if (next != entered && !IsBlocked(state, next))
            {
                exits++;
            }
        }

        return exits;
    }

    private static int DistanceToNearestApple(GameState state, Point from)
    {
        var nearest = 500;

        foreach (var (position, kind) in state.Apples)
        {
            if (kind == AppleKind.Poison)
            {
                continue;
            }

            var distance = Math.Abs(position.X - from.X) + Math.Abs(position.Y - from.Y);
            nearest = Math.Min(nearest, distance);
        }

        return nearest;
    }

    private static Point Step(Point position, Direction direction) => direction switch
    {
        Direction.Up => new Point(position.X, position.Y - 1),
        Direction.Down => new Point(position.X, position.Y + 1),
        Direction.Left => new Point(position.X - 1, position.Y),
        Direction.Right => new Point(position.X + 1, position.Y),
        _ => position
    };

    private static bool IsOpposite(Direction current, Direction proposed) =>
        (current == Direction.Up && proposed == Direction.Down) ||
        (current == Direction.Down && proposed == Direction.Up) ||
        (current == Direction.Left && proposed == Direction.Right) ||
        (current == Direction.Right && proposed == Direction.Left);
}
