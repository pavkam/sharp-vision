// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

using SharpVision.Dialogs;
using SharpVision.Popups;

/// <summary>Verifies outside-press dismissal for a context menu, which two different mechanisms
/// serve depending on whether a modal plane is up.
///
/// <para>Modeless, <c>LightDismiss</c> does the work: it registers a preview pointer handler by
/// walking to the topmost parent it can reach, which is the application root.</para>
///
/// <para>With a plane active that handler is unreachable - <c>Router.RouteCore</c> stops a route at
/// the plane boundary, and the boundary for a press inside a dialog is the dialog, never the
/// application root above it. The plane publishes dismissal itself instead:
/// <c>PointerManager.Dispatch</c> calls <c>ModalityManager.RequestDismiss</c>, which reaches the
/// popup through <c>Popup.OnModalDismissRequested</c> because an open popup joins the active scope
/// as a secondary root.</para>
///
/// <para>The modal case went unverified for a long time, on the assumption that the dismissal
/// policy probably already covered it. It does - and these tests keep it that way, since the two
/// mechanisms are independent and a regression in either would otherwise be invisible.</para>
/// </summary>
public sealed class LightDismissModalityTests
{
    /// <summary>Verifies a context menu opened inside a presented modal dialog closes on a press
    /// landing outside it but inside the same plane - the only region reachable while the dialog is
    /// up - and that dismissal stops at the menu rather than taking the dialog with it.</summary>
    [Fact]
    public async Task Pointer_WhenMenuIsOpenedInsideAModalDialog_ClosesOnOutsidePressAsync()
    {
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);

        var menu = NewMenu();
        var inner = NewAnchor(menu);
        var dialog = new MenuHostDialog(inner);

        Task<bool>? pending = null;
        await surface.UpdateAsync(
            () => pending = dialog.Present(opener, initialFocus: null, TestContext.Current.CancellationToken),
            "present the modal dialog");

        await OpenMenuAsync(surface, menu, inner);

        // Pinned because it is what makes this case non-trivial: LightDismiss registers on the
        // application root, above the plane, so the route for the press below never reaches it.
        await AssertLightDismissIsRegisteredAboveThePlaneAsync(surface, menu);

        // Constrained to the dialog's own surface - a press outside the plane would prove nothing
        // about dismissal from within it.
        var press = await OutsidePressPointAsync(surface, menu, inner, () => dialog.SurfaceBounds);
        await PressAsync(surface, press);

        menu.Opened.ShouldBeFalse("an outside press inside the modal plane must dismiss the menu");
        pending!.IsCompleted.ShouldBeFalse("dismissal must stop at the menu, not close the dialog too");

        await surface.UpdateAsync(() => dialog.Accept(true), "close the dialog");
        (await pending).ShouldBeTrue();
    }

    /// <summary>Verifies the modal dismissal policy is not indiscriminate: a press landing on the
    /// menu itself leaves it open, so "dismiss on any press inside the plane" cannot pass for the
    /// behavior the case above asserts.</summary>
    [Fact]
    public async Task Pointer_WhenPressLandsOnTheMenuInsideAModalDialog_LeavesItOpenAsync()
    {
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);

        var menu = NewMenu();
        var inner = NewAnchor(menu);
        var dialog = new MenuHostDialog(inner);

        Task<bool>? pending = null;
        await surface.UpdateAsync(
            () => pending = dialog.Present(opener, initialFocus: null, TestContext.Current.CancellationToken),
            "present the modal dialog");

        await OpenMenuAsync(surface, menu, inner);

        var inside = await surface.Application.Dispatcher.InvokeAsync(
            () =>
            {
                var bounds = ((Popup) menu.Presentation).SurfaceBounds;
                return new Point(bounds.X, bounds.Y);
            },
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(inside);
        await surface.Pointer.PressAsync();

        menu.Opened.ShouldBeTrue("a press on the menu itself must not dismiss it");

        await surface.Pointer.ReleaseAsync();
        await surface.UpdateAsync(() => dialog.Accept(true), "close the dialog");
        _ = await pending!;
    }

    /// <summary>The modeless counter-case, which travels the LightDismiss path instead. This is the
    /// configuration every other light-dismiss test already runs, and the reason the modal case
    /// stayed unverified.</summary>
    [Fact]
    public async Task Pointer_WhenMenuIsOpenedWithNoModalPlane_ClosesOnOutsidePressAsync()
    {
        var menu = NewMenu();
        var inner = NewAnchor(menu);
        var host = new Overlay { Children = { inner } };

        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);

        await OpenMenuAsync(surface, menu, inner);

        var press = await OutsidePressPointAsync(surface, menu, inner, () => host.Bounds);
        await PressAsync(surface, press);

        menu.Opened.ShouldBeFalse();
    }

    private static ContextMenu NewMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Cut" });
        return menu;
    }

    private static Button NewAnchor(ContextMenu menu) => new()
    {
        Text = "Inner",
        Width = Length.Cells(10),
        Height = Length.Cells(3),
        ContextMenu = menu
    };

    // Show(row, col) rather than a right-click: the menu then opens at an exact known cell, so the
    // outside-press search below is not chasing wherever a synthesized click happened to land.
    private static async Task OpenMenuAsync(ComponentSurface surface, ContextMenu menu, ControlBase anchor)
    {
        var bounds = await surface.Application.Dispatcher.InvokeAsync(
            () => anchor.Bounds,
            TestContext.Current.CancellationToken);
        bounds.ShouldNotBe(default, "the anchor must be laid out before the menu opens");

        await surface.UpdateAsync(() => menu.Show(bounds.Y, bounds.X), "open the context menu");
        menu.Opened.ShouldBeTrue();
    }

    private static async Task AssertLightDismissIsRegisteredAboveThePlaneAsync(
        ComponentSurface surface,
        ContextMenu menu) =>
        await surface.Application.Dispatcher.InvokeAsync(
            () =>
            {
                // The same walk LightDismiss's constructor performs.
                var root = menu.Presentation;
                while (root.Parent is { } parent)
                {
                    root = parent;
                }

                root.ShouldBeSameAs(
                    surface.Application.Root,
                    "LightDismiss registers on the application root, so a plane boundary below it puts " +
                    "the handler out of route and the dismissal policy is what must answer");
            },
            TestContext.Current.CancellationToken);

    // The dismissing press has to miss both the popup surface and the anchor, or dismissal is
    // deliberately declined. Both rects are only known once the popup is laid out, so the point is
    // searched for rather than guessed: a hardcoded cell silently stops testing anything the moment
    // a default border or padding moves the popup.
    private static async Task<Point> OutsidePressPointAsync(
        ComponentSurface surface,
        ContextMenu menu,
        ControlBase anchor,
        Func<Rect> region) =>
        await surface.Application.Dispatcher.InvokeAsync(
            () =>
            {
                var surfaceBounds = ((Popup) menu.Presentation).SurfaceBounds;
                surfaceBounds.ShouldNotBe(default, "the popup must be laid out before its bounds are read");

                var anchorBounds = anchor.Bounds;
                var searched = region();
                searched.ShouldNotBe(default, "the press region must be laid out");

                for (var y = searched.Y; y < searched.Y + searched.Height; y++)
                {
                    for (var x = searched.X; x < searched.X + searched.Width; x++)
                    {
                        var candidate = new Point(x, y);
                        if (!surfaceBounds.Contains(candidate) && !anchorBounds.Contains(candidate))
                        {
                            return candidate;
                        }
                    }
                }

                throw new InvalidOperationException(
                    "The popup and its anchor cover the whole press region, so no outside press exists.");
            },
            TestContext.Current.CancellationToken);

    private static async Task PressAsync(ComponentSurface surface, Point point)
    {
        await surface.Pointer.MoveToAsync(point);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
    }

    private sealed class MenuHostDialog: Dialog<bool>
    {
        public MenuHostDialog(ControlBase content) : base(cancelledResult: false) =>
            Content = new Stack
            {
                Children =
                {
                    new ControlText("Pick an action"),
                    content
                }
            };

        public Task<bool> Present(ControlBase owner, ControlBase? initialFocus, CancellationToken cancellationToken) =>
            PresentAsync(owner, initialFocus, cancellationToken);

        public bool Accept(bool result) => Complete(result);
    }
}
