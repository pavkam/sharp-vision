// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides normal, excluded, and popup ownership slots without deriving from Container.</summary>
internal sealed class TraversalOwner: Control, IStyleScope
{
    private readonly OwnedControlSlot _normal;
    private readonly OwnedControlSlot _excluded;
    private readonly OwnedControlSlot _secondary;
    private readonly OwnedControlSlot _popup;

    /// <summary>Gets or sets whether ordinary descendants are clipped to this owner's bounds.</summary>
    internal bool ClipChildren { get; set; } = true;

    /// <inheritdoc/>
    protected override bool OwnsPointerState => CanFocus;

    /// <inheritdoc/>
    protected override bool ClipsChildren => ClipChildren;

    /// <summary>Initializes deterministic slots in normal, excluded, then popup registration order.</summary>
    internal TraversalOwner()
    {
        _normal = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "normal",
                ChangeImpact.Measure),
            int.MaxValue);
        _excluded = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: false,
                participatesInNavigation: false,
                partKey: "excluded",
                ChangeImpact.Measure),
            int.MaxValue);
        _secondary = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "secondary",
                ChangeImpact.Measure),
            int.MaxValue);
        _popup = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "popup",
                ChangeImpact.Measure),
            int.MaxValue);
    }

    /// <summary>Adds one detached control to the ordinary interactive slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    internal void AddNormal(Control control) => _normal.Add(control);

    /// <summary>Removes one identical control from the ordinary interactive slot.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>Whether the control was present.</returns>
    internal bool RemoveNormal(Control control) => _normal.Remove(control);

    /// <summary>Adds one detached control to the ordinary non-interactive slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    internal void AddExcluded(Control control) => _excluded.Add(control);

    /// <summary>Adds one detached control to the later ordinary interactive slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    internal void AddSecondary(Control control) => _secondary.Add(control);

    /// <summary>Adds one detached control to the elevated popup slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    internal void AddPopup(Control control) => _popup.Add(control);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;
        MeasureSlot(_normal, constraint, ref width, ref height);
        MeasureSlot(_excluded, constraint, ref width, ref height);
        MeasureSlot(_secondary, constraint, ref width, ref height);
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ArrangeSlot(_normal, bounds);
        ArrangeSlot(_excluded, bounds);
        ArrangeSlot(_secondary, bounds);
    }

    private static void MeasureSlot(
        OwnedControlSlot slot,
        Constraint constraint,
        ref int width,
        ref int height)
    {
        for (var index = 0; index < slot.Count; index++)
        {
            var child = slot[index];
            child.Measure(constraint);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
    }

    private static void ArrangeSlot(OwnedControlSlot slot, Rect bounds)
    {
        for (var index = 0; index < slot.Count; index++)
        {
            slot[index].Arrange(bounds);
        }
    }
}
