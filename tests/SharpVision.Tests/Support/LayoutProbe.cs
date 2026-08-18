// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>A container probe that measures the union of its children and arranges each child to its slot.</summary>
internal sealed class LayoutProbe: Container
{
    /// <summary>Initializes a probe with an optional child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal LayoutProbe(int capacity = int.MaxValue) : base(capacity) =>
        EnableChromeAuthoring();

    /// <summary>Routes one keyboard press through the routed-event default path.</summary>
    /// <param name="code">The pressed key code.</param>
    internal void RaiseKey(Code code) =>
        Router.Route(
            this,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

    /// <summary>Routes one wheel pointer event through the routed-event default path.</summary>
    /// <param name="wheelX">The horizontal wheel delta.</param>
    /// <param name="wheelY">The vertical wheel delta.</param>
    internal void RaiseWheel(int wheelX, int wheelY) =>
        Router.Route(
            this,
            Events.Pointer,
            new PointerEventArgs(new Pointer(
                cells: default,
                pixels: null,
                Buttons.None,
                PointerAction.Wheel,
                wheelX,
                wheelY,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false)));

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;

        foreach (var child in Children)
        {
            child.Measure(constraint);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        foreach (var child in Children)
        {
            child.Arrange(bounds);
        }
    }
}
