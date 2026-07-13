using System.Text;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using ControlText = SharpVision.Controls.Text;
using KeyAction = SharpVision.Terminal.Input.Action;

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Button ownership, command ordering, activation, layout, and cells.</summary>
public sealed class ButtonTests
{
    /// <summary>Verifies documented defaults and capacity-one content ownership.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var button = new Button();

        button.Content.ShouldBeNull();
        button.Command.ShouldBeNull();
        button.CommandParameter.ShouldBeNull();
        button.IsDefault.ShouldBeFalse();
        button.IsCancel.ShouldBeFalse();
        button.CanFocus.ShouldBeTrue();
        button.Padding.ShouldBe(new Thickness(1));
        button.Glyphs.ShouldBe(Glyphs.Rounded);
        button.HasShadow.ShouldBeTrue();
        button.ShadowOffset.ShouldBe(new Point(1, 1));
        button.Children.Add(new ProbeControl());
        _ = Should.Throw<InvalidOperationException>(() =>
            button.Children.Add(new ProbeControl()));
    }

    /// <summary>Verifies the default Button draws its own border and shadow without showcase wrappers.</summary>
    [Fact]
    public void Render_WhenDefaultStyleIsUsed_DrawsBorderAndShadow()
    {
        var button = new Button { Content = new ControlText("Apply") };
        var size = new Size(9, 5);
        button.Width = Length.Cells(size.Width);
        button.Height = Length.Cells(size.Height);
        new Engine().Layout(button, size);
        using var frame = new Frame(new Size(10, 6));

        button.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(8, 4)).ShouldBe("╯");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("A");
        frame.GetCell(new Point(9, 4)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies a Button can opt into the visible Turbo Vision block shadow mode.</summary>
    [Fact]
    public void Render_WhenBlockShadowIsSelected_DrawsConfiguredShadowGlyphOutsideTheBody()
    {
        var button = new Button
        {
            Content = new ControlText("Apply"),
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowGlyph = new Rune('▓'),
            Width = Length.Cells(9),
            Height = Length.Cells(5),
        };
        var size = new Size(9, 5);
        new Engine().Layout(button, size);
        using var frame = new Frame(new Size(10, 6));

        button.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(9, 4)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(8, 4)).ShouldBe("╯");
    }

    /// <summary>Verifies a held Button shifts its complete face over its own shadow without styling that shadow as hovered or pressed.</summary>
    [Fact]
    public void Render_WhenPressed_MovesFaceIntoShadow()
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Content = new ControlText("Go"),
        };
        var size = new Size(10, 6);
        new Engine().Layout(button, size);
        using var released = new Frame(size);
        button.Render(released.Canvas);

        Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
        using var pressed = new Frame(size);
        button.Render(pressed.Canvas);

        button.IsPressed.ShouldBeTrue();
        FrameOracle.Get(released, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(pressed, new Point(0, 0)).ShouldBeEmpty();
        FrameOracle.Get(pressed, new Point(1, 1)).ShouldBe("╭");
        pressed.GetCell(new Point(6, 1)).Style.Attributes.ShouldNotBe(Attributes.Dim);
    }

    /// <summary>Verifies a shadowless Button keeps its position while Space resolves the pressed face appearance.</summary>
    [Fact]
    public void Render_WhenPressedWithoutShadow_UsesPressedAppearanceWithoutTranslation()
    {
        var style = ThemeTestSupport.OverlayStyle<Button>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(255), background: Color.Indexed(240))),
            (State.Pressed, new ThemeOverlay(foreground: Color.Indexed(255), background: Color.Indexed(24))));
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            HasShadow = false,
            Style = style,
            Content = new ControlText("Go"),
        };
        var size = new Size(10, 6);
        new Engine().Layout(button, size);

        Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
        using var frame = new Frame(size);
        button.Render(frame.Canvas);

        button.IsPressed.ShouldBeTrue();
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        frame.GetCell(new Point(0, 0)).Style.Background.ShouldBe(Color.Indexed(24));
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("G");
        FrameOracle.Get(frame, new Point(5, 2)).ShouldBe("╯");
        FrameOracle.Get(frame, new Point(6, 3)).ShouldBeEmpty();
    }

    /// <summary>Verifies hover appearance brightens the interactive face while the detached shadow retains its normal dim treatment.</summary>
    [Fact]
    public void Render_WhenHovered_StylesFrameButNotShadow()
    {
        var style = ThemeTestSupport.OverlayStyle<Button>(
            (State.Normal, new ThemeOverlay(attributes: Attributes.None)),
            (State.Hovered, new ThemeOverlay(attributes: Attributes.Bold)));
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Style = style,
        };
        new Engine().Layout(button, new Size(10, 6));
        button.SetHovered(true);
        using var frame = new Frame(new Size(10, 6));

        button.Render(frame.Canvas);

        frame.GetCell(new Point(0, 0)).Style.Attributes.ShouldBe(Attributes.Bold);
        frame.GetCell(new Point(6, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies invalid replacement preserves the previous child and its parent.</summary>
    [Fact]
    public void Content_WhenReplacementIsOwned_ThrowsBeforeMutation()
    {
        var button = new Button();
        var previous = new ProbeControl();
        var owner = new Overlay();
        var invalid = new ProbeControl();
        button.Content = previous;
        owner.Children.Add(invalid);

        _ = Should.Throw<ArgumentException>(() => button.Content = invalid);

        button.Content.ShouldBeSameAs(previous);
        previous.Parent.ShouldBeSameAs(button);
        invalid.Parent.ShouldBeSameAs(owner);
    }

    /// <summary>Verifies Click observes released state and precedes command execution.</summary>
    [Fact]
    public void PerformClick_WhenCommandCanExecute_RaisesThenExecutesExactlyOnce()
    {
        var order = new List<string>();
        var parameter = new object();
        var command = new ProbeCommand { Executing = _ => order.Add("command") };
        var button = new Button { Command = command, CommandParameter = parameter };
        button.Click += (_, eventArgs) =>
        {
            button.IsPressed.ShouldBeFalse();
            eventArgs.Cause.ShouldBe(ActivationCause.Programmatic);
            order.Add("click");
        };

        button.PerformClick();

        order.ShouldBe(["click", "command"]);
        command.Queries.ShouldBe([parameter]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies false CanExecute suppresses both Click and execution.</summary>
    [Fact]
    public void PerformClick_WhenCommandCannotExecute_DoesNothing()
    {
        var command = new ProbeCommand { CanExecuteValue = false };
        var button = new Button { Command = command };
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        button.PerformClick();

        clicks.ShouldBe(0);
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies command replacement raises the standard property notification once.</summary>
    [Fact]
    public void Command_WhenReplacementChanges_RaisesPropertyChangedOnce()
    {
        var button = new Button();
        var names = new List<string?>();
        button.PropertyChanged += (_, eventArgs) => names.Add(eventArgs.PropertyName);
        var command = new ProbeCommand();

        button.Command = command;
        button.Command = command;

        names.ShouldBe([nameof(Button.Command)]);
    }

    /// <summary>Verifies keyboard activation reaches the public Button event and command.</summary>
    [Fact]
    public void Route_WhenEnterIsPressed_ActivatesButtonWithKeyboardCause()
    {
        var command = new ProbeCommand();
        var button = new Button { Command = command };
        ActivationCause? cause = null;
        button.Click += (_, eventArgs) => cause = eventArgs.Cause;

        Router.Route(
            button,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Enter,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

        cause.ShouldBe(ActivationCause.Keyboard);
        command.Executions.Count.ShouldBe(1);
    }

    /// <summary>Verifies semantic content hit testing still reaches the owning default behavior.</summary>
    [Fact]
    public async Task Dispatch_WhenContentIsPointerTarget_ActivatesOwningButtonAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var button = new Button
        {
            Bounds = new Rect(0, 0, 6, 1),
            Content = new ControlText("Click"),
        };
        new Engine().Layout(button, new Size(6, 1));
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        await dispatcher.InvokeAsync(() =>
        {
            button.Attach(dispatcher);
            using var capture = new CaptureManager(button);
            _ = capture.Dispatch(Pointer(new Point(2, 0), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(2, 0), PointerAction.Release));
        }, TestContext.Current.CancellationToken);

        clicks.ShouldBe(1);
    }

    /// <summary>Verifies passive motion over owned text resolves hover to the semantic Button.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerMovesOverContent_HoversButtonInsteadOfTextAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var content = new ControlText("Hover");
        var button = new Button
        {
            Bounds = new Rect(0, 0, 6, 1),
            Content = content,
        };
        new Engine().Layout(button, new Size(6, 1));

        await dispatcher.InvokeAsync(() =>
        {
            button.Attach(dispatcher);
            using var capture = new CaptureManager(button);

            _ = capture.Dispatch(Pointer(new Point(2, 0), PointerAction.Move));

            capture.Hovered.ShouldBeSameAs(button);
            button.IsHovered.ShouldBeTrue();
            content.IsHovered.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies padding, margin, Unicode content, and semantic rendering.</summary>
    [Fact]
    public void Render_WhenButtonHasUnicodeContent_ComputesExactBoundsAndCells()
    {
        var content = new ControlText("界") { Margin = new Thickness(1, 0) };
        var button = new Button { Content = content, Padding = new Thickness(1) };
        new Engine().Layout(button, new Size(6, 3));
        using var frame = new Frame(new Size(6, 3));

        button.Render(frame.Canvas);

        button.DesiredSize.ShouldBe(new Size(6, 3));
        content.Bounds.ShouldBe(new Rect(2, 1, 2, 1));
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("界");
        frame.GetCell(new Point(3, 1)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies a styled Button owns the complete visible surface behind its content.</summary>
    [Fact]
    public void Render_WhenStyleDefinesBackground_FillsButtonBounds()
    {
        var style = ThemeTestSupport.OverlayStyle<Button>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(255), background: Color.Indexed(24))));
        var button = new Button
        {
            Content = new ControlText("Run"),
            Padding = new Thickness(1),
            Style = style,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var size = new Size(8, 3);
        new Engine().Layout(button, size);
        using var frame = new Frame(size);

        button.Render(frame.Canvas);

        frame.GetCell(new Point(0, 0)).Style.Background.ShouldBe(Color.Indexed(24));
        frame.GetCell(new Point(7, 2)).Style.Background.ShouldBe(Color.Indexed(24));
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("R");
    }

    /// <summary>Verifies unavailable controls reject programmatic activation.</summary>
    [Theory]
    [InlineData(false, Visibility.Visible)]
    [InlineData(true, Visibility.Hidden)]
    public void PerformClick_WhenButtonIsUnavailable_DoesNothing(bool enabled, Visibility visibility)
    {
        var button = new Button { IsEnabled = enabled, Visibility = visibility };
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        button.PerformClick();

        clicks.ShouldBe(0);
    }

    private static Pointer Pointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);
}
