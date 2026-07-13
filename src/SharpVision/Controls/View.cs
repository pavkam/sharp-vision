// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

/// <summary>A composable control whose single content child is produced once by <see cref="Build"/>.</summary>
/// <remarks>
/// Derive from <see cref="View"/> to build a reusable component from existing controls. Implement
/// <see cref="Build"/> to return the content root; the runtime installs it as the view's only child
/// the first time the view is measured after attachment. This is one-shot construction, not reactive
/// rendering: after <see cref="Build"/> runs, mutate the returned subtree like any other control tree.
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

    /// <summary>Produces this view's content root. Called once, after attachment and before the first
    /// layout pass. Must return a non-null control; return a layout container for multiple children.</summary>
    /// <returns>The non-null content root installed as this view's only child.</returns>
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
            child.DesiredSize.Width + child.Margin.Horizontal,
            child.DesiredSize.Height + child.Margin.Vertical);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        Content?.Arrange(bounds, widthResolved: true, heightResolved: true);

    // Build lazily, only while attached, so detached trees stay inert and the content tree can read
    // attached context (dispatcher, and for Screen the running Application). Measure only runs on an
    // attached, laid-out tree, so this is the first point where building is both safe and meaningful.
    private void EnsureBuilt()
    {
        if (_built || Dispatcher is null)
        {
            return;
        }

        Control content = Build() ??
            throw new InvalidOperationException("View.Build must return a non-null control.");
        Children.SetOnly(content);
        _built = true;
    }
}
