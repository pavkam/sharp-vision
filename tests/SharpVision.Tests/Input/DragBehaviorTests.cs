// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies DragBehavior state machine transitions and delegate invocations.</summary>
public sealed class DragBehaviorTests
{
    private readonly Rect _bounds = new(0, 0, 10, 10);
    private bool _focused;
    private bool _captured;
    private bool _pressed;

    private DragBehavior CreateBehavior(bool available = true) => new(
        () => _bounds,
        () => available,
        () =>
        {
            _focused = true;
            return true;
        },
        () =>
        {
            _captured = true;
            return true;
        },
        () => _captured,
        () => _captured = false,
        pressed => _pressed = pressed);

    /// <summary>Verifies the constructor rejects a null contentBounds delegate.</summary>
    [Fact]
    public void Constructor_WhenContentBoundsIsNull_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        _ = Should.Throw<ArgumentNullException>(() => new DragBehavior(
            null!,
            () => true,
            () => true,
            () => true,
            () => true,
            () => { },
            _ => { }));
    }

    /// <summary>Verifies the constructor rejects a null setPressed delegate.</summary>
    [Fact]
    public void Constructor_WhenSetPressedIsNull_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        _ = Should.Throw<ArgumentNullException>(() => new DragBehavior(
            () => _bounds,
            () => true,
            () => true,
            () => true,
            () => true,
            () => { },
            null!));
    }

    /// <summary>Verifies a press inside bounds starts the drag.</summary>
    [Fact]
    public void TryStart_WhenInsideBounds_StartsDrag()
    {
        // Arrange
        var drag = CreateBehavior();

        // Act
        var started = drag.TryStart(new Point(5, 5));

        // Assert
        started.ShouldBeTrue();
        drag.IsDragging.ShouldBeTrue();
        _focused.ShouldBeTrue();
        _captured.ShouldBeTrue();
        _pressed.ShouldBeTrue();
    }

    /// <summary>Verifies a press outside bounds does not start the drag.</summary>
    [Fact]
    public void TryStart_WhenOutsideBounds_DoesNotStart()
    {
        // Arrange
        var drag = CreateBehavior();

        // Act
        var started = drag.TryStart(new Point(15, 15));

        // Assert
        started.ShouldBeFalse();
        drag.IsDragging.ShouldBeFalse();
        _pressed.ShouldBeFalse();
    }

    /// <summary>Verifies TryStart returns false when the control is unavailable.</summary>
    [Fact]
    public void TryStart_WhenUnavailable_DoesNotStart()
    {
        // Arrange
        var drag = CreateBehavior(available: false);

        // Act
        var started = drag.TryStart(new Point(5, 5));

        // Assert
        started.ShouldBeFalse();
        drag.IsDragging.ShouldBeFalse();
    }

    /// <summary>Verifies TryStart returns false when capture fails.</summary>
    [Fact]
    public void TryStart_WhenCaptureRefused_DoesNotStart()
    {
        // Arrange
        var drag = new DragBehavior(
            () => _bounds,
            () => true,
            () => true,
            () => false,
            () => false,
            () => { },
            pressed => _pressed = pressed);

        // Act
        var started = drag.TryStart(new Point(5, 5));

        // Assert
        started.ShouldBeFalse();
        drag.IsDragging.ShouldBeFalse();
        _pressed.ShouldBeFalse();
    }

    /// <summary>Verifies Cancel ends drag and releases capture.</summary>
    [Fact]
    public void Cancel_WhenDragging_EndsDragAndReleasesCapture()
    {
        // Arrange
        var drag = CreateBehavior();
        _ = drag.TryStart(new Point(5, 5));

        // Act
        drag.Cancel(releaseCapture: true);

        // Assert
        drag.IsDragging.ShouldBeFalse();
        _pressed.ShouldBeFalse();
        _captured.ShouldBeFalse();
    }

    /// <summary>Verifies Cancel without release preserves capture.</summary>
    [Fact]
    public void Cancel_WhenDraggingWithoutRelease_PreservesCapture()
    {
        // Arrange
        var drag = CreateBehavior();
        _ = drag.TryStart(new Point(5, 5));

        // Act
        drag.Cancel(releaseCapture: false);

        // Assert
        drag.IsDragging.ShouldBeFalse();
        _pressed.ShouldBeFalse();
        _captured.ShouldBeTrue();
    }

    /// <summary>Verifies FocusChanged(false) cancels drag.</summary>
    [Fact]
    public void FocusChanged_WhenLostWhileDragging_CancelsDrag()
    {
        // Arrange
        var drag = CreateBehavior();
        _ = drag.TryStart(new Point(5, 5));

        // Act
        drag.FocusChanged(focused: false);

        // Assert
        drag.IsDragging.ShouldBeFalse();
        _pressed.ShouldBeFalse();
        _captured.ShouldBeFalse();
    }

    /// <summary>Verifies FocusChanged(true) does not cancel drag.</summary>
    [Fact]
    public void FocusChanged_WhenGainedWhileDragging_PreservesDrag()
    {
        // Arrange
        var drag = CreateBehavior();
        _ = drag.TryStart(new Point(5, 5));

        // Act
        drag.FocusChanged(focused: true);

        // Assert
        drag.IsDragging.ShouldBeTrue();
        _pressed.ShouldBeTrue();
    }

    /// <summary>Verifies CaptureLost cancels drag without releasing capture.</summary>
    [Fact]
    public void CaptureLost_WhenDragging_CancelsDrag()
    {
        // Arrange
        var drag = CreateBehavior();
        _ = drag.TryStart(new Point(5, 5));
        _captured = false;

        // Act
        drag.CaptureLost();

        // Assert
        drag.IsDragging.ShouldBeFalse();
        _pressed.ShouldBeFalse();
    }

    /// <summary>Verifies Unavailable cancels drag without releasing capture.</summary>
    [Fact]
    public void Unavailable_WhenDragging_CancelsDrag()
    {
        // Arrange
        var drag = CreateBehavior();
        _ = drag.TryStart(new Point(5, 5));
        _captured = false;

        // Act
        drag.Unavailable();

        // Assert
        drag.IsDragging.ShouldBeFalse();
        _pressed.ShouldBeFalse();
    }
}
