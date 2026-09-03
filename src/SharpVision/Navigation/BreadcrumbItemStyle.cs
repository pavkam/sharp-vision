// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable breadcrumb-entry presentation. It uses the standard
/// borderless interactive-row theme role so hover, active, current, focus, and disabled states
/// remain consistent with other navigation controls.</summary>
[PublicAPI]
public sealed record BreadcrumbItemStyle: ControlStyle
{
    /// <summary>Gets the breadcrumb-item style definition.</summary>
    internal static StyleDefinition<BreadcrumbItemStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetInteractiveRowStyleSet(),
        Complete,
        static (previous, _, current, _) =>
            previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

    private static BreadcrumbItemStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(control.Face, control.Border, control.Shadow);

    /// <summary>Initializes a complete breadcrumb-entry presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    [SetsRequiredMembers]
    public BreadcrumbItemStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
    {
    }

    /// <summary>Gets the standard borderless interactive-row presentation.</summary>
    public static new BreadcrumbItemStyle Default =>
        Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);
}
