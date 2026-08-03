// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>Owns one bounded iTerm2 OSC 1337 Capabilities feature-reporting reply.</summary>
[PublicAPI]
public sealed class ItermCapabilitiesResponse
{
    /// <summary>Initializes one validated immutable response.</summary>
    /// <param name="hasFileCode">Whether the reply's feature string contains the bare "F" code.</param>
    internal ItermCapabilitiesResponse(bool hasFileCode) => HasFileCode = hasFileCode;

    /// <summary>
    /// Gets whether the reply's feature string contains the bare boolean code "F". The published
    /// iTerm2 feature-reporting table (see docs/protocols/iterm2.md) assigns code "F" to both
    /// FILE and FOCUS_REPORTING, so this flag alone cannot prove multipart file support — it is
    /// corroborated, not disambiguated, by the TERM_PROGRAM_VERSION narrowing that
    /// <c>QueryEvidenceAdapter</c> applies before iTerm2 evidence can ever authorize output.
    /// </summary>
    public bool HasFileCode { get; }
}
