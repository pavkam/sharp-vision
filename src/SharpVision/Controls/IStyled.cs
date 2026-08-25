// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Declares that a control or surface exposes one typed primary complete-style slot.
/// A conforming type implements the required <see cref="Style"/> and <see cref="ActualStyle"/>
/// members as forwarders over a private <see cref="StyleSlot{TStyle}"/> initialized through
/// <c>InitializeStyle</c>.</summary>
/// <typeparam name="TStyle">The small immutable complete style value.</typeparam>
[PublicAPI]
public interface IStyled<TStyle>
    where TStyle : ControlStyle
{
    /// <summary>Gets or sets the complete local style, or null to use the definition fallback.</summary>
    /// <exception cref="InvalidOperationException">The attached instance is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The instance is disposed.</exception>
    public TStyle? Style { get; set; }

    /// <summary>Gets the cached complete local, Theme-owned, or fallback style.</summary>
    public TStyle ActualStyle { get; }
}
