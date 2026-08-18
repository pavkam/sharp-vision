// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Chooses the currency identity text a <see cref="CurrencyInput"/> composes around its
/// formatted number.</summary>
[PublicAPI]
public enum CurrencyDisplayMode
{
    /// <summary>Uses <see cref="NumberFormatInfo.CurrencySymbol"/> - the
    /// default, always resolvable for every culture including <see cref="CultureInfo.InvariantCulture"/>.</summary>
    Symbol,

    /// <summary>Uses the three-letter ISO 4217 code resolved from
    /// <see cref="RegionInfo.ISOCurrencySymbol"/>, unless
    /// <see cref="CurrencyInput.CurrencyOverride"/> is set.</summary>
    IsoCode,

    /// <summary>Uses the culture-native currency name resolved from
    /// <see cref="RegionInfo.CurrencyNativeName"/>, unless
    /// <see cref="CurrencyInput.CurrencyOverride"/> is set.</summary>
    Name,

    /// <summary>Uses <see cref="CurrencyInput.CurrencyOverride"/> exclusively; the override must be set.</summary>
    Custom
}
