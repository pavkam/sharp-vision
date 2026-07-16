// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Drives a mounted component with real SGR terminal pointer reports.</summary>
internal sealed class ComponentPointer
{
    private readonly ComponentSurface _surface;
    private Point? _lastPoint;

    /// <summary>Initializes a pointer driver for one non-null mounted surface.</summary>
    /// <param name="surface">The owning component surface.</param>
    /// <exception cref="ArgumentNullException"><paramref name="surface"/> is null.</exception>
    internal ComponentPointer(ComponentSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
    }

    /// <summary>Moves the pointer to the current arranged center of an owned control.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <returns>A task completed after hover and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    internal async Task MoveToAsync(Control control)
    {
        var point = await _surface.ResolvePointAsync(control);
        var value = Encode(button: 35, point, final: 'M');
        await _surface.SendAsync(value, $"move pointer to {point}");
        _lastPoint = point;
    }

    /// <summary>Holds the primary pointer button at the most recently moved-to point.</summary>
    /// <returns>A task completed after focus, capture, pressed state, and rendering settle.</returns>
    /// <exception cref="InvalidOperationException">The pointer has not been positioned.</exception>
    internal async Task PressAsync()
    {
        var point = _lastPoint ?? throw new InvalidOperationException(
            "Move the component pointer before pressing a button.");
        await _surface.SendAsync(Encode(button: 0, point, final: 'M'), $"press primary pointer at {point}");
    }

    /// <summary>Releases the primary pointer button at the most recently moved-to point.</summary>
    /// <returns>A task completed after capture release, activation, and rendering settle.</returns>
    /// <exception cref="InvalidOperationException">The pointer has not been positioned.</exception>
    internal async Task ReleaseAsync()
    {
        var point = _lastPoint ?? throw new InvalidOperationException(
            "Move the component pointer before releasing a button.");
        await _surface.SendAsync(Encode(button: 0, point, final: 'm'), $"release primary pointer at {point}");
    }

    /// <summary>Moves to and clicks the current arranged center of an owned control.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <returns>A task completed after move, press, release, activation, and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    internal async Task ClickAsync(Control control)
    {
        await MoveToAsync(control);
        await PressAsync();
        await ReleaseAsync();
    }

    private static byte[] Encode(int button, Point point, char final) =>
        Encoding.ASCII.GetBytes(
            FormattableString.Invariant($"\u001b[<{button};{point.X + 1};{point.Y + 1}{final}"));
}
