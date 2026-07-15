// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;


using SharpVision.Scrolling;


using ScrollRange = Scrolling.Range;

/// <summary>Proves real terminal input, focus, scrolling, and resize across the running gallery.</summary>
public sealed class GalleryInteractionTests
{
    /// <summary>Verifies the real gallery reaches its first frame and shuts down cleanly.</summary>
    [Fact]
    public async Task StartAsync_WhenGalleryRuns_RendersAndStopsCleanlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        application.Size.ShouldBe(new Size(80, 24));
        terminal.Writes.Count.ShouldBeGreaterThan(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies navigation, activation, editing, scrolling, and resize through Application.</summary>
    [Fact]
    public async Task Input_WhenGalleryRuns_UpdatesLiveControlsAndSurvivesResizeAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(80, 24), new Size(800, 480)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes(
            "\u001b[<0;3;8M\u001b[<0;3;8m"));
        await WaitUntilAsync(
            () => gallery.SelectedPage == "Button",
            application,
            "Button page selection");

        var button = await application.Dispatcher.InvokeAsync(
            () => Find<Button>(gallery.Content, static value => value.IsEnabled),
            TestContext.Current.CancellationToken);
        var activeButton = button.ShouldNotBeNull();
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(activeButton).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        terminal.QueueInput("\r"u8);
        await WaitUntilAsync(
            () => Find<ControlText>(
                gallery.Content,
                static value => value.Content.StartsWith("Activation log: Keyboard", StringComparison.Ordinal)) is not null,
            application,
            "Button keyboard activation");

        var main = gallery.Content.Parent.ShouldBeOfType<Stack>();
        var wheel = string.Concat(Enumerable.Repeat("\u001b[<65;30;10M", 8));
        terminal.QueueInput(Encoding.ASCII.GetBytes(wheel));
        await WaitUntilAsync(
            () => main.VerticalOffset > 0,
            application,
            "main page scrolling");

        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "TextInput")),
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(
            () => gallery.SelectedPage == "TextInput",
            application,
            "TextInput page selection");
        var input = await application.Dispatcher.InvokeAsync(
            () => Find<TextInput>(gallery.Content, static value => !value.IsReadOnly),
            TestContext.Current.CancellationToken);
        var activeInput = input.ShouldNotBeNull();
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(activeInput).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        terminal.QueueInput("X"u8);
        await WaitUntilAsync(
            () => activeInput.Text.EndsWith('X'),
            application,
            "TextInput editing");

        var rendered = NextFrame(application);
        terminal.QueueResize(new Dimensions(new Size(100, 30), new Size(1000, 600)));
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        gallery.Bounds.ShouldBe(new Rect(0, 0, 100, 30));
        gallery.SelectedPage.ShouldBe("TextInput");
        application.Failure.ShouldBeNull();
        terminal.Writes.Count.ShouldBeGreaterThan(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies decoded terminal wheel input scrolls the showcased multiline editor before its documentation viewport.</summary>
    [Fact]
    public async Task Input_WhenWheelTargetsShowcaseMultilineEditor_ScrollsEditorBeforePageAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 40)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "TextInput")),
            TestContext.Current.CancellationToken);
        var editor = await application.Dispatcher.InvokeAsync(
            () => Find<TextInput>(
                gallery.Content,
                static value => value.AcceptsReturn && value.Height == Length.Cells(3)),
            TestContext.Current.CancellationToken);
        var activeEditor = editor.ShouldNotBeNull();
        await BringIntoViewAsync(activeEditor, gallery, application);
        var target = await application.Dispatcher.InvokeAsync(
            () => new Point(activeEditor.Bounds.X + 1, activeEditor.Bounds.Y + 1),
            TestContext.Current.CancellationToken);
        var main = gallery.Content.Parent.ShouldBeOfType<Stack>();
        var previousPageOffset = await application.Dispatcher.InvokeAsync(
            () => main.VerticalOffset,
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<65;{target.X};{target.Y}M"));
        await WaitUntilAsync(
            () => activeEditor.VerticalOffset > 0,
            application,
            "showcase multiline editor wheel scrolling");

        await application.Dispatcher.InvokeAsync(
            () => main.VerticalOffset.ShouldBe(previousPageOffset),
            TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the Text sample appends markup and records the activation path in its visible activity log.</summary>
    [Fact]
    public async Task Input_WhenTextMarkupButtonIsActivated_UpdatesActivityLogAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 40)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "Text")),
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(
            () => gallery.SelectedPage == "Text",
            application,
            "Text page selection");

        var append = await application.Dispatcher.InvokeAsync(
            () => Find<Button>(
                gallery.Content,
                static value => value.Content is ControlText { Content: "Append markup" }),
            TestContext.Current.CancellationToken);
        var activeAppend = append.ShouldNotBeNull();
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(activeAppend).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        terminal.QueueInput("\r"u8);
        await WaitUntilAsync(
            () => Find<ControlText>(
                gallery.Content,
                static value => value.Content.StartsWith("Activity log: Keyboard", StringComparison.Ordinal)) is not null,
            application,
            "Text markup mutation");

        application.Failure.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the List example reports a changed selection through its public selection API.</summary>
    [Fact]
    public async Task Input_WhenListSelectionChanges_UpdatesSelectionStatusAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 40)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "List")),
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(
            () => gallery.SelectedPage == "List",
            application,
            "List page selection");

        var active = await application.Dispatcher.InvokeAsync(
            () => Find<List>(
                gallery.Content,
                static value => value.IsEnabled),
            TestContext.Current.CancellationToken);
        var selected = active.ShouldNotBeNull();
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                selected.SelectedIndex = 2;
            },
            TestContext.Current.CancellationToken);

        await WaitUntilAsync(
            () => Find<ControlText>(
                gallery.Content,
                static value => value.Content.StartsWith("Selected item: Gamma", StringComparison.Ordinal)) is not null,
            application,
            "List selection status");

        application.Failure.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a decoded primary SGR click drives visible navigation states and selection.</summary>
    [Fact]
    public async Task Input_WhenPrimaryPointerClicksSidebarButton_SelectsButtonPageAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var button = gallery.Navigation[1];
        var point = await application.Dispatcher.InvokeAsync(
            () => button.Bounds,
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes(
            $"\u001b[<0;{point.X + 1};{point.Y + 1}M"));
        await WaitUntilAsync(
            () => button.IsPressed,
            application,
            "dashboard Button pressed state");

        await application.Dispatcher.InvokeAsync(() =>
        {
            button.IsHovered.ShouldBeTrue();
            button.IsFocused.ShouldBeTrue();
            button.IsPressed.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes(
            $"\u001b[<0;{point.X + 1};{point.Y + 1}m"));
        await WaitUntilAsync(
            () => gallery.SelectedPage == "Button",
            application,
            "dashboard Button page selection");

        await application.Dispatcher.InvokeAsync(() =>
        {
            button.IsSelected.ShouldBeTrue();
            button.Background.ShouldBe(Color.Indexed(4));
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies passive SGR motion visibly hovers a sidebar entry and clears on terminal leave.</summary>
    [Fact]
    public async Task Input_WhenPointerMovesOverSidebarEntry_UsesAndClearsHoverAppearanceAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var entry = gallery.Navigation[1];
        var point = await application.Dispatcher.InvokeAsync(
            () => entry.Bounds,
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<35;{point.X + 1};{point.Y + 1}M"));
        await WaitUntilAsync(
            () => entry.IsHovered,
            application,
            "sidebar passive hover");

        await application.Dispatcher.InvokeAsync(() =>
            entry.Foreground.ShouldBe(Color.Indexed(14)),
            TestContext.Current.CancellationToken);

        terminal.QueueInput("\u001b[<35;0;0M"u8);
        await WaitUntilAsync(
            () => !entry.IsHovered,
            application,
            "sidebar passive hover leave");

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies passive motion over sample Button content applies the showcase hover appearance.</summary>
    [Fact]
    public async Task Input_WhenPointerMovesOverButtonContent_UsesHoverAppearanceAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(80, 60)));
        var root = new ButtonPane();
        await using Application application = new(
            root,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var button = await application.Dispatcher.InvokeAsync(
            () => Find<Button>(root, static value => value.IsEnabled),
            TestContext.Current.CancellationToken);
        var active = button.ShouldNotBeNull();
        var point = await application.Dispatcher.InvokeAsync(
            () => active.Bounds,
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<35;{point.X + 1};{point.Y + 1}M"));
        await WaitUntilAsync(
            () => active.IsHovered,
            application,
            "sample button passive hover");

        await application.Dispatcher.InvokeAsync(() =>
            active.Foreground.ShouldBe(Color.Indexed(14)),
            TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a primary click leaves the showcased Button focused with its focused appearance.</summary>
    [Fact]
    public async Task Input_WhenPrimaryPointerClicksButton_LeavesFocusedAppearanceAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(80, 60)));
        var root = new ButtonPane();
        await using Application application = new(
            root,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var button = await application.Dispatcher.InvokeAsync(
            () => Find<Button>(root, static value => value.IsEnabled),
            TestContext.Current.CancellationToken);
        var active = button.ShouldNotBeNull();
        var point = await application.Dispatcher.InvokeAsync(
            () => new Point(active.Bounds.X + 1, active.Bounds.Y),
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes(
            $"\u001b[<0;{point.X + 1};{point.Y + 1}M" +
            $"\u001b[<0;{point.X + 1};{point.Y + 1}m"));
        await WaitUntilAsync(
            () => active.IsFocused,
            application,
            "button focused appearance");

        await application.Dispatcher.InvokeAsync(() =>
        {
            active.IsFocused.ShouldBeTrue();
            active.Attributes.ShouldBe(Attributes.Underline);
            active.Background.ShouldBe(Color.Indexed(0));

            if (active.IsHovered)
            {
                active.Foreground.ShouldBe(Color.Indexed(14));
            }
            else
            {
                active.Foreground.ShouldBe(Color.Indexed(15));
            }
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies arrow navigation starts focused and selects the next sidebar page through decoded input.</summary>
    [Fact]
    public async Task Input_WhenArrowDownIsPressed_SelectsAndFocusesNextSidebarPageAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        application.Started += FocusSelected;
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.QueueInput("\u001b[B"u8);
        await WaitUntilAsync(
            () => gallery.SelectedPage == "Button",
            application,
            "sidebar arrow navigation");

        await application.Dispatcher.InvokeAsync(() =>
        {
            gallery.Navigation[1].IsFocused.ShouldBeTrue();
            gallery.Navigation[1].IsSelected.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);

        void FocusSelected(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            gallery.FocusSelected(application.Focus).ShouldBeTrue();
        }
    }

    /// <summary>Verifies Enter activates the currently focused sidebar entry through the ordinary pressable path.</summary>
    [Fact]
    public async Task Input_WhenEnterIsPressedOnFocusedSidebarEntry_SelectsThatPageAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(gallery.Navigation[1]).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        terminal.QueueInput("\r"u8);
        await WaitUntilAsync(
            () => gallery.SelectedPage == "Button",
            application,
            "sidebar Enter activation");

        await application.Dispatcher.InvokeAsync(
            () => gallery.Navigation[1].IsFocused.ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies raw captured pointer drag moves the showcased horizontal scrollbar thumb and its live value label.</summary>
    [Fact]
    public async Task Input_WhenShowcaseScrollBarThumbIsDragged_UpdatesValueAndStatusAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "ScrollBar")),
            TestContext.Current.CancellationToken);
        var scrollBar = await application.Dispatcher.InvokeAsync(
            () => Find<ScrollBar>(gallery.Content, static value => value.Orientation == Orientation.Horizontal),
            TestContext.Current.CancellationToken);
        var activeScrollBar = scrollBar.ShouldNotBeNull();
        await BringIntoViewAsync(activeScrollBar, gallery, application);
        var start = await application.Dispatcher.InvokeAsync(() =>
        {
            var trackLength = activeScrollBar.Bounds.Width - 2;
            var thumb = Thumb.Resolve(
                new ScrollRange(
                    activeScrollBar.Minimum,
                    activeScrollBar.Maximum,
                    activeScrollBar.Value,
                    activeScrollBar.ViewportSize),
                trackLength);
            return new Point(activeScrollBar.Bounds.X + 1 + thumb.Start, activeScrollBar.Bounds.Y);
        }, TestContext.Current.CancellationToken);
        var end = await application.Dispatcher.InvokeAsync(
            () => new Point(activeScrollBar.Bounds.X + activeScrollBar.Bounds.Width - 2, activeScrollBar.Bounds.Y),
            TestContext.Current.CancellationToken);
        var middle = new Point((start.X + end.X) / 2, start.Y);
        var initial = await application.Dispatcher.InvokeAsync(
            () => activeScrollBar.Value,
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<0;{start.X + 1};{start.Y + 1}M"));
        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<32;{middle.X + 1};{middle.Y + 1}M"));
        await WaitUntilAsync(
            () => activeScrollBar.Value > initial && activeScrollBar.Value < activeScrollBar.Maximum,
            application,
            "showcase scrollbar intermediate drag value");
        terminal.QueueInput(Encoding.ASCII.GetBytes(
            $"\u001b[<32;{end.X + 1};{end.Y + 1}M" +
            $"\u001b[<0;{end.X + 1};{end.Y + 1}m"));
        await WaitUntilAsync(
            () => activeScrollBar.Value == activeScrollBar.Maximum,
            application,
            "showcase scrollbar thumb drag");
        await WaitUntilAsync(
            () => Find<ControlText>(
                gallery.Content,
                static value => value.Content == "Thumb value: 100") is not null,
            application,
            "showcase scrollbar value status");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the showcased FIGlet editor updates text and selects a catalog font through its dropdown list.</summary>
    [Fact]
    public async Task Input_WhenFigletEditorTextAndFontChange_UpdatesPreviewAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "FigletText")),
            TestContext.Current.CancellationToken);
        var editor = await application.Dispatcher.InvokeAsync(
            () => Find<TextInput>(gallery.Content, static value => value.Text == "SharpVision"),
            TestContext.Current.CancellationToken);
        var preview = await application.Dispatcher.InvokeAsync(
            () => Find<FigletText>(gallery.Content, static value => value.Content == "SharpVision"),
            TestContext.Current.CancellationToken);
        var picker = await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(
                gallery.Content,
                static value => value.SelectedIndex >= 0 && value.Items[value.SelectedIndex] is "Standard"),
            TestContext.Current.CancellationToken);
        var activeEditor = editor.ShouldNotBeNull();
        var activePreview = preview.ShouldNotBeNull();
        var activePicker = picker.ShouldNotBeNull();
        await BringIntoViewAsync(activeEditor, gallery, application);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(activeEditor).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        terminal.QueueInput("!"u8);
        await WaitUntilAsync(
            () => activePreview.Content.EndsWith('!'),
            application,
            "FIGlet text editing");
        await BringIntoViewAsync(activePicker, gallery, application);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(activePicker).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        terminal.QueueInput("\r"u8);
        await WaitUntilAsync(
            () => Find<ComboBox>(
                gallery.Content,
                static value => value.IsOpen) is not null,
            application,
            "FIGlet dropdown open");
        var fonts = await application.Dispatcher.InvokeAsync(
            () => Find<List>(
                gallery.Content,
                static value => value.EffectiveIsVisible),
            TestContext.Current.CancellationToken);
        var activeFonts = fonts.ShouldNotBeNull();
        var invoked = false;
        await application.Dispatcher.InvokeAsync(
            () => activeFonts.ItemInvoked += (_, _) => invoked = true,
            TestContext.Current.CancellationToken);
        await BringIntoViewAsync(activeFonts, gallery, application);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                activeFonts.VerticalOffset.ShouldBe(0);
                activeFonts.Bounds.Height.ShouldBeGreaterThan(1);
            },
            TestContext.Current.CancellationToken);
        var fontPoint = await application.Dispatcher.InvokeAsync(
            () => new Point(activeFonts.Bounds.X + 1, activeFonts.Bounds.Y + 1),
            TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                _ = activeFonts.HitTest(fontPoint).ShouldBeOfType<ListItem>();
                _ = application.Capture.Root.HitTest(fontPoint).ShouldBeOfType<ListItem>();
            },
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<0;{fontPoint.X + 1};{fontPoint.Y + 1}M"));
        await WaitUntilAsync(
            () => application.Capture.Captured is ListItem,
            application,
            "FIGlet font pointer press");
        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<0;{fontPoint.X + 1};{fontPoint.Y + 1}m"));
        await WaitUntilAsync(
            () => invoked,
            application,
            "FIGlet font item invocation");
        await WaitUntilAsync(
            () => !activePicker.IsOpen,
            application,
            "FIGlet dropdown dismissal");
        await application.Dispatcher.InvokeAsync(
            () => activePicker.SelectedIndex.ShouldNotBe(0),
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the FIGlet popup picker accepts keyboard selection after its trigger opens it.</summary>
    [Fact]
    public async Task Input_WhenFigletPickerOpens_KeyboardSelectionChangesPreviewAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "FigletText")),
            TestContext.Current.CancellationToken);
        var picker = await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(
                gallery.Content,
                static value => value.SelectedIndex >= 0 && value.Items[value.SelectedIndex] is "Standard"),
            TestContext.Current.CancellationToken);
        var preview = await application.Dispatcher.InvokeAsync(
            () => Find<FigletText>(gallery.Content, static value => value.Content == "SharpVision"),
            TestContext.Current.CancellationToken);
        var activePicker = picker.ShouldNotBeNull();
        var activePreview = preview.ShouldNotBeNull();
        await BringIntoViewAsync(activePicker, gallery, application);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(activePicker).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        terminal.QueueInput("\r\u001b[B\r"u8);
        await WaitUntilAsync(
            () => activePreview.Font.Name != "Standard",
            application,
            "FIGlet keyboard font selection");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static T? Find<T>(Control control, Func<T, bool> predicate) where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(predicate);

        if (control is T match && predicate(match))
        {
            return match;
        }

        if (control is not Container container)
        {
            return null;
        }

        foreach (var child in container.Children)
        {
            if (Find(child, predicate) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static int IndexOf(Gallery gallery, string page)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentException.ThrowIfNullOrWhiteSpace(page);
        var index = gallery.Pages.Select(static value => value).ToList().IndexOf(page);
        return index >= 0 ? index : throw new InvalidOperationException($"The {page} page is not registered.");
    }

    private static Task NextFrame(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += Complete;
        return completion.Task;

        void Complete(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            application.FrameRendered -= Complete;
            _ = completion.TrySetResult();
        }
    }

    private static async Task BringIntoViewAsync(
        Control control,
        Gallery gallery,
        Application application)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentNullException.ThrowIfNull(application);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += Complete;
        var moved = await application.Dispatcher.InvokeAsync(() =>
        {
            var main = gallery.Content.Parent.ShouldBeOfType<Stack>();
            return main.BringIntoView(control);
        }, TestContext.Current.CancellationToken);

        if (moved)
        {
            await completion.Task.WaitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            application.FrameRendered -= Complete;
        }

        return;

        void Complete(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            application.FrameRendered -= Complete;
            _ = completion.TrySetResult();
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        Application application,
        string operation)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(application);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        for (var attempt = 0; attempt < 5_000; attempt++)
        {
            if (await application.Dispatcher.InvokeAsync(
                predicate,
                TestContext.Current.CancellationToken))
            {
                return;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(1),
                TestContext.Current.CancellationToken);
        }

        (await application.Dispatcher.InvokeAsync(
            predicate,
            TestContext.Current.CancellationToken)).ShouldBeTrue(
            $"Timed out waiting for {operation}.");
    }
}
