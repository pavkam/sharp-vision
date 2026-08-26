// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Adapts one public typed slot to the framework's non-generic registry.</summary>
/// <typeparam name="TStyle">The immutable complete style value.</typeparam>
internal sealed class StyleSlotRegistration<TStyle>: StyleSlotBase
    where TStyle : ControlStyle
{
    private readonly StyleSlot<TStyle> _slot;

    /// <summary>Initializes a registry adapter for one typed slot.</summary>
    /// <param name="slot">The non-null typed slot.</param>
    internal StyleSlotRegistration(StyleSlot<TStyle> slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        _slot = slot;
    }

    internal override ControlBase Owner => _slot.Owner;

    internal override object Slot => _slot;

    internal override string PropertyName => _slot.PropertyName;

    internal override string ActualPropertyName => _slot.ActualPropertyName;

    internal override bool OwnsAppearance => _slot.OwnsAppearance;

    internal override AppearanceStates GetAppearance(Theme? theme) => _slot.GetAppearance(theme);

    internal override InvalidationImpact GetThemeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        _slot.GetThemeImpact(previous, current, previousParentAmbientFace, currentParentAmbientFace);

    internal override string? GetThemeResolvedProperty(Theme? previous, Theme? current) =>
        _slot.GetThemeResolvedProperty(previous, current);

    internal override void PublishThemeChanged(Theme? previous, Theme? current) =>
        _slot.PublishThemeChanged(previous, current);

    internal override void ClearResolvedCache() => _slot.ClearResolvedCache();

    internal override void DisposeBindings() => _slot.DisposeBindings();

    internal override void ReleaseInvalidBinding() => _slot.ReleaseInvalidBinding();
}
