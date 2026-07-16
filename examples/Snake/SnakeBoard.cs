// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Custom drawing control that renders the snake game board.</summary>
public sealed class SnakeBoard: Control
{
    private static readonly TerminalStyle _borderStyle = new(
        Color.Indexed(8), Color.Default, TerminalAttributes.None);

    private static readonly TerminalStyle _snakeBodyStyle = new(
        Color.Rgb(0, 200, 0), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _snakeHeadStyle = new(
        Color.Rgb(0, 255, 0), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _obstacleStyle = new(
        Color.Indexed(8), Color.Default, TerminalAttributes.None);

    private static readonly TerminalStyle _gameOverStyle = new(
        Color.Rgb(255, 60, 60), Color.Default, TerminalAttributes.Bold);

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

    /// <summary>Raised when the user presses R to restart.</summary>
    public event EventHandler? RestartRequested;

    /// <summary>Raised when the user presses P to pause/unpause.</summary>
    public event EventHandler? PauseToggled;

    /// <summary>Gets or sets the game state to render.</summary>
    public GameState? State { get; set; }

    /// <summary>Requests a visual redraw of the board.</summary>
    public void RequestRedraw() => Invalidate(ChangeImpact.Render);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) =>
        new(constraint.Width ?? 40, constraint.Height ?? 20);

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var state = State;

        if (state is null)
        {
            return;
        }

        canvas.Clear(Bounds, new TerminalStyle(Color.Default, Color.Indexed(0)));
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

        if (state.IsGameOver)
        {
            DrawCenteredOverlay(canvas, "GAME OVER", "Press R to restart", _gameOverStyle);
        }
        else if (state.IsPaused)
        {
            DrawCenteredOverlay(canvas, "PAUSED", "Press P to resume", _pausedStyle);
        }

        if (state.IsSpeedBoosted)
        {
            var speedStyle = new TerminalStyle(Color.Rgb(0, 255, 255), Color.Default, TerminalAttributes.Bold);
            _ = canvas.Draw(
                "⚡ SPEED".AsSpan(),
                new Point(Bounds.X + 2, Bounds.Bottom - 1),
                speedStyle);
        }
    }

    private void DrawCenteredOverlay(TerminalCanvas canvas, string title, string subtitle, TerminalStyle style)
    {
        var centerX = Bounds.X + (Bounds.Width / 2);
        var centerY = Bounds.Y + (Bounds.Height / 2);

        var overlayWidth = Math.Max(title.Length, subtitle.Length) + 6;
        var overlayHeight = 5;
        var overlayRect = new Rect(
            centerX - (overlayWidth / 2),
            centerY - (overlayHeight / 2),
            overlayWidth,
            overlayHeight);

        canvas.Clear(overlayRect, new TerminalStyle(Color.Default, Color.Indexed(0)));
        canvas.DrawBox(overlayRect, LineStyle.Paired, style);

        _ = canvas.Draw(
            title.AsSpan(),
            new Point(centerX - (title.Length / 2), centerY - 1),
            style);
        _ = canvas.Draw(
            subtitle.AsSpan(),
            new Point(centerX - (subtitle.Length / 2), centerY + 1),
            _dimStyle);
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

        switch (code)
        {
            case Code.Up:
                DirectionChanged?.Invoke(this, Direction.Up);
                eventArgs.Handled = true;
                break;
            case Code.Down:
                DirectionChanged?.Invoke(this, Direction.Down);
                eventArgs.Handled = true;
                break;
            case Code.Left:
                DirectionChanged?.Invoke(this, Direction.Left);
                eventArgs.Handled = true;
                break;
            case Code.Right:
                DirectionChanged?.Invoke(this, Direction.Right);
                eventArgs.Handled = true;
                break;
            case Code.Unknown:
                break;
            case Code.Character:
                break;
            case Code.Escape:
                break;
            case Code.Enter:
                break;
            case Code.Tab:
                break;
            case Code.Backspace:
                break;
            case Code.Home:
                break;
            case Code.End:
                break;
            case Code.Insert:
                break;
            case Code.Delete:
                break;
            case Code.PageUp:
                break;
            case Code.PageDown:
                break;
            case Code.F1:
                break;
            case Code.F2:
                break;
            case Code.F3:
                break;
            case Code.F4:
                break;
            case Code.F5:
                break;
            case Code.F6:
                break;
            case Code.F7:
                break;
            case Code.F8:
                break;
            case Code.F9:
                break;
            case Code.F10:
                break;
            case Code.F11:
                break;
            case Code.F12:
                break;
            case Code.F13:
                break;
            case Code.F14:
                break;
            case Code.F15:
                break;
            case Code.F16:
                break;
            case Code.F17:
                break;
            case Code.F18:
                break;
            case Code.F19:
                break;
            case Code.F20:
                break;
            case Code.F21:
                break;
            case Code.F22:
                break;
            case Code.F23:
                break;
            case Code.F24:
                break;
            case Code.F25:
                break;
            case Code.F26:
                break;
            case Code.F27:
                break;
            case Code.F28:
                break;
            case Code.F29:
                break;
            case Code.F30:
                break;
            case Code.F31:
                break;
            case Code.F32:
                break;
            case Code.F33:
                break;
            case Code.F34:
                break;
            case Code.F35:
                break;
            case Code.CapsLock:
                break;
            case Code.ScrollLock:
                break;
            case Code.NumLock:
                break;
            case Code.PrintScreen:
                break;
            case Code.Pause:
                break;
            case Code.Menu:
                break;
            default:
                break;
        }

        if (code == Code.Character)
        {
            if (character == new Rune('w') || character == new Rune('W'))
            {
                DirectionChanged?.Invoke(this, Direction.Up);
                eventArgs.Handled = true;
            }
            else if (character == new Rune('s') || character == new Rune('S'))
            {
                DirectionChanged?.Invoke(this, Direction.Down);
                eventArgs.Handled = true;
            }
            else if (character == new Rune('a') || character == new Rune('A'))
            {
                DirectionChanged?.Invoke(this, Direction.Left);
                eventArgs.Handled = true;
            }
            else if (character == new Rune('d') || character == new Rune('D'))
            {
                DirectionChanged?.Invoke(this, Direction.Right);
                eventArgs.Handled = true;
            }
            else if (character == new Rune('r') || character == new Rune('R'))
            {
                RestartRequested?.Invoke(this, EventArgs.Empty);
                eventArgs.Handled = true;
            }
            else if (character == new Rune('p') || character == new Rune('P'))
            {
                PauseToggled?.Invoke(this, EventArgs.Empty);
                eventArgs.Handled = true;
            }
        }
    }

    private static (Rune Glyph, TerminalStyle Style) AppleVisual(AppleKind kind) => kind switch
    {
        AppleKind.Normal => (new Rune('●'), new TerminalStyle(
            Color.Rgb(0, 255, 0), Color.Default, TerminalAttributes.None)),
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
