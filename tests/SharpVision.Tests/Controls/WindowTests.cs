using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;

using Shouldly;

using KeyAction = SharpVision.Terminal.Input.Action;

namespace SharpVision.Tests.Controls;

/// <summary>Verifies framed terminal window layout, title chrome, and visual shadow behavior.</summary>
public sealed class WindowTests
{
    /// <summary>Verifies a title owns the top edge while content receives the bounded interior box.</summary>
    [Fact]
    public void Render_WhenTitleAndChildArePresent_DrawsFramedChromeAndInterior()
    {
        var child = new ProbeControl(new Size(3, 1)) { Content = "app".AsMemory() };
        var window = new Window
        {
            Title = "Tools",
            Child = child,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var size = new Size(10, 4);
        new Engine().Layout(window, size);
        using var frame = new Frame(size);

        window.Render(frame.Canvas);

        child.Bounds.ShouldBe(new Rect(1, 1, 8, 2));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("s");
        FrameOracle.Get(frame, new Point(7, 0)).ShouldBe(" ");
        FrameOracle.Get(frame, new Point(9, 0)).ShouldBe("╮");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(0, 3)).ShouldBe("╰");
    }

    /// <summary>Verifies the Turbo Vision block shadow occupies only translated cells outside the window body.</summary>
    [Fact]
    public void Render_WhenBlockShadowIsEnabled_DrawsOutsideBodyWithoutCoveringContent()
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 4, 3),
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowOffset = new Point(1, 1),
        };
        using var frame = new Frame(new Size(6, 5));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("╯");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 3)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
    }

    /// <summary>Verifies a long title clips inside the top edge without corrupting either frame corner.</summary>
    [Fact]
    public void Render_WhenTitleExceedsFrameWidth_PreservesTopCorners()
    {
        var window = new Window { Title = "A deliberately long title" };
        var size = new Size(6, 2);
        new Engine().Layout(window, size);
        using var frame = new Frame(size);

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("╮");
    }

    /// <summary>Verifies centered and right title placement keep the title inside both corners.</summary>
    [Theory]
    [InlineData(WindowTitlePlacement.Center, 9)]
    [InlineData(WindowTitlePlacement.Right, 16)]
    public void Render_WhenTitlePlacementChanges_AlignsTitleInsideFrame(
        WindowTitlePlacement placement,
        int expectedTitleColumn)
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 20, 3),
            Title = "Hi",
            TitlePlacement = placement,
        };
        using var frame = new Frame(new Size(20, 3));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(expectedTitleColumn, 0)).ShouldBe("H");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(19, 0)).ShouldBe("╮");
    }

    /// <summary>Verifies unhandled Enter and Escape invoke the first available default and cancel button inside the window.</summary>
    [Fact]
    public void Dispatch_WhenEnterOrEscapeIsUnhandled_InvokesWindowDefaultOrCancelButton()
    {
        var defaults = 0;
        var cancels = 0;
        var content = new Stack();
        var accept = new Button { IsDefault = true };
        var cancel = new Button { IsCancel = true };
        accept.Click += (_, _) => defaults++;
        cancel.Click += (_, _) => cancels++;
        content.Children.Add(accept);
        content.Children.Add(cancel);
        var window = new Window { Child = content };

        Router.Route(window, Events.Key, Key(Code.Enter));
        Router.Route(window, Events.Key, Key(Code.Escape));

        defaults.ShouldBe(1);
        cancels.ShouldBe(1);
    }

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));
}
