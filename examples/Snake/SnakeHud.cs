// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

using System.Globalization;

using SharpVision.Text;

/// <summary>Displays the retained two-row game metrics, status, and keyboard guidance.</summary>
internal sealed class SnakeHud: CompositeControlBase
{
    private readonly ProgressBar _boostMeter;
    private readonly Text _guidance;
    private readonly Text _metrics;
    private readonly Text _quit;
    private readonly Text _status;

    /// <summary>Initializes an opaque two-cell HUD with a permanently reserved quit shortcut.</summary>
    internal SnakeHud()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Height = Length.Cells(2);
        Face = HudFace();

        _metrics = new Text("") { HorizontalAlignment = HorizontalAlignment.Stretch, Overflow = Overflow.Clip };
        _status = new Text("") { Overflow = Overflow.Clip };

        // The boost meter drains in sub-cell steps beside the status label while a speed boost is
        // active; it collapses to zero width the rest of the time.
        _boostMeter = new ProgressBar
        {
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            UseSubCellResolution = true,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(1, 0)
        };

        var metricsRow = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1),
            Spacing = 0
        };
        Dock.SetSide(_status, DockSide.Right);
        metricsRow.Children.Add(_status);
        Dock.SetSide(_boostMeter, DockSide.Right);
        metricsRow.Children.Add(_boostMeter);
        metricsRow.Children.Add(_metrics);

        _guidance = new Text("") { HorizontalAlignment = HorizontalAlignment.Stretch, Overflow = Overflow.Clip };
        _quit = new Text("<error><b>CTRL+Q  QUIT</b></error>") { Overflow = Overflow.Clip };

        var controlsRow = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1),
            Spacing = 0
        };
        Dock.SetSide(_quit, DockSide.Right);
        controlsRow.Children.Add(_quit);
        controlsRow.Children.Add(_guidance);

        var root = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(2),
            Orientation = Orientation.Vertical,
            Spacing = 0,
            Padding = new Thickness(1, 0),
            Face = HudFace(),
            Children = { metricsRow, controlsRow }
        };

        InitializeContent(root);
        UpdateTitle("Easy");
    }

    private static Face HudFace() => new(
        ThemeColor.ControlText,
        Color.Rgb(0x30, 0x30, 0x3A),
        ThemeDecoration.NormalText,
        Underline.None,
        Color.Default);

    /// <summary>Shows current gameplay metrics, boost status, and movement guidance.</summary>
    /// <param name="state">The non-null game state whose current values are displayed.</param>
    /// <param name="bestScore">The non-negative best score displayed with six digits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bestScore"/> is negative.</exception>
    internal void UpdateGame(GameState state, int bestScore)
    {
        ValidateGameArguments(state, bestScore);
        UpdateMetrics(state, bestScore);
        UpdateBoostMeter(state);
        _status.Content = state.IsSpeedBoosted ? "<info><b>⚡ BOOST</b></info>" : "<d>READY</d>";
        _guidance.Content = "<d>ARROWS / WASD</d>  MOVE   <d>P</d>  PAUSE";
    }

    /// <summary>Shows current gameplay metrics with paused status and resume guidance.</summary>
    /// <param name="state">The non-null game state whose current values are displayed.</param>
    /// <param name="bestScore">The non-negative best score displayed with six digits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bestScore"/> is negative.</exception>
    internal void UpdatePaused(GameState state, int bestScore)
    {
        ValidateGameArguments(state, bestScore);
        UpdateMetrics(state, bestScore);
        UpdateBoostMeter(state);
        _status.Content = "<warning><b>PAUSED</b></warning>";
        _guidance.Content = "<d>ARROWS / WASD</d>  MOVE   <d>P</d>  RESUME";
    }

    /// <summary>Shows the title-phase difficulty and start controls while retaining the exit hint.</summary>
    /// <param name="difficulty">The non-empty selected difficulty name.</param>
    /// <exception cref="ArgumentException"><paramref name="difficulty"/> is null, empty, or whitespace.</exception>
    internal void UpdateTitle(string difficulty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(difficulty);

        var escapedDifficulty = Text.Escape(difficulty.ToUpperInvariant());
        _metrics.Content = $"<accent><b>SNAKE</b></accent>  <b>{escapedDifficulty}</b>";
        _status.Content = "<d>READY</d>";
        _boostMeter.Visibility = Visibility.Collapsed;
        _guidance.Content = "<accent><b>ENTER</b></accent>  START   <d>1 / 2 / 3</d>  DIFFICULTY";
    }

    private void UpdateBoostMeter(GameState state)
    {
        _boostMeter.Visibility = state.IsSpeedBoosted ? Visibility.Visible : Visibility.Collapsed;
        _boostMeter.Value = state.BoostFraction;
    }

    private void UpdateMetrics(GameState state, int bestScore)
    {
        var score = state.Score.ToString("D6", CultureInfo.InvariantCulture);
        var best = bestScore.ToString("D6", CultureInfo.InvariantCulture);
        var lives = new string('♥', Math.Max(0, state.Lives));
        var difficulty = Text.Escape(state.DifficultyName.ToUpperInvariant());

        _metrics.Content =
            $"<b>SCORE</b> <accent>{score}</accent>  " +
            $"<b>LIVES</b> <error>{lives}</error>  " +
            $"<b>{difficulty}</b>  " +
            $"<b>BEST</b> {best}";
    }

    private static void ValidateGameArguments(GameState state, int bestScore)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(bestScore);
    }
}
