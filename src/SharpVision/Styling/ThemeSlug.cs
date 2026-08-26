// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Validates portable lowercase theme identifiers shared by public and document models.</summary>
internal static class ThemeSlug
{
    /// <summary>Validates a lowercase kebab-case theme slug for an argument boundary.</summary>
    /// <param name="value">The nonblank slug.</param>
    /// <param name="parameterName">The public parameter name.</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not lowercase kebab case.</exception>
    internal static string Validate(string value, string parameterName) => IsValid(value)
        ? value
        : throw new ArgumentException(
            "A theme slug must use lowercase ASCII letters and digits separated by single hyphens.",
            parameterName);

    /// <summary>Reports whether a value is lowercase kebab case without allocating.</summary>
    /// <param name="value">The candidate slug.</param>
    /// <returns>Whether the value is a portable theme identifier.</returns>
    internal static bool IsValid(string value)
    {
        var previousWasHyphen = true;

        foreach (var character in value)
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                previousWasHyphen = false;
                continue;
            }

            if (character != '-' || previousWasHyphen)
            {
                return false;
            }

            previousWasHyphen = true;
        }

        return !previousWasHyphen;
    }
}
