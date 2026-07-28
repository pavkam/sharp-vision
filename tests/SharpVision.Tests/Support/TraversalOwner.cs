// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides normal, excluded, and popup ownership slots for traversal tests.</summary>
internal sealed class TraversalOwner: Control
{
    private readonly OwnedControlSlot _normal;
    private readonly OwnedControlSlot _excluded;
    private readonly OwnedControlSlot _secondary;
    private readonly OwnedControlSlot _popup;

    internal bool ClipChildren { get; set; } = true;
    protected override bool ClipsChildren => ClipChildren;

    internal TraversalOwner()
    {
        _normal = RegisterOwnedSlot(
            new OwnedControlOptions(OwnedControlRole.FrameworkPart, OwnedControlLayer.Normal, true, true, "normal",
                InvalidationImpact.Measure), int.MaxValue);
        _excluded = RegisterOwnedSlot(
            new OwnedControlOptions(OwnedControlRole.FrameworkPart, OwnedControlLayer.Normal, false, false, "excluded",
                InvalidationImpact.Measure), int.MaxValue);
        _secondary =
            RegisterOwnedSlot(
                new OwnedControlOptions(OwnedControlRole.FrameworkPart, OwnedControlLayer.Normal, true, true,
                    "secondary", InvalidationImpact.Measure), int.MaxValue);
        _popup = RegisterOwnedSlot(
            new OwnedControlOptions(OwnedControlRole.FrameworkPart, OwnedControlLayer.Popup, true, false, "popup",
                InvalidationImpact.Measure), int.MaxValue);
    }

    internal void AddNormal(Control control) => _normal.Add(control);
    internal bool RemoveNormal(Control control) => _normal.Remove(control);
    internal void AddExcluded(Control control) => _excluded.Add(control);
    internal void AddSecondary(Control control) => _secondary.Add(control);
    internal void AddPopup(Control control) => _popup.Add(control);

    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;
        MeasureSlot(_normal, constraint, ref width, ref height);
        MeasureSlot(_excluded, constraint, ref width, ref height);
        MeasureSlot(_secondary, constraint, ref width, ref height);
        return new Size(width, height);
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        ArrangeSlot(_normal, bounds);
        ArrangeSlot(_excluded, bounds);
        ArrangeSlot(_secondary, bounds);
    }

    private static void MeasureSlot(OwnedControlSlot slot, Constraint constraint, ref int width, ref int height)
    {
        for (var index = 0; index < slot.Count; index++)
        {
            slot[index].Measure(constraint);
            width = Math.Max(width, slot[index].DesiredSize.Width);
            height = Math.Max(height, slot[index].DesiredSize.Height);
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
