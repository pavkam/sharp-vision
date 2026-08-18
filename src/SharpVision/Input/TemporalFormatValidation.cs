// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Shared format-pattern validation for the segmented temporal input controls: each
/// rejects a format pattern its own value type cannot render at the property boundary, instead
/// of throwing later from the layout pass where it would escape as an unhandled exception.</summary>
internal static class TemporalFormatValidation
{
    /// <exception cref="ArgumentException">The format cannot be rendered by <paramref name="typeName"/>
    /// under <paramref name="culture"/>.</exception>
    public static void Validate(
        string format,
        CultureInfo culture,
        string paramName,
        string typeName,
        Func<string, CultureInfo, string> render)
    {
        try
        {
            _ = render(format, culture);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException(
                $"The format \"{format}\" cannot be rendered by a {typeName} value.",
                paramName,
                exception);
        }
    }
}
