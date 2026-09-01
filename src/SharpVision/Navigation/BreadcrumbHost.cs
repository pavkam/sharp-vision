// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Owns measurement and arrangement of one breadcrumb's semantic item controls.</summary>
internal sealed class BreadcrumbHost: Container
{
    /// <summary>Initializes a private host for an exact breadcrumb owner.</summary>
    /// <param name="owner">The non-null semantic owner.</param>
    internal BreadcrumbHost(Breadcrumb owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
    }

    /// <summary>Gets the breadcrumb whose committed layout this host presents.</summary>
    internal Breadcrumb Owner { get; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;
        var participants = 0;

        foreach (var child in Children)
        {
            var desired = MeasureChild(child, new Constraint(width: null, constraint.Height));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            width = width.Add(desired.Width.Add(child.Margin.Horizontal));
            height = Math.Max(height, desired.Height.Add(child.Margin.Vertical));
            participants++;
        }

        width = width.Add(Math.Max(0, participants - 1));
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var x = bounds.X;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                ArrangeChild(child, default, ResolvedAxes.Both);
                continue;
            }

            var width = child.DesiredSize.Width.Add(child.Margin.Horizontal);
            ArrangeChild(child, new Rect(x, bounds.Y, width, bounds.Height), ResolvedAxes.Both);
            x = x.Add(width).Add(1);
        }
    }
}
