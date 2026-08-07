// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Owns the retained Snake presentation, game phases, input, and animation loops.</summary>
public sealed class SnakeScreen: Screen
{
    private static readonly TimeSpan _visualInterval = TimeSpan.FromMilliseconds(80);

    private readonly SnakeAnimationState _animation = new();
    private readonly SnakeBoard _board;
    private readonly HighScoreTable _highScores = new();
    private readonly SnakeHud _hud;
    private readonly List<Rune> _initials = [];
    private readonly SnakeTitlePanel _titlePanel;
    private CancellationTokenSource? _gameLoopCts;
    private Task? _gameLoopTask;
    private CancellationTokenSource? _visualLoopCts;
    private Task? _visualLoopTask;
    private GameState _state;
    private GameState? _demoState;
    private bool _demoTickParity;
    private GamePhase _phase;
    private int _gameTickQueued;
    private int _selectedDifficulty;
    private int _visualPulseQueued;

    /// <summary>Initializes the full retained game layout.</summary>
    public SnakeScreen()
    {
        _state = new GameState(width: 40, height: 20, difficulty: 0);

        _board = new SnakeBoard { State = _state };
        _board.DirectionChanged += OnDirectionChanged;
        _titlePanel = new SnakeTitlePanel();
        _hud = new SnakeHud();

        var gameArea = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        gameArea.Children.Add(_board);
        Overlay.SetZIndex(_titlePanel, 10);
        gameArea.Children.Add(_titlePanel);

        var layout = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Dock.SetSide(_hud, DockSide.Top);
        layout.Children.Add(_hud);
        layout.Children.Add(gameArea);
        InitializeContent(layout);

        _ = AddHandler(Events.Key, OnKey);
        TransitionTo(GamePhase.Title);
    }

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnAttach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Theme = ThemeCatalog.Dark;
    }

    /// <inheritdoc/>
    protected override void OnStarted(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _ = application.Focus.Focus(_board);
        StartVisualLoop();
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        StopGameLoop();
        StopVisualLoop();
        _board.DirectionChanged -= OnDirectionChanged;
    }

    #endregion

    #region Phase transitions

    /// <summary>
    /// Applies one validated game phase to the retained board, title panel, and HUD.
    /// </summary>
    /// <param name="phase">The defined phase to make current.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="phase"/> is undefined.</exception>
    /// <exception cref="InvalidOperationException">The attached screen is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The screen or an owned control is disposed.</exception>
    internal void TransitionTo(GamePhase phase)
    {
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "The game phase is unknown.");
        }

        var difficulty = SelectedDifficultyName();
        var bestScore = BestScore();

        switch (phase)
        {
            case GamePhase.Title:
                _board.ShowBoard = false;
                _board.ShowAttractMode = true;
                _board.ShowPaused = false;
                _titlePanel.Visibility = Visibility.Visible;
                _titlePanel.ShowTitle(difficulty, _highScores.Entries);
                _hud.UpdateTitle(difficulty);
                break;
            case GamePhase.Playing:
                _board.ShowBoard = true;
                _board.ShowAttractMode = false;
                _board.ShowPaused = false;
                _titlePanel.Visibility = Visibility.Collapsed;
                _hud.UpdateGame(_state, bestScore);
                break;
            case GamePhase.DeathAnimation:
                _board.ShowBoard = true;
                _board.ShowAttractMode = false;
                _board.ShowPaused = false;
                _titlePanel.Visibility = Visibility.Collapsed;
                _hud.UpdateGame(_state, bestScore);
                break;
            case GamePhase.HighScoreEntry:
                _board.ShowBoard = false;
                _board.ShowAttractMode = true;
                _board.ShowPaused = false;
                _titlePanel.Visibility = Visibility.Visible;
                _titlePanel.ShowRecord(_state.Score, InitialsText());
                _hud.UpdateTitle(difficulty);
                break;
            case GamePhase.GameOver:
                _board.ShowBoard = false;
                _board.ShowAttractMode = true;
                _board.ShowPaused = false;
                _titlePanel.Visibility = Visibility.Visible;
                _titlePanel.ShowGameOver(_state.Score);
                _hud.UpdateTitle(difficulty);
                break;
            case GamePhase.Paused:
                _board.ShowBoard = true;
                _board.ShowAttractMode = false;
                _board.ShowPaused = true;
                _titlePanel.Visibility = Visibility.Collapsed;
                _hud.UpdatePaused(_state, bestScore);
                break;
            default:
                throw new UnreachableException();
        }

        _phase = phase;
    }

    private void StartGame()
    {
        var playWidth = Math.Max(10, _board.Bounds.Width - 2);
        var playHeight = Math.Max(6, _board.Bounds.Height - 2);
        _state = new GameState(playWidth, playHeight, _selectedDifficulty);
        _board.State = _state;
        _board.DeathPulse = -1;
        _board.DeathVisibleSegments = 0;
        _board.ClearEffects();
        TransitionTo(GamePhase.Playing);
        StartGameLoop();
    }

    private void TriggerDeathAnimation()
    {
        StopGameLoop();
        _animation.BeginDeath();
        _board.DeathPulse = _animation.DeathPulse;
        _board.DeathVisibleSegments = _animation.VisibleDeathSegments(_state.Body.Count);
        TransitionTo(GamePhase.DeathAnimation);
    }

    private void OnDeathAnimationComplete()
    {
        _board.DeathPulse = -1;
        _board.DeathVisibleSegments = 0;

        if (_state.IsGameOver)
        {
            if (_highScores.Qualifies(_state.Score))
            {
                _initials.Clear();
                TransitionTo(GamePhase.HighScoreEntry);
            }
            else
            {
                TransitionTo(GamePhase.GameOver);
            }

            return;
        }

        TransitionTo(GamePhase.Playing);
        StartGameLoop();
    }

    private string SelectedDifficultyName() => _selectedDifficulty switch
    {
        0 => "Easy",
        1 => "Medium",
        _ => "Hard"
    };

    private int BestScore()
    {
        var tableBest = _highScores.Entries.Count == 0 ? 0 : _highScores.Entries[0].Score;
        return Math.Max(_state.Score, tableBest);
    }

    #endregion

    #region Animation loops

    private void StartGameLoop()
    {
        StopGameLoop();

        var cts = new CancellationTokenSource();
        _gameLoopCts = cts;
        _gameLoopTask = Task.Run(() => GameLoopAsync(cts.Token));
    }

    private void StopGameLoop()
    {
        var cts = _gameLoopCts;
        var task = _gameLoopTask;
        _gameLoopCts = null;
        _gameLoopTask = null;

        if (cts is null)
        {
            Debug.Assert(task is null, "A game-loop task must have an owning cancellation source.");
            return;
        }

        Debug.Assert(task is not null, "A game-loop cancellation source must have an observed task.");

        try
        {
            cts.Cancel();

            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task GameLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_state.CurrentTickMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var application = Application;

            if (application is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _gameTickQueued, 1, 0) != 0)
            {
                continue;
            }

            try
            {
                application.Dispatcher.Post(() =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested || Disposed || _phase != GamePhase.Playing)
                        {
                            return;
                        }

                        TickGame();
                    }
                    finally
                    {
                        Volatile.Write(ref _gameTickQueued, 0);
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                Volatile.Write(ref _gameTickQueued, 0);
                return;
            }
            catch (InvalidOperationException)
            {
                Volatile.Write(ref _gameTickQueued, 0);
            }
        }
    }

    private void StartVisualLoop()
    {
        if (_visualLoopCts is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _visualLoopCts = cts;
        _visualLoopTask = Task.Run(() => VisualLoopAsync(cts.Token));
    }

    private void StopVisualLoop()
    {
        var cts = _visualLoopCts;
        var task = _visualLoopTask;
        _visualLoopCts = null;
        _visualLoopTask = null;

        if (cts is null)
        {
            Debug.Assert(task is null, "A visual-loop task must have an owning cancellation source.");
            return;
        }

        Debug.Assert(task is not null, "A visual-loop cancellation source must have an observed task.");

        try
        {
            cts.Cancel();

            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task VisualLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_visualInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var application = Application;

            if (application is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _visualPulseQueued, 1, 0) != 0)
            {
                continue;
            }

            try
            {
                application.Dispatcher.Post(() =>
                {
                    try
                    {
                        if (!cancellationToken.IsCancellationRequested && !Disposed)
                        {
                            AdvanceVisuals();
                        }
                    }
                    finally
                    {
                        Volatile.Write(ref _visualPulseQueued, 0);
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                Volatile.Write(ref _visualPulseQueued, 0);
                return;
            }
            catch (InvalidOperationException)
            {
                Volatile.Write(ref _visualPulseQueued, 0);
            }
        }
    }

    /// <summary>Advances presentation state once without advancing the game simulation.</summary>
    /// <remarks>This method must run on the application dispatcher while the screen is attached.</remarks>
    /// <exception cref="InvalidOperationException">The attached screen is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The screen or an owned control is disposed.</exception>
    internal void AdvanceVisuals()
    {
        var deathComplete = _animation.Advance();
        _titlePanel.Phase = _animation.PrismPhase;
        _board.AnimationFrame = _animation.Frame;
        _board.DeathPulse = _animation.DeathPulse;
        _board.DeathVisibleSegments = _animation.VisibleDeathSegments(_state.Body.Count);
        _board.AdvanceEffects();
        AdvanceDemo();

        if (deathComplete && _phase == GamePhase.DeathAnimation)
        {
            OnDeathAnimationComplete();
        }
    }

    // The attract-mode snake plays itself on the visual clock: it recreates its game whenever the
    // board size changes or the demo runs out of lives, and it moves every second pulse (160 ms)
    // so the title screen stays lively without stealing attention from the menu cards.
    private void AdvanceDemo()
    {
        if (_phase is not (GamePhase.Title or GamePhase.HighScoreEntry or GamePhase.GameOver))
        {
            return;
        }

        var width = Math.Max(10, _board.Bounds.Width);
        var height = Math.Max(6, _board.Bounds.Height);

        if (_demoState is null || _demoState.IsGameOver || _demoState.Width != width || _demoState.Height != height)
        {
            _demoState = new GameState(width, height, difficulty: 1);
            _board.DemoState = _demoState;
        }

        _demoTickParity = !_demoTickParity;

        if (!_demoTickParity)
        {
            return;
        }

        _demoState.ChangeDirection(AttractPilot.ChooseDirection(_demoState));
        _ = _demoState.Tick();
    }

    private void TickGame()
    {
        var result = _state.Tick();
        TransitionTo(GamePhase.Playing);
        _board.RequestRedraw();

        if (result == TickResult.Ate && _state.LastEaten is { } eaten)
        {
            SpawnEatEffects(eaten.Position, eaten.Kind);
        }

        if (result == TickResult.Died)
        {
            TriggerDeathAnimation();
        }
    }

    // Every apple kind announces itself with a floating score label and a sparkle burst in its own
    // signature color, matching the palette the board already uses for that apple.
    private void SpawnEatEffects(Point position, AppleKind kind)
    {
        var (text, color) = kind switch
        {
            AppleKind.Normal => ("+10", Color.Rgb(120, 255, 120)),
            AppleKind.Golden => ("+50", Color.Rgb(255, 215, 0)),
            AppleKind.Poison => ("-5", Color.Rgb(220, 80, 220)),
            AppleKind.Speed => ("+20", Color.Rgb(0, 255, 255)),
            AppleKind.Life => ("+15 ♥", Color.Rgb(255, 90, 90)),
            _ => ("+?", Color.Default)
        };

        _board.AddScorePopup(new ScorePopup(position, text, color));
        _board.AddSparkleBurst(new SparkleBurst(position, color));
    }

    #endregion

    #region Input

    private void OnKey(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Handled || e.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        if ((e.Stroke.Modifiers & Modifiers.Control) != 0 &&
            e.Stroke is { Code: Code.Character, Character: { } character } &&
            Rune.ToLowerInvariant(character) == new Rune('q'))
        {
            Application?.Closed();
            e.Handled = true;
            return;
        }

        switch (_phase)
        {
            case GamePhase.Title:
                HandleTitleInput(e);
                break;
            case GamePhase.Playing:
                HandlePlayingInput(e);
                break;
            case GamePhase.Paused:
                HandlePausedInput(e);
                break;
            case GamePhase.HighScoreEntry:
                HandleHighScoreInput(e);
                break;
            case GamePhase.GameOver:
                HandleGameOverInput(e);
                break;
            case GamePhase.DeathAnimation:
                e.Handled = true;
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void HandleTitleInput(KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.Enter)
        {
            StartGame();
            e.Handled = true;
            return;
        }

        if (e.Stroke.Code != Code.Character || e.Stroke.Character is not { } character)
        {
            return;
        }

        var lower = Rune.ToLowerInvariant(character);

        if (lower == new Rune('q'))
        {
            Application?.Closed();
            e.Handled = true;
            return;
        }

        var selectedDifficulty = character.Value switch
        {
            '1' => 0,
            '2' => 1,
            '3' => 2,
            _ => -1
        };

        if (selectedDifficulty >= 0)
        {
            _selectedDifficulty = selectedDifficulty;
            TransitionTo(GamePhase.Title);
            e.Handled = true;
        }
    }

    private void HandlePlayingInput(KeyEventArgs e)
    {
        if (e.Stroke is { Code: Code.Character, Character: { } character } &&
            Rune.ToLowerInvariant(character) == new Rune('p'))
        {
            _state.IsPaused = true;
            StopGameLoop();
            TransitionTo(GamePhase.Paused);
            e.Handled = true;
        }
    }

    private void HandlePausedInput(KeyEventArgs e)
    {
        if (e.Stroke is { Code: Code.Character, Character: { } character } &&
            Rune.ToLowerInvariant(character) == new Rune('p'))
        {
            _state.IsPaused = false;
            TransitionTo(GamePhase.Playing);
            StartGameLoop();
            e.Handled = true;
        }
    }

    private void HandleHighScoreInput(KeyEventArgs e)
    {
        if (e.Stroke is { Code: Code.Character, Character: { } character } &&
            Rune.IsLetter(character) &&
            _initials.Count < 3)
        {
            _initials.Add(Rune.ToUpperInvariant(character));
            _titlePanel.ShowRecord(_state.Score, InitialsText());
            e.Handled = true;
            return;
        }

        if (e.Stroke.Code == Code.Backspace && _initials.Count > 0)
        {
            _initials.RemoveAt(_initials.Count - 1);
            _titlePanel.ShowRecord(_state.Score, InitialsText());
            e.Handled = true;
            return;
        }

        if (e.Stroke.Code == Code.Enter && _initials.Count == 3)
        {
            _ = _highScores.Insert(InitialsText(), _state.Score);
            TransitionTo(GamePhase.Title);
            e.Handled = true;
        }
    }

    private void HandleGameOverInput(KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.Enter)
        {
            TransitionTo(GamePhase.Title);
            e.Handled = true;
        }
    }

    private void OnDirectionChanged(object? sender, Direction direction)
    {
        _ = sender;

        if (_phase != GamePhase.Playing)
        {
            return;
        }

        _state.ChangeDirection(direction);
        TickGame();
    }

    private string InitialsText()
    {
        var initials = new StringBuilder();

        foreach (var initial in _initials)
        {
            _ = initials.Append(initial);
        }

        return initials.ToString();
    }

    #endregion
}
