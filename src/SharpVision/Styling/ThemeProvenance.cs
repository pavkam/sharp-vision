// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Validates theme attribution fields shared by runtime and catalog metadata.</summary>
internal static class ThemeProvenance
{
    private static readonly HashSet<string> _licenses = new(StringComparer.Ordinal)
    {
        "0BSD", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "BSL-1.0", "CC0-1.0",
        "GPL-2.0-only", "GPL-2.0-or-later", "GPL-3.0-only", "GPL-3.0-or-later", "ISC",
        "LGPL-2.1-only", "LGPL-2.1-or-later", "LGPL-3.0-only", "LGPL-3.0-or-later",
        "MIT", "MPL-2.0", "Unlicense", "Zlib"
    };

    /// <summary>Validates one supported SPDX license identifier.</summary>
    internal static string ValidateLicense(string value, string parameterName) => _licenses.Contains(value)
        ? value
        : throw new ArgumentException("The theme license must be a supported SPDX license identifier.", parameterName);

    /// <summary>Validates one absolute HTTP or HTTPS provenance URL.</summary>
    internal static string ValidateSource(string value, string parameterName) => IsSource(value)
        ? value
        : throw new ArgumentException("The theme source must be an absolute HTTP or HTTPS URL.", parameterName);

    /// <summary>Reports whether one value is a supported SPDX license identifier.</summary>
    internal static bool IsLicense(string value) => _licenses.Contains(value);

    /// <summary>Reports whether one value is an absolute HTTP or HTTPS provenance URL.</summary>
    internal static bool IsSource(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" &&
        !string.IsNullOrEmpty(uri.Host);
}
