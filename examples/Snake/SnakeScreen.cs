// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

using System.Globalization;

using SharpVision.Fonts;
using SharpVision.Text;

/// <summary>Root screen managing title, gameplay, death animation, and high-score entry.</summary>
public sealed class SnakeScreen: Screen
{
    private static readonly FigletFont _titleFont = FigletCatalog.Default.Load("Small");
    private static readonly FigletFont _deathFont = FigletCatalog.Default.Load("Standard");

    private readonly SnakeBoard _board;
    private readonly HighScoreTable _highScores = new();
    private readonly Text _topBar;
    private readonly FigletText _figlet;
    private readonly Text _difficultyLabel;
    private readonly Text _scoresText;
    private readonly Text _initialsText;
    private readonly Stack _titleOverlay;
    private readonly Dock _menuBox;
    private readonly Dock _scoresBox;
    private readonly Dock _topBarDock;
    private GameState _state;
    private CancellationTokenSource? _cts;
    private GamePhase _phase = GamePhase.Title;
    private string _initials = "";
    private int _selectedDifficulty;

    /// <summary>Initializes the full game layout.</summary>
    public SnakeScreen()
    {
        _state = new GameState(width: 40, height: 20, difficulty: 0);

        _board = new SnakeBoard { State = _state };
        _board.DirectionChanged += OnDirectionChanged;

        _figlet = new FigletText(_titleFont)
        {
            Content = "SNAKE",
            Foreground = Color.Rgb(0, 255, 100),
            Attributes = TerminalAttributes.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        _difficultyLabel = new Text("<accent>1/2/3</accent> Difficulty: <b>Easy</b>");
        _menuBox = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = Length.Cells(30),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            BorderColor = ThemeColors.Border,
            Padding = new Thickness(1, 0),
            Children =
            {
                new Stack
                {
                    Spacing = 0,
                    Children =
                    {
                        new Text("<accent>ENTER</accent> Start game"),
                        _difficultyLabel,
                        new Text("<accent>  Q  </accent> Quit"),
                    },
                },
            },
        };

        _scoresText = new Text("") { Overflow = Overflow.Wrap };
        _initialsText = new Text("") { HorizontalAlignment = HorizontalAlignment.Center };
        _scoresBox = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = Length.Cells(30),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Light,
            BorderColor = ThemeColors.Border,
            Padding = new Thickness(1, 0),
            Children =
            {
                new Stack
                {
                    Children =
                    {
                        new Text("<b>HIGH SCORES</b>") { HorizontalAlignment = HorizontalAlignment.Center },
                        _scoresText,
                    },
                },
            },
        };

        _titleOverlay = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1,
            Children = { _figlet, _menuBox, _scoresBox },
        };

        _topBar = new Text("") { Overflow = Overflow.Clip };
        _topBarDock = new Dock
        {
            Background = ThemeColors.Surface,
            FillMode = FillMode.Opaque,
            Height = Length.Cells(1),
            Padding = new Thickness(1, 0),
            Children = { _topBar },
        };

        var gameArea = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        gameArea.Children.Add(_board);
        Overlay.SetZIndex(_titleOverlay, 10);
        gameArea.Children.Add(_titleOverlay);

        var layout = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Dock.SetSide(_topBarDock, Side.Top);
        layout.Children.Add(_topBarDock);
        layout.Children.Add(gameArea);
        InitializeContent(layout);

        _ = AddHandler(Events.Key, OnKey);
        UpdateTitleScreen();
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
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _board.DirectionChanged -= OnDirectionChanged;
    }

    #region Phase transitions

    private void UpdateTitleScreen()
    {
        _phase = GamePhase.Title;
        _board.ShowBoard = false;
        _titleOverlay.Visibility = Visibility.Visible;

        _topBar.Content = "<accent><b>🐍 SNAKE</b></accent>  <d>A SharpVision showcase game</d>";

        var diffName = _selectedDifficulty switch { 0 => "Easy", 1 => "Medium", _ => "Hard" };
        _difficultyLabel.Content = $"<accent>1/2/3</accent> Difficulty: <b>{diffName}</b>";

        var sb = new System.Text.StringBuilder();

        for (var i = 0; i < _highScores.Entries.Count; i++)
        {
            var (name, score) = _highScores.Entries[i];
            var rank = (i + 1).ToString(CultureInfo.InvariantCulture);

            if (i > 0)
            {
                _ = sb.Append('\n');
            }

            _ = sb.Append(CultureInfo.InvariantCulture, $"<d>{rank,2}.</d> <accent>{name}</accent> {score.ToString(CultureInfo.InvariantCulture),5}");
        }

        _scoresText.Content = sb.ToString();
        _board.RequestRedraw();
    }

    private void StartGame()
    {
        _phase = GamePhase.Playing;
        var playWidth = Math.Max(10, _board.Bounds.Width - 2);
        var playHeight = Math.Max(6, _board.Bounds.Height - 2);
        _state = new GameState(width: playWidth, height: playHeight, difficulty: _selectedDifficulty);
        _board.State = _state;
        _board.ShowBoard = true;
        _titleOverlay.Visibility = Visibility.Collapsed;
        UpdateTopBar();
        _board.RequestRedraw();
        StartGameLoop();
    }

    private void TriggerDeathAnimation()
    {
        _phase = GamePhase.DeathAnimation;
        StopGameLoop();

        _board.DeathFlashActive = true;
        _board.RequestRedraw();

        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = Task.Run(async () =>
        {
            for (var frame = 0; frame < 6; frame++)
            {
                try { await Task.Delay(120, cts.Token); }
                catch (OperationCanceledException) { return; }

                Application?.Dispatcher.Post(() =>
                {
                    if (IsDisposed)
                    {
                        return;
                    }
                    _board.DeathFlashFrame = frame;
                    _board.RequestRedraw();
                });
            }

            try { await Task.Delay(400, cts.Token); }
            catch (OperationCanceledException) { return; }

            Application?.Dispatcher.Post(() =>
            {
                if (IsDisposed)
                {
                    return;
                }

                _board.DeathFlashActive = false;
                OnDeathAnimationComplete();
            });
        }, cts.Token);
    }

    private void OnDeathAnimationComplete()
    {
        if (_state.IsGameOver)
        {
            if (_highScores.Qualifies(_state.Score))
            {
                EnterHighScorePhase();
            }
            else
            {
                ShowGameOverPhase();
            }
        }
        else
        {
            _phase = GamePhase.Playing;
            UpdateTopBar();
            _board.RequestRedraw();
            StartGameLoop();
        }
    }

    private void EnterHighScorePhase()
    {
        _phase = GamePhase.HighScoreEntry;
        _initials = "";
        _board.ShowBoard = false;
        _titleOverlay.Visibility = Visibility.Visible;

        _figlet.Content = "NEW RECORD";
        _figlet.Foreground = Color.Rgb(255, 215, 0);
        _figlet.Font = _deathFont;

        UpdateInitialsDisplay();
        _titleOverlay.Children.Clear();
        _titleOverlay.Children.Add(_figlet);
        _titleOverlay.Children.Add(BuildInitialsBox());
        _board.RequestRedraw();
    }

    private Dock BuildInitialsBox()
    {
        var display = _initials.PadRight(3, '_');
        _initialsText.Content =
            $"<b>Score: <accent>{_state.Score.ToString(CultureInfo.InvariantCulture)}</accent></b>\n\n" +
            $"Enter your initials: <b><accent>{display[0]} {display[1]} {display[2]}</accent></b>\n\n" +
            "<d>Type 3 letters, then press ENTER</d>";
        return new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = Length.Cells(36),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            BorderColor = Color.Rgb(255, 215, 0),
            Padding = new Thickness(1, 0),
            Children = { _initialsText },
        };
    }

    private void UpdateInitialsDisplay()
    {
        var display = _initials.PadRight(3, '_');
        _initialsText.Content =
            $"<b>Score: <accent>{_state.Score.ToString(CultureInfo.InvariantCulture)}</accent></b>\n\n" +
            $"Enter your initials: <b><accent>{display[0]} {display[1]} {display[2]}</accent></b>\n\n" +
            "<d>Type 3 letters, then press ENTER</d>";
    }

    private void ShowGameOverPhase()
    {
        _phase = GamePhase.GameOver;
        _board.ShowBoard = false;
        _titleOverlay.Visibility = Visibility.Visible;

        _figlet.Content = "GAME OVER";
        _figlet.Foreground = Color.Rgb(255, 60, 60);
        _figlet.Font = _deathFont;

        var gameOverBox = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = Length.Cells(30),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            BorderColor = Color.Rgb(255, 60, 60),
            Padding = new Thickness(1, 0),
            Children =
            {
                new Stack
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new Text($"<b>Final Score: <accent>{_state.Score.ToString(CultureInfo.InvariantCulture)}</accent></b>")
                        {
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                        new Text("<d>Press ENTER to continue</d>")
                        {
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                    },
                },
            },
        };

        _titleOverlay.Children.Clear();
        _titleOverlay.Children.Add(_figlet);
        _titleOverlay.Children.Add(gameOverBox);
        _board.RequestRedraw();
    }

    #endregion

    #region Game loop

    private void StartGameLoop()
    {
        StopGameLoop();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = Task.Run(async () => await GameLoopAsync(cts.Token), cts.Token);
    }

    private void StopGameLoop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task GameLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(_state.CurrentTickMs, ct); }
            catch (OperationCanceledException) { return; }

            Application?.Dispatcher.Post(() =>
            {
                if (IsDisposed || _phase != GamePhase.Playing)
                {
                    return;
                }

                var result = _state.Tick();
                UpdateTopBar();
                _board.RequestRedraw();

                if (result == TickResult.Died)
                {
                    TriggerDeathAnimation();
                }
            });
        }
    }

    private void UpdateTopBar()
    {
        var lives = new string('♥', Math.Max(0, _state.Lives));
        var speed = _state.IsSpeedBoosted ? " <cyan><b>⚡BOOST</b></cyan>" : "";
        _topBar.Content =
            $"<b>SCORE</b> <accent>{_state.Score.ToString(CultureInfo.InvariantCulture),-6}</accent> " +
            $"<b>LIVES</b> <red>{lives}</red> " +
            $"<b>LEVEL</b> {_state.DifficultyName,-6} " +
            $"<b>BEST</b> {BestScore().ToString(CultureInfo.InvariantCulture)}" +
            speed;
    }

    private int BestScore()
    {
        var best = _state.Score;

        if (_highScores.Entries.Count > 0 && _highScores.Entries[0].Score > best)
        {
            best = _highScores.Entries[0].Score;
        }

        return best;
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
        }
        else if (e.Stroke.Code == Code.Character)
        {
            var ch = e.Stroke.Character;

            if (ch == new Rune('q') || ch == new Rune('Q'))
            {
                Application?.Closed();
                e.Handled = true;
            }
            else if (ch == new Rune('1'))
            {
                _selectedDifficulty = 0;
                UpdateTitleScreen();
                e.Handled = true;
            }
            else if (ch == new Rune('2'))
            {
                _selectedDifficulty = 1;
                UpdateTitleScreen();
                e.Handled = true;
            }
            else if (ch == new Rune('3'))
            {
                _selectedDifficulty = 2;
                UpdateTitleScreen();
                e.Handled = true;
            }
        }
    }

    private void HandlePlayingInput(KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.Character)
        {
            var ch = e.Stroke.Character;

            if (ch == new Rune('p') || ch == new Rune('P'))
            {
                _phase = GamePhase.Paused;
                _state.IsPaused = true;
                StopGameLoop();
                _board.ShowPaused = true;
                _board.RequestRedraw();
                e.Handled = true;
                return;
            }
        }

        if ((e.Stroke.Modifiers & Modifiers.Control) != 0 &&
            e.Stroke.Code == Code.Character &&
            e.Stroke.Character is { } c &&
            Rune.ToLowerInvariant(c) == new Rune('q'))
        {
            Application?.Closed();
            e.Handled = true;
        }
    }

    private void HandlePausedInput(KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.Character)
        {
            var ch = e.Stroke.Character;

            if (ch == new Rune('p') || ch == new Rune('P'))
            {
                _phase = GamePhase.Playing;
                _state.IsPaused = false;
                _board.ShowPaused = false;
                _board.RequestRedraw();
                StartGameLoop();
                e.Handled = true;
            }
        }
    }

    private void HandleHighScoreInput(KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.Character && e.Stroke.Character is { } ch)
        {
            if (Rune.IsLetter(ch) && _initials.Length < 3)
            {
                _initials += Rune.ToUpperInvariant(ch).ToString();
                UpdateInitialsDisplay();
                _board.RequestRedraw();
                e.Handled = true;
            }
        }
        else if (e.Stroke.Code == Code.Backspace && _initials.Length > 0)
        {
            _initials = _initials[..^1];
            UpdateInitialsDisplay();
            _board.RequestRedraw();
            e.Handled = true;
        }
        else if (e.Stroke.Code == Code.Enter && _initials.Length > 0)
        {
            _ = _highScores.Insert(_initials, _state.Score);
            _figlet.Font = _titleFont;
            _figlet.Content = "SNAKE";
            _figlet.Foreground = Color.Rgb(0, 255, 100);
            _titleOverlay.Children.Clear();
            _titleOverlay.Children.Add(_figlet);
            _titleOverlay.Children.Add(_menuBox);
            _titleOverlay.Children.Add(_scoresBox);
            UpdateTitleScreen();
            e.Handled = true;
        }
    }

    private void HandleGameOverInput(KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.Enter)
        {
            _figlet.Font = _titleFont;
            _figlet.Content = "SNAKE";
            _figlet.Foreground = Color.Rgb(0, 255, 100);
            _titleOverlay.Children.Clear();
            _titleOverlay.Children.Add(_figlet);
            _titleOverlay.Children.Add(_menuBox);
            _titleOverlay.Children.Add(_scoresBox);
            UpdateTitleScreen();
            e.Handled = true;
        }
    }

    private void OnDirectionChanged(object? sender, Direction direction)
    {
        _ = sender;

        if (_phase == GamePhase.Playing)
        {
            _state.ChangeDirection(direction);
        }
    }

    #endregion
}
