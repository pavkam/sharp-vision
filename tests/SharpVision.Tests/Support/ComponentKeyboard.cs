// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Drives a mounted component with real terminal keyboard bytes.</summary>
internal sealed class ComponentKeyboard
{
    private readonly ComponentSurface _surface;

    /// <summary>Initializes a keyboard driver for one non-null mounted surface.</summary>
    /// <param name="surface">The owning component surface.</param>
    /// <exception cref="ArgumentNullException"><paramref name="surface"/> is null.</exception>
    internal ComponentKeyboard(ComponentSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
    }

    /// <summary>Presses one supported key through its real terminal encoding.</summary>
    /// <param name="code">The defined key code to press.</param>
    /// <returns>A task completed after routed key behavior and rendering settle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="code"/> is undefined.</exception>
    /// <exception cref="NotSupportedException"><paramref name="code"/> has no component-driver encoding yet.</exception>
    internal Task PressAsync(Code code)
    {
        return !Enum.IsDefined(code)
            ? throw new ArgumentOutOfRangeException(nameof(code), code, "The keyboard code is undefined.")
            : code == Code.Tab
                ? _surface.SendAsync("\t"u8.ToArray(), "press Tab")
                : throw new NotSupportedException($"Component keyboard encoding for {code} is not supported.");
    }
}
