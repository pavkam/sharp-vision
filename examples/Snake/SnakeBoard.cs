// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Custom drawing control that renders the snake game board with animations.</summary>
public sealed class SnakeBoard: Control
{
    private static readonly TerminalStyle _borderStyle = new(
        Color.Indexed(8), Color.Default, TerminalAttributes.None);

    private static readonly TerminalStyle _snakeBodyStyle = new(
        Color.Rgb(0, 200, 0), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _snakeHeadStyle = new(
        Color.Rgb(0, 255, 0), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _obstacleStyle = new(
        Color.Rgb(80, 80, 80), Color.Default, TerminalAttributes.Dim);

    private static readonly TerminalStyle _pausedStyle = new(
        Color.Rgb(255, 215, 0), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _dimStyle = new(
        Color.Indexed(7), Color.Default, TerminalAttributes.Dim);

    /// <summary>Initializes a focusable game board.</summary>
    public SnakeBoard()
    {
        CanFocus = true;
        _ = AddHandler(Events.Key, OnKeyPressed);
    }

    /// <summary>Raised when a direction key is pressed.</summary>
    public event EventHandler<Direction>? DirectionChanged;

    /// <summary>Gets or sets the game state to render.</summary>
    public GameState? State { get; set; }

    /// <summary>Gets or sets whether the board should render game content or clear for title screen.</summary>
    public bool ShowBoard { get; set; }

    /// <summary>Gets or sets whether the paused overlay is shown.</summary>
    public bool ShowPaused { get; set; }

    /// <summary>Gets or sets whether the death flash is active.</summary>
    public bool DeathFlashActive { get; set; }

    /// <summary>Gets or sets the current death animation frame.</summary>
    public int DeathFlashFrame { get; set; }

    /// <summary>Requests a visual redraw of the board.</summary>
    public void RequestRedraw() => Invalidate(ChangeImpact.Render);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) =>
        new(constraint.Width ?? 60, constraint.Height ?? 24);

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        canvas.Clear(Bounds, new TerminalStyle(Color.Default, Color.Indexed(0)));

        if (!ShowBoard)
        {
            return;
        }

        var state = State;

        if (state is null)
        {
            return;
        }

        canvas.DrawBox(Bounds, LineStyle.Heavy, _borderStyle);

        var originX = Bounds.X + 1;
        var originY = Bounds.Y + 1;

        foreach (var obstacle in state.Obstacles)
        {
            canvas.DrawRune(
                new Rune('▓'),
                new Point(originX + obstacle.X, originY + obstacle.Y),
                _obstacleStyle);
        }

        foreach (var (position, kind) in state.Apples)
        {
            var (glyph, style) = AppleVisual(kind);
            canvas.DrawRune(glyph, new Point(originX + position.X, originY + position.Y), style);
        }

        if (DeathFlashActive)
        {
            var flashColor = DeathFlashFrame % 2 == 0
                ? Color.Rgb(255, 0, 0)
                : Color.Rgb(255, 200, 0);
            var flashStyle = new TerminalStyle(flashColor, Color.Default, TerminalAttributes.Bold);

            foreach (var segment in state.Body)
            {
                canvas.DrawRune(
                    new Rune('░'),
                    new Point(originX + segment.X, originY + segment.Y),
                    flashStyle);
            }

            return;
        }

        var isHead = true;

        foreach (var segment in state.Body)
        {
            var point = new Point(originX + segment.X, originY + segment.Y);

            if (isHead)
            {
                canvas.DrawRune(new Rune('◉'), point, _snakeHeadStyle);
                isHead = false;
            }
            else
            {
                canvas.DrawRune(new Rune('█'), point, _snakeBodyStyle);
            }
        }

        if (state.IsSpeedBoosted)
        {
            var speedStyle = new TerminalStyle(Color.Rgb(0, 255, 255), Color.Default, TerminalAttributes.Bold);
            _ = canvas.Draw(
                " ⚡ SPEED BOOST ".AsSpan(),
                new Point(Bounds.X + 2, Bounds.Bottom - 1),
                speedStyle);
        }

        if (ShowPaused)
        {
            DrawCenteredBox(canvas, "PAUSED", "Press P to resume", _pausedStyle);
        }
    }

    private void DrawCenteredBox(TerminalCanvas canvas, string title, string subtitle, TerminalStyle style)
    {
        var centerX = Bounds.X + (Bounds.Width / 2);
        var centerY = Bounds.Y + (Bounds.Height / 2);
        var width = Math.Max(title.Length, subtitle.Length) + 6;
        var height = 5;
        var rect = new Rect(centerX - (width / 2), centerY - (height / 2), width, height);

        canvas.Clear(rect, new TerminalStyle(Color.Default, Color.Indexed(0)));
        canvas.DrawBox(rect, LineStyle.Paired, style);
        _ = canvas.Draw(title.AsSpan(), new Point(centerX - (title.Length / 2), centerY - 1), style);
        _ = canvas.Draw(subtitle.AsSpan(), new Point(centerX - (subtitle.Length / 2), centerY + 1), _dimStyle);
    }

    private void OnKeyPressed(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        var code = eventArgs.Stroke.Code;
        var character = eventArgs.Stroke.Character;
        Direction? dir = null;

        if (code == Code.Up || (code == Code.Character && (character == new Rune('w') || character == new Rune('W'))))
        {
            dir = Direction.Up;
        }
        else if (code == Code.Down || (code == Code.Character && (character == new Rune('s') || character == new Rune('S'))))
        {
            dir = Direction.Down;
        }
        else if (code == Code.Left || (code == Code.Character && (character == new Rune('a') || character == new Rune('A'))))
        {
            dir = Direction.Left;
        }
        else if (code == Code.Right || (code == Code.Character && (character == new Rune('d') || character == new Rune('D'))))
        {
            dir = Direction.Right;
        }

        if (dir is { } direction)
        {
            DirectionChanged?.Invoke(this, direction);
            eventArgs.Handled = true;
        }
    }

    private static (Rune Glyph, TerminalStyle Style) AppleVisual(AppleKind kind) => kind switch
    {
        AppleKind.Normal => (new Rune('●'), new TerminalStyle(
            Color.Rgb(50, 255, 50), Color.Default, TerminalAttributes.None)),
        AppleKind.Golden => (new Rune('◆'), new TerminalStyle(
            Color.Rgb(255, 215, 0), Color.Default, TerminalAttributes.Bold)),
        AppleKind.Poison => (new Rune('✦'), new TerminalStyle(
            Color.Rgb(200, 0, 200), Color.Default, TerminalAttributes.None)),
        AppleKind.Speed => (new Rune('★'), new TerminalStyle(
            Color.Rgb(0, 255, 255), Color.Default, TerminalAttributes.Bold)),
        AppleKind.Life => (new Rune('♥'), new TerminalStyle(
            Color.Rgb(255, 50, 50), Color.Default, TerminalAttributes.Bold)),
        _ => (new Rune('?'), new TerminalStyle(Color.Default, Color.Default)),
    };
}
