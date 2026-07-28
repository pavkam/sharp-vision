// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Custom drawing control that renders the snake game board with animations.</summary>
public sealed class SnakeBoard: Control
{
    private static readonly TerminalStyle _borderStyle = new(
        Color.Rgb(127, 127, 127), Color.Default);

    private static readonly TerminalStyle _snakeBodyStyle = new(
        Color.Rgb(0, 200, 0), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _speedBodyStyle = new(
        Color.Rgb(0, 255, 255), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _deathBodyStyle = new(
        Color.Rgb(0, 120, 0), Color.Default, TerminalAttributes.Dim);

    private static readonly TerminalStyle _deathRedStyle = new(
        Color.Rgb(255, 45, 45), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _deathGoldStyle = new(
        Color.Rgb(255, 200, 0), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _obstacleStyle = new(
        Color.Rgb(80, 80, 80), Color.Default, TerminalAttributes.Dim);

    private static readonly TerminalStyle _pausedStyle = new(
        Color.Rgb(255, 215, 0), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _dimStyle = new(
        Color.Rgb(229, 229, 229), Color.Default, TerminalAttributes.Dim);

    #region Construction and properties

    /// <summary>Initializes a focusable game board.</summary>
    public SnakeBoard()
    {
        Focusable = true;
        _ = AddHandler(Events.Key, OnKeyPressed);
    }

    /// <summary>Raised when a direction key is pressed.</summary>
    public event EventHandler<Direction>? DirectionChanged;

    /// <summary>Gets or sets the game state rendered inside the board.</summary>
    /// <remarks>
    /// A null value leaves only the optional attract-mode field visible. Changing the state is
    /// dispatcher-affine while attached and requests render-only invalidation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public GameState? State
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets whether game-board content is rendered.</summary>
    /// <remarks>
    /// The attract-mode field remains independently visible when enabled. Changing this value is
    /// dispatcher-affine while attached and requests render-only invalidation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ShowBoard
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets whether the centered paused overlay is rendered above board content.</summary>
    /// <remarks>Changing this value is dispatcher-affine while attached and requests render-only invalidation.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ShowPaused
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets whether the sparse ambient title-screen field is rendered.</summary>
    /// <remarks>
    /// The field can render without a game state or visible board. Changing this value is
    /// dispatcher-affine while attached and requests render-only invalidation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ShowAttractMode
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets the current visual animation frame in the inclusive range zero through 59.</summary>
    /// <remarks>
    /// The frame drives ambient, apple, head, and speed-boost motion without changing game state.
    /// Mutation is dispatcher-affine while attached and requests render-only invalidation.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside [0, 59].</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int AnimationFrame
    {
        get;
        set
        {
            if (value is < 0 or > 59)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "The animation frame must be from zero through 59.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the requested number of snake segments revealed by the death wave.</summary>
    /// <remarks>
    /// Rendering clamps this value to the current body length. Mutation is dispatcher-affine while
    /// attached and requests render-only invalidation.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int DeathVisibleSegments
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the current death pulse, where minus one disables the death presentation.</summary>
    /// <remarks>
    /// Active pulse values range from zero through 14. Mutation is dispatcher-affine while attached
    /// and requests render-only invalidation.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside [-1, 14].</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int DeathPulse
    {
        get;
        set
        {
            if (value is < -1 or > 14)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "The death pulse must be from minus one through 14.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = -1;

    /// <summary>Gets or sets whether the compatibility death-flash presentation is active.</summary>
    /// <remarks>
    /// This forwarding property keeps the existing Snake screen source-compatible until it adopts
    /// <see cref="DeathPulse"/> and <see cref="DeathVisibleSegments"/> directly. Enabling it reveals
    /// the current body and starts pulse zero; disabling it sets the pulse to minus one. Mutation is
    /// dispatcher-affine while attached and requests render-only invalidation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool DeathFlashActive
    {
        get => DeathPulse >= 0;
        set
        {
            if (value && DeathPulse < 0)
            {
                DeathVisibleSegments = State?.Body.Count ?? 0;
            }

            DeathPulse = value ? Math.Max(0, DeathPulse) : -1;
        }
    }

    /// <summary>Gets or sets the compatibility death-flash frame in the inclusive range zero through 14.</summary>
    /// <remarks>
    /// While the compatibility flash is active, this value forwards to <see cref="DeathPulse"/>.
    /// Setting it while inactive validates dispatcher and disposal state but does not activate the
    /// death presentation. This property exists only to bridge the current Snake screen implementation.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside [0, 14].</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int DeathFlashFrame
    {
        get => Math.Max(0, DeathPulse);
        set
        {
            if (value is < 0 or > 14)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "The death flash frame must be from zero through 14.");
            }

            DeathPulse = DeathPulse < 0 ? -1 : value;
        }
    }

    /// <summary>Requests a visual redraw of the board.</summary>
    /// <remarks>The request is dispatcher-affine while the control is attached.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void RequestRedraw() => Invalidate(InvalidationImpact.Render);

    #endregion

    #region Rendering helpers

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) =>
        new(constraint.Width ?? 60, constraint.Height ?? 24);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        canvas.Clear(Bounds, new TerminalStyle(Color.Default, Color.Rgb(0, 0, 0)));

        if (ShowAttractMode)
        {
            DrawAttractMode(canvas);
        }

        var state = State;

        if (!ShowBoard || state is null || Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        canvas.DrawBox(Bounds, LineStyle.Heavy, _borderStyle);

        var origin = new Point(Bounds.X + 1, Bounds.Y + 1);
        DrawObstacles(canvas, state, origin);
        DrawApples(canvas, state, origin);

        if (DeathPulse >= 0)
        {
            DrawDeathPresentation(canvas, state, origin);
        }
        else
        {
            DrawSnake(canvas, state, origin);
        }

        if (state.IsSpeedBoosted)
        {
            DrawBoostBanner(canvas);
        }

        if (ShowPaused)
        {
            DrawPause(canvas);
        }
    }

    private void DrawAttractMode(TerminalCanvas canvas)
    {
        var color = AnimationFrame % 2 == 0
            ? Color.Rgb(18, 58, 42)
            : Color.Rgb(24, 72, 54);
        var style = new TerminalStyle(color, Color.Default, TerminalAttributes.Dim);

        for (var y = Bounds.Y; y < Bounds.Bottom; y++)
        {
            for (var x = Bounds.X; x < Bounds.Right; x++)
            {
                var signature = ((long) x * 17) + ((long) y * 31) + AnimationFrame;

                if (Math.Abs(signature % 47) is 0 or 1)
                {
                    canvas.DrawRune(new Rune('.'), new Point(x, y), style);
                }
            }
        }
    }

    private static void DrawObstacles(TerminalCanvas canvas, GameState state, Point origin)
    {
        foreach (var obstacle in state.Obstacles)
        {
            canvas.DrawRune(
                new Rune('▓'),
                new Point(origin.X + obstacle.X, origin.Y + obstacle.Y),
                _obstacleStyle);
        }
    }

    private void DrawApples(TerminalCanvas canvas, GameState state, Point origin)
    {
        foreach (var (position, kind) in state.Apples)
        {
            var (glyph, style) = AppleVisual(kind);
            canvas.DrawRune(glyph, new Point(origin.X + position.X, origin.Y + position.Y), style);
        }
    }

    private void DrawSnake(TerminalCanvas canvas, GameState state, Point origin)
    {
        var segmentIndex = 0;

        foreach (var segment in state.Body)
        {
            var point = new Point(origin.X + segment.X, origin.Y + segment.Y);

            if (segmentIndex == 0)
            {
                var green = 210 + (AnimationFrame % 4 * 15);
                var style = new TerminalStyle(
                    Color.Rgb(0, green, 0),
                    Color.Default,
                    TerminalAttributes.Bold);
                canvas.DrawRune(new Rune('◉'), point, style);
            }
            else
            {
                var style = state.IsSpeedBoosted && (segmentIndex + AnimationFrame) % 3 == 0
                    ? _speedBodyStyle
                    : _snakeBodyStyle;
                canvas.DrawRune(new Rune('█'), point, style);
            }

            segmentIndex++;
        }
    }

    private void DrawDeathPresentation(TerminalCanvas canvas, GameState state, Point origin)
    {
        var visibleSegments = Math.Min(DeathVisibleSegments, state.Body.Count);
        var segmentIndex = 0;

        foreach (var segment in state.Body)
        {
            var point = new Point(origin.X + segment.X, origin.Y + segment.Y);

            if (segmentIndex < visibleSegments)
            {
                var style = (segmentIndex + DeathPulse) % 2 == 0
                    ? _deathRedStyle
                    : _deathGoldStyle;
                canvas.DrawRune(new Rune('░'), point, style);
            }
            else
            {
                canvas.DrawRune(new Rune('█'), point, _deathBodyStyle);
            }

            segmentIndex++;
        }
    }

    private void DrawBoostBanner(TerminalCanvas canvas)
    {
        _ = canvas.Draw(
            " ⚡ SPEED BOOST ".AsSpan(),
            new Point(Bounds.X + 2, Bounds.Bottom - 1),
            _speedBodyStyle);
    }

    private void DrawPause(TerminalCanvas canvas) =>
        DrawCenteredBox(canvas, "PAUSED", "P  RESUME", _pausedStyle);

    private void DrawCenteredBox(TerminalCanvas canvas, string title, string subtitle, TerminalStyle style)
    {
        var centerX = Bounds.X + (Bounds.Width / 2);
        var centerY = Bounds.Y + (Bounds.Height / 2);
        var width = Math.Max(title.Length, subtitle.Length) + 6;
        const int height = 5;
        var rect = new Rect(centerX - (width / 2), centerY - (height / 2), width, height);

        canvas.Clear(rect, new TerminalStyle(Color.Default, Color.Rgb(0, 0, 0)));
        canvas.DrawBox(rect, LineStyle.Paired, style);
        _ = canvas.Draw(title.AsSpan(), new Point(centerX - (title.Length / 2), centerY - 1), style);
        _ = canvas.Draw(subtitle.AsSpan(), new Point(centerX - (subtitle.Length / 2), centerY + 1), _dimStyle);
    }

    private (Rune Glyph, TerminalStyle Style) AppleVisual(AppleKind kind)
    {
        var bright = AnimationFrame % 2 == 0;
        return kind switch
        {
            AppleKind.Normal => (new Rune('●'), new TerminalStyle(
                PulseColor(50, 255, 50, bright), Color.Default)),
            AppleKind.Golden => (new Rune('◆'), new TerminalStyle(
                PulseColor(255, 215, 0, bright), Color.Default, TerminalAttributes.Bold)),
            AppleKind.Poison => (new Rune('✦'), new TerminalStyle(
                PulseColor(200, 0, 200, bright), Color.Default)),
            AppleKind.Speed => (new Rune('★'), new TerminalStyle(
                PulseColor(0, 255, 255, bright), Color.Default, TerminalAttributes.Bold)),
            AppleKind.Life => (new Rune('♥'), new TerminalStyle(
                PulseColor(255, 50, 50, bright), Color.Default, TerminalAttributes.Bold)),
            _ => (new Rune('?'), new TerminalStyle(Color.Default, Color.Default))
        };
    }

    private static Color PulseColor(int red, int green, int blue, bool bright)
    {
        const int dimming = 40;
        var adjustment = bright ? 0 : -dimming;
        return Color.Rgb(
            Math.Clamp(red + adjustment, 0, byte.MaxValue),
            Math.Clamp(green + adjustment, 0, byte.MaxValue),
            Math.Clamp(blue + adjustment, 0, byte.MaxValue));
    }

    #endregion

    #region Input

    private void OnKeyPressed(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble ||
            (eventArgs.Stroke.Action != KeyAction.Press && eventArgs.Stroke.Action != KeyAction.Repeat))
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
        else if (code == Code.Down ||
                 (code == Code.Character && (character == new Rune('s') || character == new Rune('S'))))
        {
            dir = Direction.Down;
        }
        else if (code == Code.Left ||
                 (code == Code.Character && (character == new Rune('a') || character == new Rune('A'))))
        {
            dir = Direction.Left;
        }
        else if (code == Code.Right ||
                 (code == Code.Character && (character == new Rune('d') || character == new Rune('D'))))
        {
            dir = Direction.Right;
        }

        if (dir is { } direction)
        {
            DirectionChanged?.Invoke(this, direction);
            eventArgs.Handled = true;
        }
    }

    #endregion
}
