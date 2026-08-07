// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Exposes one style definition's primary-or-secondary kind without its style type.
/// <see cref="StyleDefinition{TStyle}"/> is generic, so the theme catalog - which discovers style
/// types reflectively and holds them only as <see cref="Type"/> - cannot read
/// <c>IsControl</c> off the constructed instance without this non-generic view.</summary>
internal interface IStyleDefinition
{
    /// <summary>Gets whether this definition owns a control's semantic appearance, and therefore a
    /// <c>styles.*</c> theme section. A secondary (part) definition owns neither.</summary>
    public bool IsControl { get; }
}
