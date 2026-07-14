// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

/// <summary>A composable control whose single content child is produced once by <see cref="Build"/>.</summary>
/// <remarks>
/// Derive from <see cref="View"/> to build a reusable component from existing controls. Implement
/// <see cref="Build"/> to return the content root; the runtime installs it as the view's only child
/// on the view's first measure, whether or not the view is attached at that point. This is one-shot
/// construction, not reactive rendering: after <see cref="Build"/> runs, mutate the returned subtree
/// like any other control tree.
/// </remarks>
public abstract class View: Container
{
    private bool _built;

    /// <summary>Initializes an empty capacity-one composable view.</summary>
    protected View() : base(capacity: 1)
    {
    }

    /// <summary>Gets the built content child, or null before <see cref="Build"/> has run.</summary>
    protected Control? Content => Children.Count == 0 ? null : Children[0];

    /// <summary>Produces this view's content root. Called once, on the first layout pass, whether or
    /// not the view is attached. Must return a non-null control; return a layout container for
    /// multiple children.</summary>
    /// <returns>The non-null content root installed as this view's only child.</returns>
    /// <exception cref="InvalidOperationException">This method returned null.</exception>
    protected abstract Control Build();

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureBuilt();

        if (Content is not { } child)
        {
            return default;
        }

        child.Measure(constraint);
        return new Size(
            Add(child.DesiredSize.Width, child.Margin.Horizontal),
            Add(child.DesiredSize.Height, child.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        Content?.Arrange(bounds, widthResolved: true, heightResolved: true);

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }

        Control content = Build() ??
            throw new InvalidOperationException("View.Build must return a non-null control.");
        Children.SetOnly(content);
        _built = true;
    }

    private static int Add(int left, int right)
    {
        long result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }
}
