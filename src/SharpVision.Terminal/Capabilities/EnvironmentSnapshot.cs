// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Copies caller-supplied environment evidence into an owned ordinal snapshot without discarding
/// the caller dictionary's lookup semantics for the keys the library actually reads.
/// </summary>
/// <remarks>
/// A caller may legitimately supply an <see cref="StringComparer.OrdinalIgnoreCase"/> dictionary
/// holding lowercase <c>term</c> or <c>tmux</c>. Copying that straight into an ordinal dictionary
/// would keep the entries but make every later canonical lookup miss, so passive detection and
/// active negotiation would disagree about identical input. Canonicalizing the recognized keys
/// during the copy keeps both paths consistent while still publishing an ordinal snapshot that
/// retains no reference to the mutable caller dictionary.
/// </remarks>
internal static class EnvironmentSnapshot
{
    private static readonly string[] _recognized =
    [
        EvidenceEnvironmentVars.Term,
        EvidenceEnvironmentVars.ColorTerm,
        EvidenceEnvironmentVars.NoColor,
        EvidenceEnvironmentVars.TermProgram,
        EvidenceEnvironmentVars.TermProgramVersion,
        EvidenceEnvironmentVars.Tmux,
        EvidenceEnvironmentVars.Sty,
        EvidenceEnvironmentVars.SshConnection,
        EvidenceEnvironmentVars.SshTty
    ];

    /// <summary>Creates the owned ordinal snapshot for one caller environment.</summary>
    /// <param name="environment">The non-null caller-supplied environment.</param>
    /// <returns>An owned read-only ordinal snapshot with canonical recognized keys.</returns>
    internal static ReadOnlyDictionary<string, string?> Create(
        IReadOnlyDictionary<string, string?> environment)
    {
        Debug.Assert(environment is not null, "Callers validate the environment before copying.");

        var snapshot = new Dictionary<string, string?>(environment.Count, StringComparer.Ordinal);

        foreach (var pair in environment)
        {
            snapshot[pair.Key] = pair.Value;
        }

        // Resolve each recognized key through the caller's own comparer, then republish it under
        // its canonical spelling so later exact lookups find it.
        foreach (var key in _recognized)
        {
            if (environment.TryGetValue(key, out var value))
            {
                snapshot[key] = value;
            }
        }

        return new ReadOnlyDictionary<string, string?>(snapshot);
    }
}
