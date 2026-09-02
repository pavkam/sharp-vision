// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Owns and lays out one command bar's semantic primary-plane entries.</summary>
internal sealed class CommandBarHost: Container
{
    private readonly CommandBar _owner;

    /// <summary>Initializes the permanent presentation host for one owner.</summary>
    /// <param name="owner">The non-null command bar that computes the active primary snapshot.</param>
    internal CommandBarHost(CommandBar owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Face = ControlStyle.Default.Face;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;
        var count = 0;

        foreach (var child in Children)
        {
            child.Measure(new Constraint(width: null, constraint.Height));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            width = width.Add(child.DesiredSize.Width).Add(child.Margin.Horizontal);
            height = Math.Max(height, child.DesiredSize.Height.Add(child.Margin.Vertical));
            count++;
        }

        width = width.Add(LayoutMath.GapExtent(_owner.Spacing, count, int.MaxValue));
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var position = bounds.X;
        var arranged = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed || !_owner.IsPrimaryEntry(child))
            {
                ArrangeChild(child, new Rect(bounds.X, bounds.Y, 0, 0), ResolvedAxes.Both);
                continue;
            }

            if (arranged > 0)
            {
                position = position.Add(_owner.Spacing);
            }

            var remaining = Math.Max(0, bounds.Right - position);
            var width = Math.Min(remaining, child.DesiredSize.Width.Add(child.Margin.Horizontal));
            ArrangeChild(child, new Rect(position, bounds.Y, width, bounds.Height), ResolvedAxes.Both);
            position = position.Add(width);
            arranged++;
        }
    }
}
