// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

using System.Globalization;

using SharpVision.Fonts;
using SharpVision.Text;

/// <summary>Owns the retained title, record-entry, and game-over presentation for the Snake screen.</summary>
/// <remarks>
/// All three phase views and their descendants are created once. Phase changes update retained text and
/// visibility only, preserving control identity, layout ownership, and the animated Prism instances.
/// </remarks>
internal sealed class SnakeTitlePanel: CompositeControl
{
    private static readonly FigletFont _smallFont = FigletCatalog.Default.Load("Small");
    private static readonly FigletFont _standardFont = FigletCatalog.Default.Load("Standard");

    private readonly Text _difficulty;
    private readonly Text _finalScore;
    private readonly Stack _gameOverView;
    private readonly Text _initials;
    private readonly Stack _recordView;
    private readonly Prism _recordPrism;
    private readonly Text _recordScore;
    private readonly Text _scores;
    private readonly Stack _titleView;
    private readonly Prism _titlePrism;

    /// <summary>Initializes all phase views and their retained presentation children exactly once.</summary>
    internal SnakeTitlePanel()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _titlePrism = CreatePrismTitle(_smallFont, "SNAKE");
        _difficulty = new Text("") { HorizontalAlignment = HorizontalAlignment.Center };
        _scores = new Text("") { Overflow = Overflow.Wrap };

        var actionCard = CreateCard(
            new Text("<accent><b>PLAY</b></accent>") { HorizontalAlignment = HorizontalAlignment.Center },
            new Text("<accent><b>ENTER</b></accent>  START"),
            new Text("<accent><b>1 / 2 / 3</b></accent>  DIFFICULTY"),
            new Text("<accent><b>Q</b></accent>  QUIT"),
            _difficulty);
        var scoresCard = CreateCard(
            new Text("<accent><b>HIGH SCORES</b></accent>") { HorizontalAlignment = HorizontalAlignment.Center },
            _scores);
        var cards = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 2 };
        cards.Columns.Add(Track.Star(1, minimum: 20));
        cards.Columns.Add(Track.Star(1, minimum: 20));
        Grid.SetColumn(scoresCard, 1);
        cards.Children.Add(actionCard);
        cards.Children.Add(scoresCard);

        _titleView = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1,
            Children =
            {
                _titlePrism,
                new Text("<d>A SHARPVISION ARCADE</d>") { HorizontalAlignment = HorizontalAlignment.Center },
                cards
            }
        };

        _recordPrism = CreatePrismTitle(_standardFont, "NEW RECORD");
        _recordScore = new Text("") { HorizontalAlignment = HorizontalAlignment.Center };
        _initials = new Text("") { HorizontalAlignment = HorizontalAlignment.Center };
        _recordView = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Spacing = 1,
            Children =
            {
                _recordPrism,
                CreateCard(
                    new Text("<accent><b>NEW HIGH SCORE</b></accent>")
                    {
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    _recordScore,
                    _initials,
                    new Text("<d>TYPE 3 LETTERS, THEN ENTER</d>") { HorizontalAlignment = HorizontalAlignment.Center })
            }
        };

        _finalScore = new Text("") { HorizontalAlignment = HorizontalAlignment.Center };
        _gameOverView = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Spacing = 1,
            Children =
            {
                new FigletText(_standardFont)
                {
                    Content = "GAME OVER",
                    Face = new Face(
                        Color.Rgb(0xFF, 0x00, 0x00),
                        Color.Transparent,
                        TerminalAttributes.Bold,
                        Underline.None,
                        Color.Rgb(0xFF, 0x00, 0x00)),
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                CreateCard(
                    _finalScore,
                    new Text("<accent><b>ENTER</b></accent>  CONTINUE")
                    {
                        HorizontalAlignment = HorizontalAlignment.Center
                    })
            }
        };

        var root = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { _titleView, _recordView, _gameOverView }
        };

        InitializeContent(root);
    }

    /// <summary>Gets or sets the normalized caller-driven phase shared by both rainbow headings.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is non-finite or outside [0, 1).</exception>
    /// <exception cref="InvalidOperationException">The attached panel is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The panel is disposed.</exception>
    internal double Phase
    {
        get => _titlePrism.Phase;
        set
        {
            if (!double.IsFinite(value) || value < 0d || value >= 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value,
                    "The title phase must be finite and from zero up to, but not including, one.");
            }

            _titlePrism.Phase = value;
            _recordPrism.Phase = value;
        }
    }

    /// <summary>Shows the title view with the selected difficulty and validated high-score table.</summary>
    /// <param name="difficulty">The non-empty selected difficulty name.</param>
    /// <param name="scores">The non-null ordered score entries to display.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="difficulty"/> or an entry name is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="difficulty"/>, <paramref name="scores"/>, or an entry name is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">An entry score is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached panel is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The panel is disposed.</exception>
    internal void ShowTitle(string difficulty, IReadOnlyList<(string Name, int Score)> scores)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(difficulty);
        ArgumentNullException.ThrowIfNull(scores);

        for (var index = 0; index < scores.Count; index++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scores[index].Name);
            ArgumentOutOfRangeException.ThrowIfNegative(scores[index].Score);
        }

        var scoreLines = new StringBuilder();

        for (var index = 0; index < scores.Count; index++)
        {
            if (index > 0)
            {
                _ = scoreLines.Append('\n');
            }

            var entry = scores[index];
            var rank = (index + 1).ToString("D2", CultureInfo.InvariantCulture);
            var score = entry.Score.ToString("D6", CultureInfo.InvariantCulture);
            _ = scoreLines.Append(CultureInfo.InvariantCulture,
                $"<d>{rank}.</d> <accent>{Text.Escape(entry.Name)}</accent>  {score}");
        }

        var escapedDifficulty = Text.Escape(difficulty.ToUpperInvariant());
        _difficulty.Content = $"<d>SELECTED</d>  <b>{escapedDifficulty}</b>";
        _scores.Content = scores.Count == 0 ? "<d>NO SCORES YET</d>" : scoreLines.ToString();
        ShowOnly(_titleView);
    }

    /// <summary>Shows live record entry with a non-negative score and zero to three entered Unicode letters.</summary>
    /// <param name="score">The non-negative record score.</param>
    /// <param name="initials">Zero to three Unicode letters entered so far.</param>
    /// <exception cref="ArgumentNullException"><paramref name="initials"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="initials"/> contains a non-letter Unicode scalar.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="score"/> is negative or <paramref name="initials"/> contains more than three Unicode scalars.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached panel is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The panel is disposed.</exception>
    internal void ShowRecord(int score, string initials)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        ArgumentNullException.ThrowIfNull(initials);

        var initialCount = 0;

        foreach (var initial in initials.EnumerateRunes())
        {
            if (!Rune.IsLetter(initial))
            {
                throw new ArgumentException("Initials may contain Unicode letters only.", nameof(initials));
            }

            initialCount++;
        }

        if (initialCount > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initials), initials, "Initials may contain at most three Unicode letters.");
        }

        var formattedScore = score.ToString("D6", CultureInfo.InvariantCulture);
        var paddedInitials = new StringBuilder();

        foreach (var initial in initials.EnumerateRunes())
        {
            _ = paddedInitials.Append(Rune.ToUpperInvariant(initial));
        }

        for (var slot = initialCount; slot < 3; slot++)
        {
            _ = paddedInitials.Append('_');
        }

        _recordScore.Content = $"<b>SCORE</b>  <accent>{formattedScore}</accent>";
        _initials.Content = $"<b>{Text.Escape(paddedInitials.ToString())}</b>";
        ShowOnly(_recordView);
    }

    /// <summary>Shows the game-over view with the final non-negative score.</summary>
    /// <param name="score">The non-negative final score.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="score"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached panel is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The panel is disposed.</exception>
    internal void ShowGameOver(int score)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(score);

        var formattedScore = score.ToString("D6", CultureInfo.InvariantCulture);
        _finalScore.Content = $"<b>FINAL SCORE</b>  <error>{formattedScore}</error>";
        ShowOnly(_gameOverView);
    }

    private static Dock CreateCard(params Control[] children)
    {
        var content = new Stack { HorizontalAlignment = HorizontalAlignment.Stretch, Spacing = 0 };

        foreach (var child in children)
        {
            content.Children.Add(child);
        }

        var card = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Shadow = new Shadow(
                true,
                ShadowMode.Composite,
                new Point(1, 1),
                default,
                ThemeColor.ControlShadow,
                Color.Transparent,
                ThemeDecoration.Shadow),
            Padding = new Thickness(1, 0)
        };
        card.Children.Add(content);
        return card;
    }

    private static Prism CreatePrismTitle(FigletFont font, string content)
    {
        return new Prism
        {
            Direction = PrismDirection.Diagonal,
            CycleLength = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            Content = new FigletText(font)
            {
                Content = content,
                Face = new Face(
                    ThemeColor.ControlText,
                    Color.Transparent,
                    TerminalAttributes.Bold,
                    Underline.None,
                    Color.Default),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private void ShowOnly(Stack visible)
    {
        _titleView.Visibility = ReferenceEquals(visible, _titleView)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _recordView.Visibility = ReferenceEquals(visible, _recordView)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _gameOverView.Visibility = ReferenceEquals(visible, _gameOverView)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
