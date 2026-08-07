// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

/// <summary>Stacks children vertically with standard showcase spacing.</summary>
internal sealed class DocColumn: CompositeControlBase
{
    private readonly Stack _column;

    /// <summary>Initializes a vertical column of showcase specimens.</summary>
    /// <param name="children">The children in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> or one of its entries is null.</exception>
    internal DocColumn(params ControlBase[] children)
    {
        ArgumentNullException.ThrowIfNull(children);

        _column = new Stack { Spacing = 1 };

        foreach (var child in children)
        {
            ArgumentNullException.ThrowIfNull(child);
            _column.Children.Add(child);
        }

        InitializeContent(_column);
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
            _column.Face = value;
        }
    }
}
