// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Custom drawing control that renders the snake game board with animations.</summary>
public sealed class SnakeBoard: Control
{
    private static readonly TerminalStyle _borderStyle = new(
        Color.Rgb(127, 127, 127), Color.Default);

    private static readonly TerminalStyle _speedBodyStyle = new(
        Color.Rgb(0, 255, 255), Color.Default, TerminalAttributes.Bold);

    private static readonly TerminalStyle _demoBodyStyle = new(
        Color.Rgb(0, 110, 60), Color.Default, TerminalAttributes.Dim);

    private static readonly TerminalStyle _demoHeadStyle = new(
        Color.Rgb(0, 170, 90), Color.Default);

    private static readonly TerminalStyle _demoAppleStyle = new(
        Color.Rgb(140, 60, 60), Color.Default, TerminalAttributes.Dim);

    private static readonly TerminalStyle _demoObstacleStyle = new(
        Color.Rgb(55, 55, 60), Color.Default, TerminalAttributes.Dim);

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

    private readonly List<SparkleBurst> _bursts = [];
    private readonly List<ScorePopup> _popups = [];

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

    /// <summary>Gets or sets the self-playing demo game rendered dim behind the attract field.</summary>
    /// <remarks>
    /// The demo renders only while <see cref="ShowAttractMode"/> is enabled. Changing the state is
    /// dispatcher-affine while attached and requests render-only invalidation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public GameState? DemoState
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

    #region Transient effects

    /// <summary>Adds one floating score popup above the board content.</summary>
    /// <remarks>Mutation is dispatcher-affine while attached and requests render-only invalidation.</remarks>
    /// <param name="popup">The popup to add at age zero.</param>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void AddScorePopup(ScorePopup popup)
    {
        _popups.Add(popup);
        Invalidate(InvalidationImpact.Render);
    }

    /// <summary>Adds one expanding sparkle burst above the board content.</summary>
    /// <remarks>Mutation is dispatcher-affine while attached and requests render-only invalidation.</remarks>
    /// <param name="burst">The burst to add at age zero.</param>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void AddSparkleBurst(SparkleBurst burst)
    {
        _bursts.Add(burst);
        Invalidate(InvalidationImpact.Render);
    }

    /// <summary>Ages every transient effect by one visual pulse and drops expired ones.</summary>
    /// <remarks>Mutation is dispatcher-affine while attached and requests render-only invalidation.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void AdvanceEffects()
    {
        for (var index = _popups.Count - 1; index >= 0; index--)
        {
            var aged = _popups[index].Aged();

            if (aged.IsExpired)
            {
                _popups.RemoveAt(index);
            }
            else
            {
                _popups[index] = aged;
            }
        }

        for (var index = _bursts.Count - 1; index >= 0; index--)
        {
            var aged = _bursts[index].Aged();

            if (aged.IsExpired)
            {
                _bursts.RemoveAt(index);
            }
            else
            {
                _bursts[index] = aged;
            }
        }

        Invalidate(InvalidationImpact.Render);
    }

    /// <summary>Removes every transient effect immediately, for example when a new game starts.</summary>
    /// <remarks>Mutation is dispatcher-affine while attached and requests render-only invalidation.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ClearEffects()
    {
        _popups.Clear();
        _bursts.Clear();
        Invalidate(InvalidationImpact.Render);
    }

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
            DrawDemoGame(canvas);
        }

        var state = State;

        if (!ShowBoard || state is null || Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        canvas.DrawBox(Bounds, LineStyle.Heavy, BorderStyle(state));

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

        DrawBursts(canvas, origin);
        DrawPopups(canvas, origin);

        if (state.IsSpeedBoosted)
        {
            DrawBoostBanner(canvas);
        }

        if (ShowPaused)
        {
            DrawPause(canvas);
        }
    }

    // The border doubles as an ambient status ring: it breathes cyan while the speed boost is
    // active and strobes red during the death wave, so game state reads at a glance even when the
    // player's eye is not on the HUD.
    private TerminalStyle BorderStyle(GameState state)
    {
        if (DeathPulse >= 0)
        {
            return DeathPulse % 2 == 0
                ? new TerminalStyle(Color.Rgb(255, 60, 60), Color.Default, TerminalAttributes.Bold)
                : new TerminalStyle(Color.Rgb(140, 30, 30), Color.Default);
        }

        if (state.IsSpeedBoosted)
        {
            var wave = TriangleWave(AnimationFrame, 12);
            var level = 150 + (wave * 105 / 6);
            return new TerminalStyle(Color.Rgb(0, level, level), Color.Default, TerminalAttributes.Bold);
        }

        return _borderStyle;
    }

    // A triangle wave keeps pulse animation symmetric (rise then fall) using only integer math on
    // the bounded animation frame.
    private static int TriangleWave(int frame, int period)
    {
        var half = period / 2;
        var phase = frame % period;
        return phase <= half ? phase : period - phase;
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
        var count = state.Body.Count;
        var segmentIndex = 0;

        // The shimmer highlight travels the full body once per 60-frame visual cycle, so longer
        // snakes show a faster-looking glint without any extra timers.
        var shimmer = count == 0 ? 0 : AnimationFrame * (count + 6) / SnakeAnimationState.RainbowFrames;

        foreach (var segment in state.Body)
        {
            var point = new Point(origin.X + segment.X, origin.Y + segment.Y);

            if (segmentIndex == 0)
            {
                var green = 210 + (AnimationFrame % 4 * 15);
                var style = new TerminalStyle(
                    state.IsSpeedBoosted ? Color.Rgb(120, 255, 255) : Color.Rgb(0, green, 0),
                    Color.Default,
                    TerminalAttributes.Bold);
                canvas.DrawRune(HeadGlyph(state.Heading), point, style);
            }
            else
            {
                canvas.DrawRune(new Rune('█'), point, BodyStyle(state, segmentIndex, count, shimmer));
            }

            segmentIndex++;
        }
    }

    private static Rune HeadGlyph(Direction heading) => heading switch
    {
        Direction.Up => new Rune('▲'),
        Direction.Down => new Rune('▼'),
        Direction.Left => new Rune('◀'),
        Direction.Right => new Rune('▶'),
        _ => new Rune('◉')
    };

    // Body cells fade from bright near the head to dark at the tail, and a two-cell shimmer
    // highlight sweeps head-to-tail. The boosted palette swaps the green ramp for cyan.
    private static TerminalStyle BodyStyle(GameState state, int segmentIndex, int count, int shimmer)
    {
        var t = count <= 2 ? 0d : (segmentIndex - 1) / (double) (count - 2);
        var color = state.IsSpeedBoosted
            ? Color.Rgb(0, Lerp(255, 90, t), Lerp(255, 110, t))
            : Color.Rgb(0, Lerp(230, 85, t), Lerp(70, 25, t));

        if (Math.Abs(segmentIndex - shimmer) <= 1)
        {
            color = state.IsSpeedBoosted
                ? Color.Rgb(190, 255, 255)
                : Color.Rgb(170, 255, 140);
        }

        return new TerminalStyle(color, Color.Default, TerminalAttributes.Bold);
    }

    private static int Lerp(int from, int to, double t) =>
        (int) Math.Round(from + ((to - from) * Math.Clamp(t, 0d, 1d)));

    // The demo game renders entirely in muted colors so the FIGlet title and cards stay dominant;
    // it exists to make the title screen feel alive, not to compete with it.
    private void DrawDemoGame(TerminalCanvas canvas)
    {
        if (DemoState is not { } demo)
        {
            return;
        }

        foreach (var obstacle in demo.Obstacles)
        {
            DrawDemoCell(canvas, obstacle, new Rune('▓'), _demoObstacleStyle);
        }

        foreach (var (position, _) in demo.Apples)
        {
            DrawDemoCell(canvas, position, new Rune('•'), _demoAppleStyle);
        }

        var segmentIndex = 0;

        foreach (var segment in demo.Body)
        {
            if (segmentIndex == 0)
            {
                DrawDemoCell(canvas, segment, HeadGlyph(demo.Heading), _demoHeadStyle);
            }
            else
            {
                DrawDemoCell(canvas, segment, new Rune('█'), _demoBodyStyle);
            }

            segmentIndex++;
        }
    }

    private void DrawDemoCell(TerminalCanvas canvas, Point cell, Rune glyph, TerminalStyle style)
    {
        var point = new Point(Bounds.X + cell.X, Bounds.Y + cell.Y);

        if (point.X >= Bounds.X && point.X < Bounds.Right && point.Y >= Bounds.Y && point.Y < Bounds.Bottom)
        {
            canvas.DrawRune(glyph, point, style);
        }
    }

    private void DrawPopups(TerminalCanvas canvas, Point origin)
    {
        foreach (var popup in _popups)
        {
            var y = origin.Y + popup.Position.Y - popup.Rise;
            var x = origin.X + popup.Position.X - (popup.Text.Length / 2);
            x = Math.Clamp(x, Bounds.X + 1, Math.Max(Bounds.X + 1, Bounds.Right - 1 - popup.Text.Length));

            if (y <= Bounds.Y || y >= Bounds.Bottom - 1)
            {
                continue;
            }

            var attributes = popup.Age switch
            {
                < 4 => TerminalAttributes.Bold,
                < 7 => TerminalAttributes.None,
                _ => TerminalAttributes.Dim
            };
            _ = canvas.Draw(
                popup.Text.AsSpan(),
                new Point(x, y),
                new TerminalStyle(popup.Color, Color.Default, attributes));
        }
    }

    private void DrawBursts(TerminalCanvas canvas, Point origin)
    {
        foreach (var burst in _bursts)
        {
            var center = new Point(origin.X + burst.Center.X, origin.Y + burst.Center.Y);
            var radius = (burst.Age / 2) + 1;
            var attributes = burst.Age < 2 ? TerminalAttributes.Bold : TerminalAttributes.Dim;
            var style = new TerminalStyle(burst.Color, Color.Default, attributes);
            var glyph = burst.Age < 3 ? new Rune('✧') : new Rune('·');

            if (burst.Age == 0)
            {
                DrawBurstCell(canvas, center, new Rune('✦'), style);
                continue;
            }

            DrawBurstCell(canvas, new Point(center.X, center.Y - radius), glyph, style);
            DrawBurstCell(canvas, new Point(center.X, center.Y + radius), glyph, style);
            DrawBurstCell(canvas, new Point(center.X - radius, center.Y), glyph, style);
            DrawBurstCell(canvas, new Point(center.X + radius, center.Y), glyph, style);
            DrawBurstCell(canvas, new Point(center.X - radius, center.Y - radius), new Rune('·'), style);
            DrawBurstCell(canvas, new Point(center.X + radius, center.Y - radius), new Rune('·'), style);
            DrawBurstCell(canvas, new Point(center.X - radius, center.Y + radius), new Rune('·'), style);
            DrawBurstCell(canvas, new Point(center.X + radius, center.Y + radius), new Rune('·'), style);
        }
    }

    private void DrawBurstCell(TerminalCanvas canvas, Point point, Rune glyph, TerminalStyle style)
    {
        if (point.X > Bounds.X && point.X < Bounds.Right - 1 && point.Y > Bounds.Y && point.Y < Bounds.Bottom - 1)
        {
            canvas.DrawRune(glyph, point, style);
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

        // The golden apple twinkles through a four-glyph diamond cycle so the highest-value pickup
        // is also the most animated object on the board.
        var goldenTwinkle = (AnimationFrame / 2 % 4) switch
        {
            0 => new Rune('◆'),
            1 => new Rune('◈'),
            2 => new Rune('◇'),
            _ => new Rune('◈')
        };

        return kind switch
        {
            AppleKind.Normal => (new Rune('●'), new TerminalStyle(
                PulseColor(50, 255, 50, bright), Color.Default)),
            AppleKind.Golden => (goldenTwinkle, new TerminalStyle(
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

        if (eventArgs.Phase != RoutingPhase.Bubble ||
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
