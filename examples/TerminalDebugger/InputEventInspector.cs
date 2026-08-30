// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Displays a bounded event timeline and structured details for the selected event.</summary>
internal sealed class InputEventInspector: CompositeControlBase
{
    private readonly DiagnosticEventLog _log;
    private readonly ListView _list;
    private readonly Text _detail;
    private readonly Button _pause;
    private readonly Text _count;

    /// <summary>Initializes an inspector for one session log.</summary>
    /// <param name="log">The non-null owned session log.</param>
    internal InputEventInspector(DiagnosticEventLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
        _list = new ListView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ItemTemplate = item => new Text(TextMarkup.Escape(item?.ToString() ?? string.Empty))
            {
                Padding = new Thickness(1, 0),
                Overflow = Overflow.Ellipsis
            }
        };
        _list.SelectionChanged += OnSelectionChanged;
        _detail = new Text("Interact with the terminal to capture decoded input.")
        {
            Padding = new Thickness(1),
            Overflow = Overflow.Wrap
        };
        _pause = new Button("&Pause");
        _pause.Click += (_, _) => _log.IsPaused = !_log.IsPaused;
        var clear = new Button("&Clear");
        clear.Click += (_, _) => _log.Clear();
        _count = new Text("0 / 500 records") { VerticalAlignment = VerticalAlignment.Center };

        var toolbar = new Stack
        {
            Orientation = Orientation.Horizontal,
            Height = Length.Auto,
            Spacing = 1,
            Padding = new Thickness(1, 0),
            Children = { _pause, clear, _count }
        };
        var matrix = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ColumnSpacing = 1
        };
        matrix.Columns.Add(Track.Star(3));
        matrix.Columns.Add(Track.Star(2));
        Grid.SetColumn(_list, 0);
        var detailGroup = new GroupBox
        {
            HeaderText = "Decoded event",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = _detail
        };
        Grid.SetColumn(detailGroup, 1);
        matrix.Children.Add(_list);
        matrix.Children.Add(detailGroup);

        var root = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Dock.SetSide(toolbar, DockSide.Top);
        root.Children.Add(toolbar);
        root.Children.Add(matrix);
        InitializeContent(root);
        _log.Changed += OnLogChanged;
    }

    private void OnLogChanged(object? sender, EventArgs eventArgs)
    {
        var items = _log.Records.Reverse().Cast<object?>().ToArray();
        _list.Items = items;
        _count.Content = $"{items.Length} / 500 records";
        _pause.Text = _log.IsPaused ? "&Resume" : "&Pause";

        if (items.Length > 0)
        {
            _list.SelectedIndex = 0;
        }
        else
        {
            _detail.Content = "Interact with the terminal to capture decoded input.";
        }
    }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        if (_list.SelectedItem is not DiagnosticEventRecord record)
        {
            return;
        }

        var builder = new StringBuilder()
            .Append("<accent><b>").Append(record.Kind).Append(" #").Append(record.Sequence).Append("</b></accent>\n")
            .Append("Time: <info>").Append(record.Timestamp.ToString("HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)).Append("</info>\n")
            .Append("Summary: ").Append(TextMarkup.Escape(record.Summary)).Append("\n\n")
            .Append(TextMarkup.Escape(record.Explanation)).Append("\n\n");

        foreach (var field in record.Fields)
        {
            _ = builder.Append("<info>").Append(TextMarkup.Escape(field.Name)).Append(":</info> ")
                .Append(TextMarkup.Escape(field.Value)).Append('\n');
        }

        _detail.Content = builder.ToString();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            _log.Changed -= OnLogChanged;
        }

        base.OnUnavailable(reason);
    }
}
