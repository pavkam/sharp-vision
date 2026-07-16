// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

using SharpVision.Text;

/// <summary>Root screen for the Snake game with sidebar, status bar, and game loop.</summary>
public sealed class SnakeScreen: Screen
{
    private readonly SnakeBoard _board;
    private readonly Text _scoreText;
    private readonly Text _livesText;
    private readonly Text _levelText;
    private readonly Text _statusText;
    private readonly Text _highScoreText;
    private readonly GameState _state;
    private CancellationTokenSource? _cts;
    private int _highScore;

    /// <summary>Initializes the game layout.</summary>
    public SnakeScreen()
    {
        _state = new GameState(width: 40, height: 20, difficulty: 0);

        _board = new SnakeBoard { State = _state };
        _board.DirectionChanged += OnDirectionChanged;
        _board.RestartRequested += OnRestartRequested;
        _board.PauseToggled += OnPauseToggled;

        _scoreText = new Text("<b>Score</b>\n0") { Overflow = Overflow.Wrap };
        _livesText = new Text("<b>Lives</b>\n♥♥♥") { Overflow = Overflow.Wrap };
        _levelText = new Text("<b>Level</b>\nEasy") { Overflow = Overflow.Wrap };
        _highScoreText = new Text("<b>Best</b>\n0") { Overflow = Overflow.Wrap };
        _statusText = new Text("<d>Arrows/WASD · P pause · R restart · Q quit</d>") { Overflow = Overflow.Clip };

        var legendText = "<d>● normal\n◆ golden\n✦ poison\n★ speed\n♥ life</d>";

        var title = new Dock
        {
            Background = ThemeColors.Surface,
            FillMode = FillMode.Opaque,
            Height = Length.Cells(1),
            Padding = new Thickness(1, 0),
            Children = { new Text("<accent><b>🐍 SNAKE</b></accent>") },
        };

        var sidebar = new Stack
        {
            Width = Length.Cells(12),
            Padding = new Thickness(1, 0),
            Spacing = 1,
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderGlyphs = Glyphs.Light,
            BorderColor = ThemeColors.Border,
            Children =
            {
                _scoreText,
                _livesText,
                _levelText,
                _highScoreText,
                new Text(legendText) { Overflow = Overflow.Wrap },
            },
        };

        var statusBar = new Dock
        {
            Height = Length.Cells(1),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderGlyphs = Glyphs.Light,
            BorderColor = ThemeColors.Border,
            Padding = new Thickness(1, 0),
            Children = { _statusText },
        };

        var layout = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Dock.SetSide(title, Side.Top);
        Dock.SetSide(sidebar, Side.Right);
        Dock.SetSide(statusBar, Side.Bottom);
        layout.Children.Add(title);
        layout.Children.Add(sidebar);
        layout.Children.Add(statusBar);
        layout.Children.Add(_board);
        InitializeContent(layout);

        _ = AddHandler(Events.Key, OnGlobalKey);
    }

    /// <inheritdoc/>
    protected override void OnAttach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Theme = Themes.Dark;
    }

    /// <inheritdoc/>
    protected override void OnStarted(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _ = application.Focus.Focus(_board);
        StartGameLoop();
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _board.DirectionChanged -= OnDirectionChanged;
        _board.RestartRequested -= OnRestartRequested;
        _board.PauseToggled -= OnPauseToggled;
    }

    private void StartGameLoop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () => await GameLoopAsync(token), token);
    }

    private async Task GameLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_state.CurrentTickMs, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var dispatcher = Application?.Dispatcher;

            if (dispatcher is null)
            {
                return;
            }

            dispatcher.Post(() =>
            {
                if (IsDisposed)
                {
                    return;
                }

                var result = _state.Tick();
                UpdateUI(result);
                _board.RequestRedraw();
            });
        }
    }

    private void UpdateUI(TickResult result)
    {
        _ = result;
        _scoreText.Content = "<b>Score</b>\n" + _state.Score.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _livesText.Content = "<b>Lives</b>\n" + new string('♥', Math.Max(0, _state.Lives));
        _levelText.Content = "<b>Level</b>\n" + _state.DifficultyName;

        if (_state.Score > _highScore)
        {
            _highScore = _state.Score;
        }

        _highScoreText.Content = "<b>Best</b>\n" + _highScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void OnDirectionChanged(object? sender, Direction direction)
    {
        _ = sender;
        _state.ChangeDirection(direction);
    }

    private void OnRestartRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _state.Reset();
        _board.RequestRedraw();
        UpdateUI(TickResult.Moved);
    }

    private void OnPauseToggled(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _state.IsPaused = !_state.IsPaused;
        _board.RequestRedraw();
    }

    private void OnGlobalKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Preview || eventArgs.Handled)
        {
            return;
        }

        if (eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        if ((eventArgs.Stroke.Modifiers & Modifiers.Control) != 0 &&
            eventArgs.Stroke.Code == Code.Character &&
            eventArgs.Stroke.Character is { } c &&
            Rune.ToLowerInvariant(c) == new Rune('q'))
        {
            Application?.Closed();
            eventArgs.Handled = true;
        }
    }
}
