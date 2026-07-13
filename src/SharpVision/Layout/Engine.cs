// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;


/// <summary>Runs guarded deterministic measure and arrange transactions.</summary>
/// <remarks>
/// One engine may lay out many roots sequentially. It is intentionally not
/// thread-safe because an attached tree is owned by exactly one dispatcher.
/// </remarks>
public sealed class Engine
{
    private bool IsActive { get; set; }

    /// <summary>Measures and arranges one root in a zero-origin terminal-cell viewport.</summary>
    /// <param name="root">The non-null root control.</param>
    /// <param name="size">The non-negative viewport size.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Layout is reentered or the attached root is accessed off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException"><paramref name="root"/> is disposed.</exception>
    public void Layout(Control root, Size size)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (IsActive)
        {
            throw new InvalidOperationException("A layout transaction cannot be reentered.");
        }

        IsActive = true;

        try
        {
            root.Measure(new Constraint(size.Width, size.Height));
            root.Arrange(new Rect(0, 0, size.Width, size.Height));
        }
        finally
        {
            IsActive = false;
        }
    }
}
