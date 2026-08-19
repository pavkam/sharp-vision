// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Layout;

using SharpVision.Surfaces;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>Adapts one explicit Overlay or application Screen as a temporary presentation owner.</summary>
internal sealed class PresentationHost
{
    private readonly ControlBase _owner;

    private PresentationHost(ControlBase owner)
    {
        Debug.Assert(owner is Overlay or Screen, "A presentation host is an Overlay or Screen.");
        _owner = owner;
    }

    /// <summary>Resolves the supplied explicit Overlay, owning Screen, or outermost fallback Overlay.</summary>
    /// <param name="owner">The non-null control whose retained ancestry is searched.</param>
    /// <returns>The resolved host, or null when no supported owner exists.</returns>
    [MustUseReturnValue]
    public static PresentationHost? Resolve(ControlBase owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (owner is Overlay explicitHost)
        {
            return new PresentationHost(explicitHost);
        }

        Overlay? fallback = null;

        for (var current = owner; current is not null; current = current.Parent)
        {
            if (current is Screen screen)
            {
                return new PresentationHost(screen);
            }

            if (current is Overlay overlay)
            {
                fallback = overlay;
            }
        }

        return fallback is null ? null : new PresentationHost(fallback);
    }

    /// <summary>Adds one detached temporary surface to this host.</summary>
    /// <param name="surface">The non-null detached surface.</param>
    internal void Add(FloatingSurfaceBase surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        switch (_owner)
        {
            case Screen screen:
                screen.AddPresentation(surface);
                break;
            case Overlay overlay:
                overlay.Children.Add(surface);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Returns whether this host directly owns the supplied control.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>True when the candidate's committed parent is this host.</returns>
    [Pure]
    public bool Owns(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _owner switch
        {
            Screen screen => screen.OwnsPresentation(control),
            Overlay overlay => ReferenceEquals(control.Parent, overlay),
            _ => throw new UnreachableException()
        };
    }

    /// <summary>Removes one identical temporary surface without disposing it.</summary>
    /// <param name="control">The non-null owned surface.</param>
    /// <returns>True when the surface was removed.</returns>
    internal bool Remove(FloatingSurfaceBase control) => _owner switch
    {
        Screen screen => screen.RemovePresentation(control),
        Overlay overlay => overlay.Children.Remove(control),
        _ => throw new UnreachableException()
    };

}
