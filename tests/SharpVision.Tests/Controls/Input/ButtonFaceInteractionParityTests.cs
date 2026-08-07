// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using System.Buffers;

/// <summary>Verifies the rectangle a pressed Button actually paints and the rectangle it accepts
/// pointer interaction in are the same rectangle.
///
/// <para>They are reached by separate paths: <c>GetChromeRenderOptions</c> paints
/// <c>BodyBounds</c>, while <c>PressableBase</c> hit-tests <c>InteractionBounds</c>. Both read
/// <c>FaceBounds</c> today, but nothing asserted that they agree, and a divergence is exactly what
/// produced the defect this file guards against: a band of visibly-lit face that silently refused
/// to activate.</para>
///
/// <para>The older regression test presses one hardcoded translated cell. These read the face
/// rectangle back off the rendered surface and then walk its whole boundary, inside and out, so the
/// assertion keeps its meaning if the shadow offset, padding, or size ever change. The face is
/// bordered because that is what makes it legible on the surface: the harness models every cell's
/// background as the same value, so a border's glyphs are the only honest way to see where the face
/// was actually drawn.</para>
/// </summary>
public sealed class ButtonFaceInteractionParityTests
{
    private static readonly Point _shadowOffset = new(1, 1);
    private static readonly Size _surface = new(12, 8);
    private static readonly SearchValues<char> _borderGlyphs = SearchValues.Create("┌─┐│└┘");

    /// <summary>Verifies every corner of the drawn pressed face keeps the button pressed and every
    /// cell just outside it does not, so the interaction rectangle is neither smaller than what is
    /// lit - the dead band that defect produced - nor larger than it.</summary>
    [Fact]
    public async Task Pointer_WhenDraggingAroundThePressedFace_TracksExactlyTheDrawnRectangleAsync()
    {
        var button = NewButton();
        await using var surface = await ComponentSurface.MountAsync(
            button,
            _surface,
            TestContext.Current.CancellationToken);

        // Pressed before the face is read, so what is measured is the translated face and not the
        // resting one, which is the entire point of the case.
        await surface.Pointer.MoveToAsync(button, new Point(2, 1));
        await surface.Pointer.PressAsync();
        button.Pressed.ShouldBeTrue();

        var face = DrawnFace(surface);
        var bounds = await surface.Application.Dispatcher.InvokeAsync(
            () => button.Bounds,
            TestContext.Current.CancellationToken);

        face.ShouldBe(
            new Rect(bounds.X + _shadowOffset.X, bounds.Y + _shadowOffset.Y, bounds.Width, bounds.Height),
            "the pressed face is drawn translated by the shadow offset");
        face.ShouldNotBe(bounds, "a face that never moved would make the rest of this vacuous");

        foreach (var inside in Corners(face))
        {
            await surface.Pointer.MovePressedToAsync(inside);
            button.Pressed.ShouldBeTrue($"{inside} is a drawn face cell and must stay pressed");
        }

        foreach (var outside in JustOutsideThePressedFace(face))
        {
            await surface.Pointer.MovePressedToAsync(outside);
            button.Pressed.ShouldBeFalse($"{outside} is outside the drawn face and must not stay pressed");

            // Back inside, so every outside probe starts from the same pressed state.
            await surface.Pointer.MovePressedToAsync(new Point(face.X + 1, face.Y + 1));
            button.Pressed.ShouldBeTrue();
        }

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a release on each corner of the drawn pressed face activates. Pressed-state
    /// tracking and activation are separate decisions, so tracking the right rectangle while
    /// declining to activate on part of it would otherwise pass unnoticed.</summary>
    [Fact]
    public async Task Pointer_WhenReleasingOnAnyDrawnFaceCorner_ActivatesAsync()
    {
        var button = NewButton();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            _surface,
            TestContext.Current.CancellationToken);

        var face = await PressedFaceAsync(surface, button);
        clicks = 0;
        var expected = 0;

        foreach (var corner in Corners(face))
        {
            await surface.Pointer.MoveToAsync(new Point(face.X + 1, face.Y + 1));
            await surface.Pointer.PressAsync();
            await surface.Pointer.MovePressedToAsync(corner);
            await surface.Pointer.ReleaseAsync();

            expected++;
            clicks.ShouldBe(expected, $"releasing on drawn face corner {corner} must activate");
        }
    }

    /// <summary>Verifies a release just outside the drawn face does not activate, so the parity
    /// above cannot be satisfied by a control that simply activates everywhere.</summary>
    [Fact]
    public async Task Pointer_WhenReleasingJustOutsideTheDrawnFace_DoesNotActivateAsync()
    {
        var button = NewButton();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            _surface,
            TestContext.Current.CancellationToken);

        var face = await PressedFaceAsync(surface, button);
        var bounds = await surface.Application.Dispatcher.InvokeAsync(
            () => button.Bounds,
            TestContext.Current.CancellationToken);
        clicks = 0;

        foreach (var outside in OutsideBothFaces(face, bounds))
        {
            await surface.Pointer.MoveToAsync(new Point(face.X + 1, face.Y + 1));
            await surface.Pointer.PressAsync();
            await surface.Pointer.MovePressedToAsync(outside);
            await surface.Pointer.ReleaseAsync();

            clicks.ShouldBe(0, $"releasing outside the drawn face at {outside} must not activate");
        }
    }

    /// <summary>Verifies the parity holds through the un-press transition too. Dragging off the
    /// pressed face clears the pressed state, which snaps the drawn face back from the translated
    /// rectangle to Bounds - and the cells of that restored face are interactive again, so a
    /// release on one activates. This is why "outside the pressed face" and "outside the button"
    /// are different sets, and it is the behavior that makes the two tests above pick their probe
    /// cells the way they do.</summary>
    [Fact]
    public async Task Pointer_WhenDragLeavesThePressedFace_TheRestingFaceBecomesInteractiveAgainAsync()
    {
        var button = NewButton();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            _surface,
            TestContext.Current.CancellationToken);

        var face = await PressedFaceAsync(surface, button);
        var bounds = await surface.Application.Dispatcher.InvokeAsync(
            () => button.Bounds,
            TestContext.Current.CancellationToken);
        clicks = 0;

        // Top-left of the resting face: inside Bounds, outside the translated pressed face.
        var restingOnly = new Point(bounds.X, bounds.Y);
        face.Contains(restingOnly).ShouldBeFalse("the probe must be off the translated face");
        bounds.Contains(restingOnly).ShouldBeTrue("the probe must be on the resting face");

        await surface.Pointer.MoveToAsync(new Point(face.X + 1, face.Y + 1));
        await surface.Pointer.PressAsync();
        button.Pressed.ShouldBeTrue();

        await surface.Pointer.MovePressedToAsync(restingOnly);
        button.Pressed.ShouldBeFalse("leaving the translated face clears the pressed state");

        // The face has snapped back to Bounds, so this cell is drawn face again - and live.
        DrawnFace(surface).ShouldBe(bounds);

        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(1, "a release on the restored resting face activates");
    }

    private static async Task<Rect> PressedFaceAsync(ComponentSurface surface, Button button)
    {
        await surface.Pointer.MoveToAsync(button, new Point(2, 1));
        await surface.Pointer.PressAsync();
        var face = DrawnFace(surface);
        await surface.Pointer.ReleaseAsync();
        return face;
    }

    // Reads the face back off the rendered surface instead of recomputing it. Recomputing would
    // compare the renderer's own rectangle with itself; the border glyphs are independent evidence
    // of where the face actually landed. The shadow is skipped while pressed, so nothing else on
    // the surface draws these glyphs.
    private static Rect DrawnFace(ComponentSurface surface)
    {
        int? minX = null, minY = null, maxX = null, maxY = null;

        for (var y = 0; y < _surface.Height; y++)
        {
            for (var x = 0; x < _surface.Width; x++)
            {
                var text = surface.Cell(new Point(x, y)).Text;
                if (text.Length != 1 || !_borderGlyphs.Contains(text[0]))
                {
                    continue;
                }

                minX = minX is { } left ? Math.Min(left, x) : x;
                minY = minY is { } top ? Math.Min(top, y) : y;
                maxX = maxX is { } right ? Math.Max(right, x) : x;
                maxY = maxY is { } bottom ? Math.Max(bottom, y) : y;
            }
        }

        _ = minX.ShouldNotBeNull("the pressed button must draw its bordered face");
        return new Rect(minX.Value, minY!.Value, maxX!.Value - minX.Value + 1, maxY!.Value - minY!.Value + 1);
    }

    private static Point[] Corners(Rect face) =>
    [
        new(face.X, face.Y),
        new(face.Right - 1, face.Y),
        new(face.X, face.Bottom - 1),
        new(face.Right - 1, face.Bottom - 1)
    ];

    // One cell beyond the middle of each edge of the pressed face, so a corner diagonal cannot be
    // mistaken for an edge miss.
    private static Point[] JustOutsideThePressedFace(Rect face) =>
    [
        new(face.X - 1, face.Y + (face.Height / 2)),
        new(face.Right, face.Y + (face.Height / 2)),
        new(face.X + (face.Width / 2), face.Y - 1),
        new(face.X + (face.Width / 2), face.Bottom)
    ];

    // Outside the pressed face AND outside the resting one. Leaving the pressed face un-presses the
    // button, which snaps the face back to Bounds - so a cell that is merely outside the translated
    // rectangle can still be live, and only a cell outside both is a true miss. Asserted directly by
    // Pointer_WhenDragLeavesThePressedFace_TheRestingFaceBecomesInteractiveAgainAsync below.
    private static Point[] OutsideBothFaces(Rect face, Rect bounds)
    {
        var right = Math.Max(face.Right, bounds.Right);
        var bottom = Math.Max(face.Bottom, bounds.Bottom);
        return
        [
            new(right, face.Y + (face.Height / 2)),
            new(face.X + (face.Width / 2), bottom),
            new(right, bottom)
        ];
    }

    private static Button NewButton() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Width = Length.Cells(6),
        Height = Length.Cells(3),
        Text = "Go",
        Style = ButtonStyle.Standard with
        {
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Light),
            Shadow = AppearanceTestValues.Shadow(visible: true, offset: _shadowOffset)
        }
    };
}
