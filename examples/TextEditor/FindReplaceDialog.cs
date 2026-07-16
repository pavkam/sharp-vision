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
    private readonly Stack _replaceRow;
    private int _lastMatchIndex;

    /// <summary>Initializes the find/replace dialog for a target editor.</summary>
    internal FindReplaceDialog(TextInput target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;

        _findBox = new TextInput { Width = Length.Cells(28), Placeholder = "Search text" };
        _findBox.Submitted += (_, _) => FindNext();

        _replaceBox = new TextInput { Width = Length.Cells(28), Placeholder = "Replace with" };
        _replaceBox.Submitted += (_, _) => ReplaceNext();

        _statusText = new Text("<d>Enter a search term</d>") { Overflow = Overflow.Clip };

        var findButton = new Button { Content = new Text("Find") };
        findButton.Click += (_, _) => FindNext();

        var replaceButton = new Button { Content = new Text("Replace") };
        replaceButton.Click += (_, _) => ReplaceNext();

        var replaceAllButton = new Button { Content = new Text("All") };
        replaceAllButton.Click += (_, _) => ReplaceAll();

        var closeButton = new Button { Content = new Text("Close"), IsCancel = true };
        closeButton.Click += (_, _) => Close();

        var findRow = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { new Text("Find:   "), _findBox, findButton },
        };

        _replaceRow = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { new Text("Replace:"), _replaceBox, replaceButton, replaceAllButton },
        };

        var buttonRow = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { closeButton },
        };

        var content = new Stack
        {
            Spacing = 1,
            Children = { findRow, _replaceRow, _statusText, buttonRow },
        };

        Window = new Window
        {
            Title = "Find",
            CanMove = true,
            CanClose = true,
            HasShadow = true,
            ShadowMode = ShadowMode.Composite,
            ShadowOffset = new Point(1, 1),
            Width = Length.Cells(50),
            Height = Length.Auto,
            Visibility = Visibility.Collapsed,
            Content = content,
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
        Window.Title = "Find";
        _replaceRow.Visibility = Visibility.Collapsed;
        Window.Visibility = Visibility.Visible;
        _lastMatchIndex = _target.CaretIndex;

        if (_target.SelectionLength > 0)
        {
            _findBox.Text = _target.SelectedText;
        }

        _statusText.Content = "<d>Enter a search term</d>";
    }

    /// <summary>Opens the dialog in find-and-replace mode.</summary>
    internal void OpenReplace()
    {
        Window.Title = "Find and Replace";
        _replaceRow.Visibility = Visibility.Visible;
        Window.Visibility = Visibility.Visible;
        _lastMatchIndex = _target.CaretIndex;

        if (_target.SelectionLength > 0)
        {
            _findBox.Text = _target.SelectedText;
        }

        _statusText.Content = "<d>Enter a search term</d>";
    }

    /// <summary>Closes and hides the dialog.</summary>
    internal void Close() => Window.Visibility = Visibility.Collapsed;

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
            _statusText.Content = $"<accent>Found</accent> at position {index.ToString(CultureInfo.InvariantCulture)} ({total.ToString(CultureInfo.InvariantCulture)} total)";
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
            _statusText.Content = $"<accent>Replaced {count.ToString(CultureInfo.InvariantCulture)}</accent> occurrences";
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
