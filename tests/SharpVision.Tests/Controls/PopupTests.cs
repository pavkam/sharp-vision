// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

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

using KeyAction = Terminal.Input.Action;

/// <summary>Verifies anchored popup visibility, placement, and dismissal behavior.</summary>
public sealed class PopupTests
{
    /// <summary>Verifies closed content cannot leak into rendering or hit testing.</summary>
    [Fact]
    public void Render_WhenClosed_DoesNotRenderOrHitTestChild()
    {
        ProbeControl child = new ProbeControl(new Size(3, 1)) { Content = "pop".AsMemory() };
        Popup popup = new Popup { Child = child };
        new Engine().Layout(popup, new Size(8, 4));
        using Frame frame = new Frame(new Size(8, 4));

        popup.Render(frame.Canvas);

        child.RenderCalls.ShouldBe(0);
        popup.HitTest(new Point(0, 0)).ShouldBeNull();
        FrameOracle.Get(frame, default).ShouldBeEmpty();
    }

    /// <summary>Verifies a popup below an anchor flips above before terminal-edge clamping.</summary>
    [Fact]
    public void Arrange_WhenBelowWouldOverflow_FlipsAboveAnchor()
    {
        ProbeControl anchor = new ProbeControl { Bounds = new Rect(2, 3, 2, 1) };
        ProbeControl child = new ProbeControl(new Size(4, 2));
        Popup popup = new Popup { Anchor = anchor, Child = child, IsOpen = true };

        new Engine().Layout(popup, new Size(10, 5));

        child.Bounds.ShouldBe(new Rect(3, 2, 4, 2));
    }

    /// <summary>Verifies an open popup owns an opaque framed surface around its content rather than leaking the child inline.</summary>
    [Fact]
    public void Render_WhenOpen_DrawsSurfaceFrameAndContainsChild()
    {
        ProbeControl anchor = new ProbeControl { Bounds = new Rect(2, 0, 2, 1) };
        ProbeControl child = new ProbeControl(new Size(4, 1)) { Content = "pick".AsMemory() };
        Popup popup = new Popup { Anchor = anchor, Child = child, IsOpen = true };
        Size size = new Size(12, 6);
        new Engine().Layout(popup, size);
        using Frame frame = new Frame(size);

        popup.Render(frame.Canvas);

        popup.SurfaceBounds.ShouldBe(new Rect(2, 1, 6, 3));
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("p");
        FrameOracle.Get(frame, new Point(7, 3)).ShouldBe("╯");
        popup.HitTest(new Point(3, 2)).ShouldBeSameAs(child);
        popup.HitTest(new Point(2, 1)).ShouldBeSameAs(popup);
    }

    /// <summary>Verifies popup surface and frame retain semantic resource decorations.</summary>
    [Fact]
    public void Render_WhenStyleUsesModernDecorations_PreservesSurfaceStyle()
    {
        ControlStyle<Popup> style = ThemeTestSupport.OverlayStyle<Popup>(
            (State.Normal, new ThemeOverlay(
                attributes: Attributes.Overline,
                underline: Underline.Dotted,
                underlineColor: Color.Indexed(4))));
        Popup popup = new Popup
        {
            Child = new ProbeControl(new Size(1, 1)),
            IsOpen = true,
            Style = style,
        };
        Size size = new Size(6, 4);
        new Engine().Layout(popup, size);
        using Frame frame = new Frame(size);

        popup.Render(frame.Canvas);

        Style rendered = frame.GetCell(new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Y)).Style;
        rendered.Attributes.ShouldBe(Attributes.Overline);
        rendered.Underline.ShouldBe(Underline.Dotted);
        rendered.UnderlineColor.ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies an open popup is painted and hit-tested above later ordinary siblings in its owning overlay.</summary>
    [Fact]
    public void Render_WhenLaterSiblingOverlaps_PopupRetainsTopmostInputAndSurface()
    {
        ComboBox comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            Items = ["Small", "Large"],
            DropDownHeight = 2,
            IsOpen = true,
        };
        Border cover = new Border { Background = Color.Indexed(7) };
        Overlay root = new Overlay { ClipToBounds = false };
        root.Children.Add(comboBox);
        root.Children.Add(cover);
        Size size = new Size(16, 8);
        new Engine().Layout(root, size);
        List list = comboBox.Children[0].ShouldBeOfType<Popup>().Child.ShouldBeOfType<List>();
        Point point = new Point(list.Bounds.X + 1, list.Bounds.Y);
        using Frame frame = new Frame(size);

        root.Render(frame.Canvas);

        _ = root.HitTest(point).ShouldBeOfType<ListItem>();
        FrameOracle.Get(frame, point).ShouldBe("m");
    }

    /// <summary>Verifies Escape bubbles through popup content and closes the owner.</summary>
    [Fact]
    public void Dispatch_WhenEscapeArrives_ClosesOpenPopup()
    {
        ProbeControl child = new ProbeControl();
        Popup popup = new Popup { Child = child, IsOpen = true };
        KeyEventArgs eventArgs = new KeyEventArgs(new Stroke(
            Code.Escape,
            default,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        Router.Route(child, Events.Key, eventArgs);

        popup.IsOpen.ShouldBeFalse();
        eventArgs.Handled.ShouldBeTrue();
    }

    /// <summary>Verifies opening transfers focus to a focusable popup child for keyboard-driven pickers.</summary>
    [Fact]
    public async Task IsOpen_WhenFocusableChildExists_MovesFocusToChildAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            List child = new List { Items = ["first", "second"] };
            Popup popup = new Popup { Child = child };
            Overlay root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using FocusManager focus = new FocusManager(root);

            popup.IsOpen = true;

            focus.Focused.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }
}
