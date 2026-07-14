// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using SharpVision.Terminal.Input;

using KeyAction = Terminal.Input.Action;

/// <summary>A container probe that measures the union of its children and arranges each child to its slot.</summary>
internal sealed class LayoutProbe: Container
{
    /// <summary>Initializes a probe with an optional child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal LayoutProbe(int capacity = int.MaxValue) : base(capacity)
    {
    }

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

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        int width = 0;
        int height = 0;

        foreach (Control child in Children)
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
        foreach (Control child in Children)
        {
            child.Arrange(bounds);
        }
    }
}
