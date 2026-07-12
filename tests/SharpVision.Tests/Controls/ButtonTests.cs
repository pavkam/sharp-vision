using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
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
        button.Children.Add(new ProbeControl());
        _ = Should.Throw<InvalidOperationException>(() =>
            button.Children.Add(new ProbeControl()));
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
