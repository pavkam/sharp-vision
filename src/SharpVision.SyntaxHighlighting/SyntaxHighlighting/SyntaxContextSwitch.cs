// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Represents one parsed KDE context-switch specification, the small language used by
/// <c>lineEndContext</c>, <c>lineEmptyContext</c>, <c>fallthroughContext</c>, and every rule's own
/// <c>context</c> attribute.
/// </summary>
/// <remarks>
/// <para>
/// The specification is a sequence of zero or more <c>#pop</c> tokens, optionally followed by one
/// or more <c>!</c>-separated target context names (each of which may itself carry a
/// <c>##OtherDefinition</c> suffix). <c>#stay</c> and an empty specification both mean "make no
/// change." A bare context name with no leading <c>#pop</c> pushes that context. <c>#pop</c> alone
/// pops without pushing; <c>#pop!Name</c> pops once then pushes <c>Name</c>; <c>#pop!A!B</c> pops
/// once then pushes <c>A</c> and then <c>B</c>. This grammar, including the exclamation point's
/// dual role as both the pop/push separator and the push/push separator, matches
/// <c>ContextSwitch::resolve</c> in the upstream KSyntaxHighlighting engine exactly, since the KDE
/// XML format's own documentation does not spell out the multi-push form.
/// </para>
/// </remarks>
[PublicAPI]
public readonly record struct SyntaxContextSwitch
{
#pragma warning disable IDE0032 // Default structs need null-coalescing getters over nullable backing storage.
    private readonly IReadOnlyList<SyntaxContextReference>? _targets;
#pragma warning restore IDE0032

    /// <summary>The specification meaning "make no change."</summary>
    internal static readonly SyntaxContextSwitch Stay = new(0, []);

    /// <summary>Initializes a resolved context switch.</summary>
    /// <param name="popCount">The non-negative number of contexts to pop.</param>
    /// <param name="targets">
    /// The non-null, possibly empty ordered sequence of contexts to push after popping.
    /// </param>
    internal SyntaxContextSwitch(int popCount, IReadOnlyList<SyntaxContextReference> targets)
    {
        PopCount = popCount;
        _targets = new SyntaxReadOnlyList<SyntaxContextReference>(targets);
    }

    /// <summary>Gets the number of contexts to pop before pushing <see cref="Targets"/>.</summary>
    public int PopCount { get; }

    /// <summary>Gets the ordered contexts to push, in push order, after popping.</summary>
    public IReadOnlyList<SyntaxContextReference> Targets => _targets ?? SyntaxReadOnlyList<SyntaxContextReference>.Empty;

    /// <summary>Gets whether this switch changes the context stack at all.</summary>
    public bool IsStay => PopCount == 0 && Targets.Count == 0;

    /// <summary>Parses one context-switch specification.</summary>
    /// <param name="specification">
    /// The raw attribute text, or null/empty (equivalent to <c>#stay</c>).
    /// </param>
    /// <returns>The resolved switch.</returns>
    internal static SyntaxContextSwitch Parse(string? specification)
    {
        if (string.IsNullOrEmpty(specification) || specification == "#stay")
        {
            return Stay;
        }

        var remaining = specification;
        var popCount = 0;

        while (remaining.StartsWith("#pop", StringComparison.Ordinal))
        {
            popCount++;

            if (remaining.Length > 4 && remaining[4] == '!')
            {
                remaining = remaining[5..];
                break;
            }

            remaining = remaining[4..];
        }

        if (remaining.Length == 0)
        {
            return new SyntaxContextSwitch(popCount, []);
        }

        var targets = new List<SyntaxContextReference>();

        foreach (var part in remaining.Split('!'))
        {
            if (part.Length == 0)
            {
                continue;
            }

            var separatorIndex = part.IndexOf("##", StringComparison.Ordinal);
            targets.Add(
                separatorIndex < 0
                    ? new SyntaxContextReference(part, null)
                    : new SyntaxContextReference(part[..separatorIndex], part[(separatorIndex + 2)..]));
        }

        return new SyntaxContextSwitch(popCount, targets);
    }
}
