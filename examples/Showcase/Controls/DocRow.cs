// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

/// <summary>Stacks children horizontally with standard showcase spacing.</summary>
internal sealed class DocRow: CompositeControlBase
{
    private readonly Stack _row;

    /// <summary>Initializes a horizontal row of showcase specimens.</summary>
    /// <param name="children">The children in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> or one of its entries is null.</exception>
    internal DocRow(params ControlBase[] children)
    {
        ArgumentNullException.ThrowIfNull(children);

        _row = new Stack { Orientation = Orientation.Horizontal, Spacing = 2 };

        foreach (var child in children)
        {
            ArgumentNullException.ThrowIfNull(child);
            _row.Children.Add(child);
        }

        InitializeContent(_row);
    }

    /// <summary>Gets or sets the complete local face, forwarded to the owned inner <see cref="Stack"/>
    /// so the surface that actually paints matches this wrapper's appearance instead of the private
    /// Stack's own opaque theme background.</summary>
    internal new Face Face
    {
        get => base.Face;
        set
        {
            base.Face = value;
            _row.Face = value;
        }
    }

    /// <summary>Gets or sets the non-negative cells between children, forwarded to the owned inner
    /// <see cref="Stack"/> so callers can override the standard showcase spacing.</summary>
    internal int Spacing
    {
        get => _row.Spacing;
        set => _row.Spacing = value;
    }
}
