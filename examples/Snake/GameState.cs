// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Pure game logic for the snake board — no UI dependencies.</summary>
public sealed class GameState
{
    private readonly LinkedList<Point> _body = new();
    private readonly HashSet<Point> _occupied = [];
    private readonly List<(Point Position, AppleKind Kind)> _apples = [];
    private readonly HashSet<Point> _obstacles = [];
    private readonly Random _random = new();
    private Direction _direction = Direction.Right;
    private Direction _pendingDirection = Direction.Right;
    private int _growRemaining;
    private int _shrinkRemaining;
    private int _speedTicksRemaining;

    /// <summary>Initializes a new game on the given board dimensions.</summary>
    public GameState(int width, int height, int difficulty)
    {
        Width = width;
        Height = height;
        Difficulty = Math.Clamp(difficulty, 0, 2);
        Lives = 3;
        Reset();
    }

    /// <summary>Gets the playable board width (inside borders).</summary>
    public int Width { get; }

    /// <summary>Gets the playable board height (inside borders).</summary>
    public int Height { get; }

    /// <summary>Gets the difficulty level (0=Easy, 1=Medium, 2=Hard).</summary>
    public int Difficulty { get; private set; }

    /// <summary>Gets the current score.</summary>
    public int Score { get; private set; }

    /// <summary>Gets the remaining lives.</summary>
    public int Lives { get; private set; }

    /// <summary>Gets whether the game is over.</summary>
    public bool IsGameOver { get; private set; }

    /// <summary>Gets whether the game is paused.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Gets whether speed boost is active.</summary>
    public bool IsSpeedBoosted => _speedTicksRemaining > 0;

    /// <summary>Gets the snake body segments (head is first).</summary>
    public IReadOnlyCollection<Point> Body => _body;

    /// <summary>Gets the head position.</summary>
    public Point Head => _body.First!.Value;

    /// <summary>Gets the active apple items.</summary>
    public IReadOnlyList<(Point Position, AppleKind Kind)> Apples => _apples;

    /// <summary>Gets the obstacle positions.</summary>
    public IReadOnlyCollection<Point> Obstacles => _obstacles;

    /// <summary>Gets the base tick interval for the current difficulty.</summary>
    public int BaseTickMs => Difficulty switch
    {
        0 => 200,
        1 => 140,
        _ => 90,
    };

    /// <summary>Gets the current tick interval accounting for speed boost.</summary>
    public int CurrentTickMs => IsSpeedBoosted ? Math.Max(40, BaseTickMs / 2) : BaseTickMs;

    /// <summary>Gets the difficulty name.</summary>
    public string DifficultyName => Difficulty switch
    {
        0 => "Easy",
        1 => "Medium",
        _ => "Hard",
    };

    /// <summary>Queues a direction change for the next tick.</summary>
    public void ChangeDirection(Direction direction)
    {
        if (IsOpposite(_direction, direction))
        {
            return;
        }

        _pendingDirection = direction;
    }

    /// <summary>Advances the game by one step.</summary>
    public TickResult Tick()
    {
        if (IsGameOver || IsPaused)
        {
            return TickResult.Moved;
        }

        _direction = _pendingDirection;

        if (_speedTicksRemaining > 0)
        {
            _speedTicksRemaining--;
        }

        var head = Head;
        var next = Advance(head, _direction);

        if (next.X < 0 || next.X >= Width || next.Y < 0 || next.Y >= Height)
        {
            return Die();
        }

        if (_obstacles.Contains(next))
        {
            return Die();
        }

        if (_occupied.Contains(next) && !(next == _body.Last!.Value && _growRemaining == 0))
        {
            return Die();
        }

        var ate = FindApple(next);

        _ = _body.AddFirst(next);
        _ = _occupied.Add(next);

        if (_growRemaining > 0)
        {
            _growRemaining--;
        }
        else if (_shrinkRemaining > 0 && _body.Count > 2)
        {
            RemoveTail();
            RemoveTail();
            _shrinkRemaining--;
        }
        else
        {
            RemoveTail();
        }

        if (ate is { } apple)
        {
            ApplyApple(apple);
            _ = _apples.Remove(apple);
            EnsureApples();
            return TickResult.Ate;
        }

        return TickResult.Moved;
    }

    /// <summary>Resets the game to the initial state.</summary>
    public void Reset()
    {
        _body.Clear();
        _occupied.Clear();
        _apples.Clear();
        _obstacles.Clear();
        Score = 0;
        IsGameOver = false;
        IsPaused = false;
        _growRemaining = 0;
        _shrinkRemaining = 0;
        _speedTicksRemaining = 0;
        _direction = Direction.Right;
        _pendingDirection = Direction.Right;

        var startX = Width / 4;
        var startY = Height / 2;

        for (var i = 3; i >= 0; i--)
        {
            var point = new Point(startX - i, startY);
            _ = _body.AddFirst(point);
            _ = _occupied.Add(point);
        }

        GenerateObstacles();
        EnsureApples();
    }

    /// <summary>Increases the difficulty and resets.</summary>
    public void NextDifficulty()
    {
        Difficulty = Math.Min(2, Difficulty + 1);
        Lives = 3;
        Reset();
    }

    private TickResult Die()
    {
        Lives--;

        if (Lives <= 0)
        {
            IsGameOver = true;
        }
        else
        {
            _body.Clear();
            _occupied.Clear();
            _growRemaining = 0;
            _shrinkRemaining = 0;
            _direction = Direction.Right;
            _pendingDirection = Direction.Right;

            var startX = Width / 4;
            var startY = Height / 2;

            for (var i = 3; i >= 0; i--)
            {
                var point = new Point(startX - i, startY);
                _ = _body.AddFirst(point);
                _ = _occupied.Add(point);
            }
        }

        return TickResult.Died;
    }

    private void RemoveTail()
    {
        if (_body.Count == 0)
        {
            return;
        }

        var tail = _body.Last!.Value;
        _body.RemoveLast();
        _ = _occupied.Remove(tail);
    }

    private (Point Position, AppleKind Kind)? FindApple(Point position)
    {
        foreach (var apple in _apples)
        {
            if (apple.Position == position)
            {
                return apple;
            }
        }

        return null;
    }

    private void ApplyApple((Point Position, AppleKind Kind) apple)
    {
        switch (apple.Kind)
        {
            case AppleKind.Normal:
                _growRemaining += 1;
                Score += 10;
                break;
            case AppleKind.Golden:
                _growRemaining += 3;
                Score += 50;
                break;
            case AppleKind.Poison:
                if (_body.Count <= 3)
                {
                    Lives--;

                    if (Lives <= 0)
                    {
                        IsGameOver = true;
                    }
                }
                else
                {
                    _shrinkRemaining += 2;
                }

                Score = Math.Max(0, Score - 5);
                break;
            case AppleKind.Speed:
                _speedTicksRemaining = (int) (5000.0 / CurrentTickMs);
                Score += 20;
                break;
            case AppleKind.Life:
                Lives = Math.Min(5, Lives + 1);
                Score += 15;
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void EnsureApples()
    {
        var area = Width * Height;
        var target = Math.Max(2, (area / 200) + Difficulty + 1);

        while (_apples.Count < target)
        {
            SpawnApple();
        }
    }

    private void SpawnApple()
    {
        var position = FindEmptyCell();

        if (position is null)
        {
            return;
        }

        var kind = PickAppleKind();
        _apples.Add((position.Value, kind));
    }

    private AppleKind PickAppleKind()
    {
        var roll = _random.Next(100);
        return roll switch
        {
            < 55 => AppleKind.Normal,
            < 75 => AppleKind.Golden,
            < 88 => AppleKind.Poison,
            < 96 => AppleKind.Speed,
            _ => AppleKind.Life,
        };
    }

    private Point? FindEmptyCell(int clearanceFromSnake = 0)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var point = new Point(_random.Next(Width), _random.Next(Height));

            if (_occupied.Contains(point) || _obstacles.Contains(point))
            {
                continue;
            }

            if (!_apples.TrueForAll(a => a.Position != point))
            {
                continue;
            }

            if (clearanceFromSnake > 0)
            {
                var tooClose = false;

                foreach (var segment in _body)
                {
                    if (Math.Abs(segment.X - point.X) + Math.Abs(segment.Y - point.Y) < clearanceFromSnake)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }
            }

            return point;
        }

        return null;
    }

    private void GenerateObstacles()
    {
        var area = Width * Height;
        var density = Difficulty switch
        {
            0 => 80,
            1 => 50,
            _ => 30,
        };
        var count = Math.Max(3, area / density);

        for (var i = 0; i < count; i++)
        {
            var point = FindEmptyCell(clearanceFromSnake: 8);

            if (point is not null)
            {
                _ = _obstacles.Add(point.Value);
            }
        }
    }

    private static Point Advance(Point position, Direction direction) => direction switch
    {
        Direction.Up => new Point(position.X, position.Y - 1),
        Direction.Down => new Point(position.X, position.Y + 1),
        Direction.Left => new Point(position.X - 1, position.Y),
        Direction.Right => new Point(position.X + 1, position.Y),
        _ => position,
    };

    private static bool IsOpposite(Direction current, Direction proposed) =>
        (current == Direction.Up && proposed == Direction.Down) ||
        (current == Direction.Down && proposed == Direction.Up) ||
        (current == Direction.Left && proposed == Direction.Right) ||
        (current == Direction.Right && proposed == Direction.Left);
}
