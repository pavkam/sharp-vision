// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a leaf whose desired size depends on its measure width, mimicking a
/// wrapping control for tests that must distinguish content measured at one width from
/// content measured at another.</summary>
internal sealed class WrappingProbeControl: ControlBase
{
    private readonly int _wideWidth;
    private readonly int _narrowWidth;

    /// <summary>Initializes a probe that reports one line at or above the wide width and two
    /// lines otherwise.</summary>
    /// <param name="wideWidth">The minimum constraint width that fits content on one line.</param>
    /// <param name="narrowWidth">The reported width when the constraint forces wrapping.</param>
    internal WrappingProbeControl(int wideWidth, int narrowWidth)
    {
        _wideWidth = wideWidth;
        _narrowWidth = narrowWidth;
    }

    /// <summary>Gets constraints received by the measure extension point.</summary>
    internal List<Constraint> MeasureConstraints { get; } = [];

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        MeasureConstraints.Add(constraint);

        return constraint.Width is { } width && width < _wideWidth
            ? new Size(Math.Min(width, _narrowWidth), 2)
            : new Size(_wideWidth, 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
    }
}
