// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Exposes viewport navigation for a selectable-text source.</summary>
/// <remarks>
/// Coordinates and scroll deltas are measured in terminal cells in the source control's local
/// coordinate space. Viewport mutations are dispatcher-affine when the source is attached.
/// </remarks>
[PublicAPI]
public interface ISelectableTextViewport
{
    /// <summary>Gets the currently visible source-local cell rectangle.</summary>
    /// <exception cref="InvalidOperationException">
    /// The attached source is accessed away from its owning dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The viewport owner has been disposed.</exception>
    public Rect SelectableTextViewport { get; }

    /// <summary>Reveals the grapheme containing or beginning at a semantic UTF-16 offset.</summary>
    /// <param name="offset">
    /// A non-negative UTF-16 offset no greater than the current semantic text length. The offset
    /// must be a complete grapheme boundary.
    /// </param>
    /// <returns>True when the viewport changed; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is negative or exceeds the current semantic text length.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="offset"/> splits an extended grapheme cluster.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The attached source is mutated away from its owning dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The viewport owner has been disposed.</exception>
    public bool RevealSelectableTextOffset(int offset);

    /// <summary>Scrolls the viewport by signed horizontal and vertical cell deltas.</summary>
    /// <param name="horizontal">The signed number of columns to scroll.</param>
    /// <param name="vertical">The signed number of rows to scroll.</param>
    /// <returns>True when either effective viewport offset changed; otherwise false.</returns>
    /// <exception cref="InvalidOperationException">
    /// The attached source is mutated away from its owning dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The viewport owner has been disposed.</exception>
    public bool ScrollSelectableTextViewport(int horizontal, int vertical);
}
