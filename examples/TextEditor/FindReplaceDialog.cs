// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TextEditor;

using System.Globalization;

/// <summary>Manages find and replace operations against a target TextInput.</summary>
internal sealed class FindReplaceDialog: IDisposable
{
    private readonly TextInput _target;
    private readonly TextInput _findBox;
    private readonly TextInput _replaceBox;
    private readonly Text _statusText;
    private readonly Control[] _replaceControls;
    private int _lastMatchIndex;

    /// <summary>Initializes the find/replace dialog for a target editor.</summary>
    internal FindReplaceDialog(TextInput target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;

        _findBox = new TextInput { Placeholder = "Search text" };
        _findBox.Submitted += (_, _) => FindNext();

        _replaceBox = new TextInput { Placeholder = "Replace with" };
        _replaceBox.Submitted += (_, _) => ReplaceNext();

        _statusText = new Text("<d>Enter a search term</d>")
        {
            Overflow = Overflow.Clip,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        };

        var findButton = CreateActionButton("Find Next");
        findButton.Click += (_, _) => FindNext();

        var replaceButton = CreateActionButton("Replace");
        replaceButton.Click += (_, _) => ReplaceNext();

        var replaceAllButton = CreateActionButton("Replace All");
        replaceAllButton.Click += (_, _) => ReplaceAll();

        var closeButton = CreateActionButton("Close");
        closeButton.IsCancel = true;
        closeButton.Click += (_, _) => Close();

        var findLabel = CreateLabel("Find:");
        var replaceLabel = CreateLabel("Replace:");
        _replaceControls = [replaceLabel, _replaceBox, replaceButton, replaceAllButton];
        var actions = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { findButton, replaceButton, replaceAllButton, closeButton }
        };
        var actionBar = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        actionBar.Columns.Add(Track.Star(1));
        actionBar.Columns.Add(Track.Auto());
        Grid.SetColumn(actions, 1);
        actionBar.Children.Add(actions);

        var form = new Grid
        {
            ColumnSpacing = 1
        };
        form.Columns.Add(Track.Auto());
        form.Columns.Add(Track.Star(1, minimum: 12));
        form.Rows.Add(Track.Auto());
        form.Rows.Add(Track.Auto());
        Add(form, findLabel, row: 0, column: 0);
        Add(form, _findBox, row: 0, column: 1);
        Add(form, replaceLabel, row: 1, column: 0);
        Add(form, _replaceBox, row: 1, column: 1);

        var content = new Stack
        {
            Margin = new Thickness(1, 0),
            Children = { form, _statusText, actionBar }
        };

        Window = new Window
        {
            Header = "Find",
            CanMove = true,
            CanClose = true,
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
            Width = Length.Percent(60),
            MinWidth = 50,
            MaxWidth = 58,
            Height = Length.Auto,
            Right = Length.Cells(2),
            Top = Length.Cells(2),
            Visibility = Visibility.Collapsed,
            Content = content
        };

        Window.Closing += (_, _) => Close();
    }

    /// <summary>Gets the dialog Window control to add to the layout.</summary>
    internal Window Window { get; }

    /// <summary>Gets whether the dialog is currently visible.</summary>
    internal bool IsOpen => Window.Visibility == Visibility.Visible;

    /// <summary>Disposes the dialog's owned controls.</summary>
    public void Dispose() => Window.Dispose();

    /// <summary>Opens the dialog in find mode.</summary>
    internal void OpenFind()
    {
        Window.Header = "Find";
        SetReplaceVisibility(Visibility.Collapsed);
        _lastMatchIndex = _target.CaretIndex;

        if (_target.SelectionLength > 0)
        {
            _findBox.Text = _target.SelectedText;
        }

        _statusText.Content = "<d>Enter a search term</d>";
        Show();
    }

    /// <summary>Opens the dialog in find-and-replace mode.</summary>
    internal void OpenReplace()
    {
        Window.Header = "Find and Replace";
        SetReplaceVisibility(Visibility.Visible);
        _lastMatchIndex = _target.CaretIndex;

        if (_target.SelectionLength > 0)
        {
            _findBox.Text = _target.SelectedText;
        }

        _statusText.Content = "<d>Enter a search term</d>";
        Show();
    }

    /// <summary>Closes and hides the dialog.</summary>
    internal void Close() => Window.Visibility = Visibility.Collapsed;

    private static Text CreateLabel(string content) => new(content)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = Alignment.End
    };

    private static Button CreateActionButton(string label) => new()
    {
        Content = new Text(label)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = Alignment.Center
        }
    };

    private static void Add(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private void SetReplaceVisibility(Visibility visibility)
    {
        foreach (var control in _replaceControls)
        {
            control.Visibility = visibility;
        }
    }

    private void Show()
    {
        if (IsOpen)
        {
            _ = _findBox.Focus();
            return;
        }

        Window.Visibility = Visibility.Visible;
        _ = _findBox.Focus();
    }

    private void FindNext()
    {
        var term = _findBox.Text;

        if (string.IsNullOrEmpty(term))
        {
            _statusText.Content = "<warning>Enter a search term</warning>";
            return;
        }

        var text = _target.Text;
        var start = Math.Min(_lastMatchIndex, text.Length);
        var index = text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);

        if (index < 0 && start > 0)
        {
            index = text.IndexOf(term, 0, StringComparison.OrdinalIgnoreCase);
        }

        if (index >= 0)
        {
            _target.Select(index, term.Length);
            _lastMatchIndex = index + term.Length;
            var total = CountMatches(text, term);
            _statusText.Content =
                $"<accent>Found</accent> at position {index.ToString(CultureInfo.InvariantCulture)} ({total.ToString(CultureInfo.InvariantCulture)} total)";
        }
        else
        {
            _statusText.Content = "<error>No matches found</error>";
            _lastMatchIndex = 0;
        }
    }

    private void ReplaceNext()
    {
        var term = _findBox.Text;
        var replacement = _replaceBox.Text;

        if (string.IsNullOrEmpty(term))
        {
            _statusText.Content = "<warning>Enter a search term</warning>";
            return;
        }

        if (_target.SelectedText.Equals(term, StringComparison.OrdinalIgnoreCase))
        {
            var start = _target.SelectionStart;
            _target.Text = _target.Text.Remove(start, term.Length).Insert(start, replacement);
            _target.Select(start, replacement.Length);
            _lastMatchIndex = start + replacement.Length;
            _statusText.Content = "<accent>Replaced</accent>";
        }

        FindNext();
    }

    private void ReplaceAll()
    {
        var term = _findBox.Text;
        var replacement = _replaceBox.Text;

        if (string.IsNullOrEmpty(term))
        {
            _statusText.Content = "<warning>Enter a search term</warning>";
            return;
        }

        var text = _target.Text;
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            text = text.Remove(index, term.Length).Insert(index, replacement);
            index += replacement.Length;
            count++;
        }

        if (count > 0)
        {
            _target.Text = text;
            _statusText.Content =
                $"<accent>Replaced {count.ToString(CultureInfo.InvariantCulture)}</accent> occurrences";
        }
        else
        {
            _statusText.Content = "<error>No matches found</error>";
        }
    }

    private static int CountMatches(string text, string term)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += term.Length;
        }

        return count;
    }
}
