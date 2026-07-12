using System.Text;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Runtime;
using SharpVision.Scrolling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Runtime;

using Shouldly;

using ControlText = SharpVision.Controls.Text;
using ScrollRange = SharpVision.Scrolling.Range;
using TerminalOptions = SharpVision.Terminal.Runtime.Options;

namespace SharpVision.Showcase.Tests;

/// <summary>Proves real terminal input, focus, scrolling, and resize across the running gallery.</summary>
public sealed class GalleryInteractionTests
{
    /// <summary>Verifies the real gallery reaches its first frame and shuts down cleanly.</summary>
    [Fact]
    public async Task StartAsync_WhenGalleryRuns_RendersAndStopsCleanlyAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using var gallery = new Gallery();
        await using var application = new Application(
            gallery.Root,
            terminal,
            terminal,
            StartupOptions.Create(new Dictionary<string, string?>()));
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
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(80, 24), new Size(800, 480)));
        using var gallery = new Gallery();
        await using var application = new Application(
            gallery.Root,
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

        var main = gallery.Content.Parent.ShouldBeOfType<ScrollView>();
        var wheel = string.Concat(Enumerable.Repeat("\u001b[<65;30;10M", 8));
        terminal.QueueInput(Encoding.ASCII.GetBytes(wheel));
        await WaitUntilAsync(
            () => main.VerticalOffset > 0,
            application,
            "main page scrolling");

        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(16),
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
        gallery.Root.Bounds.ShouldBe(new Rect(0, 0, 100, 30));
        gallery.SelectedPage.ShouldBe("TextInput");
        application.Failure.ShouldBeNull();
        terminal.Writes.Count.ShouldBeGreaterThan(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a decoded primary SGR click drives visible navigation states and selection.</summary>
    [Fact]
    public async Task Input_WhenPrimaryPointerClicksSidebarButton_SelectsButtonPageAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using var gallery = new Gallery();
        await using var application = new Application(
            gallery.Root,
            terminal,
            terminal,
            StartupOptions.Create(new Dictionary<string, string?>()));
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
            button.Appearance.Background.ShouldBe(Palette.Pressed);
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
            button.Appearance.Background.ShouldBe(Palette.Highlight);
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies arrow navigation starts focused and selects the next sidebar page through decoded input.</summary>
    [Fact]
    public async Task Input_WhenArrowDownIsPressed_SelectsAndFocusesNextSidebarPageAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using var gallery = new Gallery();
        await using var application = new Application(
            gallery.Root,
            terminal,
            terminal,
            StartupOptions.Create(new Dictionary<string, string?>()));
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
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using var gallery = new Gallery();
        await using var application = new Application(
            gallery.Root,
            terminal,
            terminal,
            StartupOptions.Create(new Dictionary<string, string?>()));
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
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using var gallery = new Gallery();
        await using var application = new Application(
            gallery.Root,
            terminal,
            terminal,
            StartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(11),
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

        terminal.QueueInput(Encoding.ASCII.GetBytes(
            $"\u001b[<0;{start.X + 1};{start.Y + 1}M" +
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
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using var gallery = new Gallery();
        await using var application = new Application(
            gallery.Root,
            terminal,
            terminal,
            StartupOptions.Create(new Dictionary<string, string?>()));
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(5),
            TestContext.Current.CancellationToken);
        var editor = await application.Dispatcher.InvokeAsync(
            () => Find<TextInput>(gallery.Content, static value => value.Text == "SharpVision"),
            TestContext.Current.CancellationToken);
        var preview = await application.Dispatcher.InvokeAsync(
            () => Find<FigletText>(gallery.Content, static value => value.Content == "SharpVision"),
            TestContext.Current.CancellationToken);
        var picker = await application.Dispatcher.InvokeAsync(
            () => Find<Button>(
                gallery.Content,
                static value => value.Content is ControlText { Content: "Font: Standard ▼" }),
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
            () => Find<List>(
                gallery.Content,
                static value => value.EffectiveIsVisible) is not null,
            application,
            "FIGlet dropdown open");
        var fonts = await application.Dispatcher.InvokeAsync(
            () => Find<List>(
                gallery.Content,
                static value => value.EffectiveIsVisible),
            TestContext.Current.CancellationToken);
        var activeFonts = fonts.ShouldNotBeNull();
        await BringIntoViewAsync(activeFonts, gallery, application);
        var fontPoint = await application.Dispatcher.InvokeAsync(
            () => new Point(activeFonts.Bounds.X + 1, activeFonts.Bounds.Y),
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.ASCII.GetBytes(
            $"\u001b[<0;{fontPoint.X + 1};{fontPoint.Y + 1}M" +
            $"\u001b[<0;{fontPoint.X + 1};{fontPoint.Y + 1}m"));
        await WaitUntilAsync(
            () => activePreview.Font.Name != "Standard",
            application,
            "FIGlet font selection");
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
        var frame = NextFrame(application);
        await application.Dispatcher.InvokeAsync(() =>
        {
            var main = gallery.Content.Parent.ShouldBeOfType<ScrollView>();
            main.BringIntoView(control).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
        await frame.WaitAsync(TestContext.Current.CancellationToken);
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
