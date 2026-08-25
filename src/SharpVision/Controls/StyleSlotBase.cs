// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Provides the non-generic framework registration shared by style slots.</summary>
internal abstract class StyleSlotBase
{
    /// <summary>Restricts style-slot creation to <see cref="ControlBase"/>.</summary>
    protected StyleSlotBase()
    {
    }

    /// <summary>Gets the control that owns this slot.</summary>
    internal abstract ControlBase Owner { get; }

    /// <summary>Gets the typed public slot object.</summary>
    internal abstract object Slot { get; }

    /// <summary>Gets the registered local-property name.</summary>
    internal abstract string PropertyName { get; }

    /// <summary>Gets the registered resolved-property name.</summary>
    internal abstract string ActualPropertyName { get; }

    /// <summary>Gets whether this slot owns the control's primary appearance.</summary>
    internal abstract bool OwnsAppearance { get; }

    /// <summary>Resolves the primary appearance for one Theme.</summary>
    internal abstract AppearanceStates GetAppearance(Theme? theme);

    /// <summary>Calculates this slot's exact Theme-transition impact.</summary>
    internal abstract InvalidationImpact GetThemeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace);

    /// <summary>Gets the resolved property changed by one Theme transition.</summary>
    internal abstract string? GetThemeResolvedProperty(Theme? previous, Theme? current);

    /// <summary>Clears the memoized resolved value.</summary>
    internal abstract void ClearResolvedCache();

    /// <summary>Releases every upstream and downstream binding edge.</summary>
    internal abstract void DisposeBindings();

    /// <summary>Releases an upstream edge that no longer follows retained ancestry.</summary>
    internal abstract void ReleaseInvalidBinding();
}
