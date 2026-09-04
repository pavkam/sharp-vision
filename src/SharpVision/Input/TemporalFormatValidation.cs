// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using JetBrains.Annotations;

/// <summary>Shared format-pattern validation for the segmented temporal input controls: each
/// rejects a format pattern its own value type cannot render at the property boundary, instead
/// of throwing later from the layout pass where it would escape as an unhandled exception.</summary>
internal static class TemporalFormatValidation
{
    /// <param name="format">The candidate format pattern.</param>
    /// <param name="culture">The culture the pattern would be rendered under.</param>
    /// <param name="paramName">The name of the property setter argument to report a failure against.</param>
    /// <param name="typeName">The value type's display name, used in the thrown message.</param>
    /// <param name="render">Formats a probe value against a candidate pattern, typically <c>value.ToString(format, culture)</c>.</param>
    /// <param name="tokenKinds">The owning control's own map of recognized pattern letters, used to
    /// additionally reject a pattern that <paramref name="render"/> would accept but the segmented
    /// layout cannot: .NET only rejects a bare custom <c>f</c>/<c>F</c> run longer than seven
    /// characters, and a percent-escaped run (<c>%f</c> followed by more <c>f</c> characters) of any
    /// length renders without complaint, so <see cref="TemporalPatternSegmenter.ParseTokens"/> and
    /// <see cref="TemporalSegmentClassification.DigitCapacity"/> re-check each parsed token's
    /// declared width against the same seven-digit cap <see cref="SegmentDescriptor"/> enforces.</param>
    /// <exception cref="ArgumentException">The format cannot be rendered by <paramref name="typeName"/>
    /// under <paramref name="culture"/>, or declares an editable run wider than the segmented layout
    /// can represent.</exception>
    public static void Validate(
        string format,
        CultureInfo culture,
        string paramName,
        string typeName,
        [InstantHandle] Func<string, CultureInfo, string> render,
        IReadOnlyDictionary<char, TemporalSegmentKind> tokenKinds)
    {
        try
        {
            _ = render(format, culture);

            foreach (var token in TemporalPatternSegmenter.ParseTokens(format, tokenKinds, culture))
            {
                if (TemporalSegmentClassification.DigitCapacity(token) > 7)
                {
                    throw new ArgumentException(
                        $"The format \"{format}\" cannot be rendered by a {typeName} value.",
                        paramName);
                }
            }
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
