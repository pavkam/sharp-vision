// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents one parsed KDE <c>&lt;context&gt;</c> element: a named highlighting state.</summary>
[PublicAPI]
public sealed class SyntaxContext
{
    /// <summary>Initializes a fully specified, internally validated context.</summary>
    /// <param name="name">The non-null, non-empty context name.</param>
    /// <param name="attributeName">The item-data name applied to text this context matches with no more specific rule attribute, or null.</param>
    /// <param name="lineEndContext">The switch applied when a line ends while this context is active.</param>
    /// <param name="lineEmptyContext">The switch applied when an empty line is encountered.</param>
    /// <param name="fallthroughContext">The switch applied when no rule in this context matches.</param>
    /// <param name="noIndentationBasedFolding">Whether this context is excluded from the definition's indentation-based folding.</param>
    /// <param name="stopEmptyLineContextSwitchLoop">Whether repeated empty-line context switching stops upon reaching this context.</param>
    /// <param name="rules">The non-null, ordered rules tried in this context, first match wins.</param>
    internal SyntaxContext(
        string name,
        string? attributeName,
        SyntaxContextSwitch lineEndContext,
        SyntaxContextSwitch lineEmptyContext,
        SyntaxContextSwitch fallthroughContext,
        bool noIndentationBasedFolding,
        bool stopEmptyLineContextSwitchLoop,
        IReadOnlyList<SyntaxRule> rules)
    {
        Name = name;
        AttributeName = attributeName;
        LineEndContext = lineEndContext;
        LineEmptyContext = lineEmptyContext;
        FallthroughContext = fallthroughContext;
        NoIndentationBasedFolding = noIndentationBasedFolding;
        StopEmptyLineContextSwitchLoop = stopEmptyLineContextSwitchLoop;
        Rules = new SyntaxReadOnlyList<SyntaxRule>(rules);
    }

    /// <summary>Gets the context name, as referenced by a <see cref="SyntaxContextReference"/>.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets the item-data name applied to text this context matches with no more specific rule
    /// attribute, or null.
    /// </summary>
    public string? AttributeName { get; }

    /// <summary>Gets the switch applied when a line ends while this context is active.</summary>
    public SyntaxContextSwitch LineEndContext { get; }

    /// <summary>
    /// Gets the switch applied when an empty line is encountered while this context is active.
    /// The reader resolves the schema's "defaults to <c>lineEndContext</c>" rule before this
    /// property is populated, so it is never itself ambiguous.
    /// </summary>
    public SyntaxContextSwitch LineEmptyContext { get; }

    /// <summary>
    /// Gets the switch applied when no rule in this context matches at the current position. A
    /// <see cref="SyntaxContextSwitch.IsStay"/> value means this context has no fallthrough, and
    /// an unmatched position instead consumes exactly one character with this context's own style.
    /// </summary>
    public SyntaxContextSwitch FallthroughContext { get; }

    /// <summary>
    /// Gets whether this context is excluded from the definition's indentation-based folding.
    /// </summary>
    public bool NoIndentationBasedFolding { get; }

    /// <summary>
    /// Gets whether repeated empty-line context switching stops upon reaching this context rather
    /// than continuing to follow further empty-line switches.
    /// </summary>
    public bool StopEmptyLineContextSwitchLoop { get; }

    /// <summary>Gets the ordered rules tried in this context; the first successful match wins.</summary>
    public IReadOnlyList<SyntaxRule> Rules { get; }
}
