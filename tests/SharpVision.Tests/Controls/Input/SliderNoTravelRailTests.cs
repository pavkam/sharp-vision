// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies a Slider rail without travel - one cell, an empty range, or a rail shrunk to
/// one cell or nothing while a drag is in flight - keeps the current value for both the press and
/// the held move, the same no-travel rule the ScrollBar thumb drag applies, while focus and capture
/// behave as they do on any other rail. Also pins the drag's reaction to live geometry changes and
/// to the slider becoming unavailable mid-drag.</summary>
public sealed class SliderNoTravelRailTests
{
    /// <summary>Verifies a press on a one-cell rail focuses and captures like any press but names no
    /// value, and the release leaves the value and rendering untouched.</summary>
    [Fact]
    public async Task Pointer_WhenRailIsOneCell_PressKeepsValueAndCapturesAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Value = 50,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(slider, new Point(0, 0));
        await surface.Pointer.PressAsync();

        // Assert - the press is a real press, it just cannot name a value
        slider.Value.ShouldBe(50);
        slider.IsPressed.ShouldBeTrue();
        surface.ShouldHaveFocus(slider);
        surface.ShouldHaveCapture(slider);

        // Act - held motion off the rail and the release
        await surface.Pointer.MovePressedToAsync(new Point(2, 0));
        slider.Value.ShouldBe(50);
        await surface.Pointer.ReleaseAsync();

        // Assert
        slider.Value.ShouldBe(50);
        slider.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        changes.ShouldBeEmpty();
        surface.ShouldRender("◆");
    }

    /// <summary>Verifies a rail whose Minimum equals Maximum has no travel however long it is, so a
    /// press and a held move keep the only representable value without publishing a change.</summary>
    [Fact]
    public async Task Pointer_WhenRangeIsEmpty_PressAndDragKeepValueAsync()
    {
        // Arrange
        var slider = new Slider { Minimum = 7, Maximum = 7, HorizontalAlignment = HorizontalAlignment.Stretch };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        slider.Value.ShouldBe(7);

        // Act
        await surface.Pointer.MoveToAsync(slider, new Point(3, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(slider);
        await surface.Pointer.MovePressedToAsync(new Point(9, 0));
        await surface.Pointer.ReleaseAsync();

        // Assert
        slider.Value.ShouldBe(7);
        changes.ShouldBeEmpty();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a held pointer move after the rail shrinks to one cell leaves the value
    /// where the press put it, publishes no change, and keeps the drag's capture.</summary>
    [Fact]
    public async Task Pointer_WhenRailShrinksToOneCellDuringDrag_KeepsValueAsync()
    {
        // Arrange
        var slider = new Slider { Maximum = 100, HorizontalAlignment = HorizontalAlignment.Stretch };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(slider, new Point(5, 0));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);
        changes.Clear();

        // Act - the rail collapses to a single cell while the drag is in flight
        await surface.ResizeAsync(new Size(1, 1));
        slider.Bounds.Width.ShouldBe(1);
        await surface.Pointer.MovePressedToAsync(new Point(0, 0));

        // Assert - a one-cell rail has no travel, so no pointer offset can name another value
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
        surface.ShouldHaveCapture(slider);
        await surface.Pointer.ReleaseAsync();
        slider.Value.ShouldBe(50);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a held pointer move after padding consumes the whole rail commits nothing,
    /// publishes no change, and keeps the drag's capture until the release.</summary>
    [Fact]
    public async Task Pointer_WhenRailBecomesEmptyDuringDrag_KeepsValueAndCaptureAsync()
    {
        // Arrange - a two-cell left padding leaves a nine-cell rail on the eleven-cell surface
        var slider = new Slider
        {
            Maximum = 100,
            Padding = new Thickness(2, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(slider, new Point(6, 0));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);
        changes.Clear();

        // Act - the surface shrinks to the padding alone, so the rail has no cells at all
        await surface.ResizeAsync(new Size(2, 1));
        slider.ContentBounds.Width.ShouldBe(0);
        await surface.Pointer.MovePressedToAsync(new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(0, 0));

        // Assert
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
        surface.ShouldHaveCapture(slider);
        await surface.Pointer.ReleaseAsync();
        slider.Value.ShouldBe(50);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies the drag resumes mapping the pointer once the rail regains travel, so the
    /// no-travel guard only suspends commits rather than ending the gesture.</summary>
    [Fact]
    public async Task Pointer_WhenRailRegainsTravelDuringDrag_ResumesTrackingAsync()
    {
        // Arrange
        var slider = new Slider { Maximum = 100, HorizontalAlignment = HorizontalAlignment.Stretch };
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(slider, new Point(5, 0));
        await surface.Pointer.PressAsync();
        await surface.ResizeAsync(new Size(1, 1));
        await surface.Pointer.MovePressedToAsync(new Point(0, 0));
        slider.Value.ShouldBe(50);

        // Act - the rail grows back and the held pointer names the far end
        await surface.ResizeAsync(new Size(11, 1));
        await surface.Pointer.MovePressedToAsync(new Point(10, 0));

        // Assert
        slider.Value.ShouldBe(100);
        surface.ShouldHaveCapture(slider);
    }

    /// <summary>Verifies a vertical rail that resizes mid-drag maps later pointer motion through the
    /// live rail, with the top cell still naming Maximum on the grown rail.</summary>
    [Fact]
    public async Task Pointer_WhenVerticalRailResizesDuringDrag_MapsMotionAgainstLiveGeometryAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(1, 11),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(slider, new Point(0, 5));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);

        // Act - the same absolute row sits three quarters of the way up the grown rail
        await surface.ResizeAsync(new Size(1, 21));
        slider.Bounds.Height.ShouldBe(21);
        await surface.Pointer.MovePressedToAsync(new Point(0, 5));

        // Assert the thumb is under the pointer on the grown rail
        slider.Value.ShouldBe(75);
        surface.Cell(new Point(0, 5)).Text.ShouldBe("◆");
        surface.ShouldHaveCapture(slider);

        // Act
        await surface.Pointer.MovePressedToAsync(new Point(0, 0));
        await surface.Pointer.ReleaseAsync();

        // Assert
        slider.Value.ShouldBe(100);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("◆");
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies hiding the slider mid-drag cancels the drag: capture and the pressed state
    /// are released, and later held motion and the release commit nothing.</summary>
    [Fact]
    public async Task Pointer_WhenSliderIsHiddenDuringDrag_CancelsDragAsync()
    {
        // Arrange
        var slider = new Slider { Maximum = 100, HorizontalAlignment = HorizontalAlignment.Stretch };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(slider, new Point(5, 0));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);
        surface.ShouldHaveCapture(slider);
        changes.Clear();

        // Act
        await surface.UpdateAsync(() => slider.Visibility = Visibility.Hidden, "hide the slider mid-drag");

        // Assert
        surface.ShouldHaveCapture(null);
        slider.IsPressed.ShouldBeFalse();
        await surface.Pointer.MovePressedToAsync(new Point(9, 0));
        await surface.Pointer.ReleaseAsync();
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
    }

    /// <summary>Verifies detaching the slider from its parent mid-drag cancels the drag: capture and
    /// the pressed state are released, and later held motion and the release commit nothing.</summary>
    [Fact]
    public async Task Pointer_WhenSliderIsDetachedDuringDrag_CancelsDragAsync()
    {
        // Arrange
        var slider = new Slider { Maximum = 100, HorizontalAlignment = HorizontalAlignment.Stretch };
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { slider } };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(slider, new Point(5, 0));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);
        surface.ShouldHaveCapture(slider);
        changes.Clear();

        // Act
        await surface.UpdateAsync(() => stack.Children.Remove(slider).ShouldBeTrue(), "detach the slider mid-drag");

        // Assert
        surface.ShouldHaveCapture(null);
        slider.IsPressed.ShouldBeFalse();
        slider.IsDisposed.ShouldBeFalse();
        await surface.Pointer.MovePressedToAsync(new Point(9, 0));
        await surface.Pointer.ReleaseAsync();
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
    }

    private static List<(int Previous, int Value)> Record(Slider slider)
    {
        List<(int Previous, int Value)> changes = [];
        slider.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        return changes;
    }
}
