// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Composes a professional editor workspace around a live application status bar.</summary>
internal sealed class StatusBarWorkspace: CompositeControl
{
    /// <summary>Initializes a bordered editor with pointer, activity, document, and environment status.</summary>
    internal StatusBarWorkspace()
    {
        Width = Length.Cells(72);
        Height = Length.Cells(11);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        PointerStatus = new Text("Mouse --,--") { Overflow = Overflow.Clip };
        ActivityStatus = new Text("Indexing")
        {
            Width = Length.Cells(8),
            Overflow = Overflow.Clip
        };
        var activity = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children =
            {
                new Spinner { Interval = TimeSpan.FromMilliseconds(120) },
                ActivityStatus
            }
        };
        AutoSave = new CheckBox
        {
            Height = Length.Cells(1),
            Padding = default,
            Content = new Text("&Autosave") { Overflow = Overflow.Clip }
        };
        AutoSave.StateChanged += (_, _) =>
            ActivityStatus.Content = AutoSave.IsChecked == true ? "Saved" : "Modified";

        Bar = new StatusBar
        {
            Padding = new Thickness(1, 0)
        };
        Bar.Items.Add(new StatusBarItem { Content = activity });
        Bar.Items.Add(new StatusBarItem
        {
            LeftSeparator = StatusBarSeparatorGlyphs.Chevron,
            Content = new Text("main") { Overflow = Overflow.Clip }
        });
        Bar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = AutoSave
        });
        Bar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            LeftSeparator = StatusBarSeparatorGlyphs.Bar,
            Content = PointerStatus
        });
        Bar.Items.Add(Item(
            "Ln 42, Col 8",
            StatusBarItemAlignment.Right,
            StatusBarSeparatorGlyphs.Bar));
        Bar.Items.Add(Item(
            "UTF-8",
            StatusBarItemAlignment.Right,
            StatusBarSeparatorGlyphs.Bullet));
        Bar.Items.Add(Item(
            "LF",
            StatusBarItemAlignment.Right,
            StatusBarSeparatorGlyphs.Diamond));

        var header = new Dock
        {
            Height = Length.Cells(1),
            Padding = new Thickness(1, 0),
            Children =
            {
                new Text("<accent><b>SharpEdit</b></accent>  <d>src/EditorScreen.cs</d>")
                {
                    Overflow = Overflow.Clip
                }
            }
        };
        EditorSurface = new Dock
        {
            Padding = new Thickness(1, 0),
            Children =
            {
                new Text(
                    "<d>40</d>  public sealed class <info>EditorScreen</info>: Screen\n" +
                    "<d>41</d>  {\n" +
                    "<d>42</d>      private readonly <accent>StatusBar</accent> _status;\n" +
                    "<d>43</d>      private readonly TextInput _editor;\n" +
                    "<d>44</d>  }")
                {
                    Overflow = Overflow.Clip
                }
            }
        };
        _ = EditorSurface.AddHandler(Events.Pointer, OnEditorPointer);

        Dock.SetSide(header, Side.Top);
        Dock.SetSide(Bar, Side.Bottom);
        var root = new Dock
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children = { header, Bar, EditorSurface }
        };
        InitializeContent(root);
    }

    /// <summary>Gets the live application status bar at the workspace's bottom edge.</summary>
    internal StatusBar Bar { get; }

    /// <summary>Gets the interactive autosave toggle retained by one right-aligned status item.</summary>
    internal CheckBox AutoSave { get; }

    /// <summary>Gets the leading activity text updated by the autosave toggle.</summary>
    internal Text ActivityStatus { get; }

    /// <summary>Gets the pointer-aware editor viewport above the status bar.</summary>
    internal Dock EditorSurface { get; }

    /// <summary>Gets the retained text item updated from routed editor pointer input.</summary>
    internal Text PointerStatus { get; }

    private void OnEditorPointer(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble ||
            eventArgs.Pointer.Action is not (PointerAction.Move or PointerAction.Press) ||
            eventArgs.LocalCells is not { } cells)
        {
            return;
        }

        PointerStatus.Content = FormattableString.Invariant($"Mouse {cells.X:D2},{cells.Y:D2}");
    }

    private static StatusBarItem Item(
        string content,
        StatusBarItemAlignment alignment = StatusBarItemAlignment.Left,
        Rune? leftSeparator = null) => new()
        {
            Alignment = alignment,
            LeftSeparator = leftSeparator,
            Content = new Text(content) { Overflow = Overflow.Clip }
        };
}
