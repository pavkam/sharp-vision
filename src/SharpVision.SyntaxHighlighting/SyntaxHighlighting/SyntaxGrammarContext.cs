// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Represents one <see cref="SyntaxContext"/> after grammar compilation: every
/// <c>IncludeRules</c> rule has been spliced away in favor of its target's own compiled rules, and
/// every context-switch target is resolved to a concrete <see cref="SyntaxGrammar"/> and index.
/// </summary>
[PublicAPI]
public sealed class SyntaxGrammarContext
{
    /// <summary>Initializes a compiled context.</summary>
    /// <param name="name">The non-null context name.</param>
    /// <param name="attributeStyle">The style role applied to text matched with no more specific rule attribute.</param>
    /// <param name="rules">The non-null, ordered, fully spliced rules; first match wins.</param>
    /// <param name="lineEndTarget">The resolved switch applied when a line ends.</param>
    /// <param name="lineEmptyTarget">The resolved switch applied when an empty line is encountered.</param>
    /// <param name="fallthroughTarget">The resolved switch applied when no rule matches.</param>
    /// <param name="indentationBasedFoldingEnabled">
    /// Whether this context participates in indentation-based folding.
    /// </param>
    /// <param name="stopEmptyLineContextSwitchLoop">
    /// Whether repeated empty-line context switching stops upon reaching this context.
    /// </param>
    internal SyntaxGrammarContext(
        string name,
        SyntaxDefaultStyle attributeStyle,
        IReadOnlyList<SyntaxCompiledRule> rules,
        SyntaxContextTarget lineEndTarget,
        SyntaxContextTarget lineEmptyTarget,
        SyntaxContextTarget fallthroughTarget,
        bool indentationBasedFoldingEnabled,
        bool stopEmptyLineContextSwitchLoop)
    {
        Name = name;
        AttributeStyle = attributeStyle;
        Rules = rules;
        LineEndTarget = lineEndTarget;
        LineEmptyTarget = lineEmptyTarget;
        FallthroughTarget = fallthroughTarget;
        IndentationBasedFoldingEnabled = indentationBasedFoldingEnabled;
        StopEmptyLineContextSwitchLoop = stopEmptyLineContextSwitchLoop;
    }

    /// <summary>Builds the empty placeholder used when a cyclic reference prevents full resolution.</summary>
    /// <param name="name">The context name to preserve for diagnostics.</param>
    /// <returns>A context with no rules and no context switches, matching upstream's own
    /// "cyclic dependency" warn-and-stop behavior instead of an unbounded recursion.</returns>
    internal static SyntaxGrammarContext Empty(string name) =>
        new(name, SyntaxDefaultStyle.Normal, [], SyntaxContextTarget.Stay, SyntaxContextTarget.Stay, SyntaxContextTarget.Stay, false, false);

    /// <summary>Gets the context name.</summary>
    public string Name { get; }

    /// <summary>Gets the style role applied to text matched with no more specific rule attribute.</summary>
    public SyntaxDefaultStyle AttributeStyle { get; }

    /// <summary>Gets the ordered, fully spliced rules; the first successful match wins.</summary>
    public IReadOnlyList<SyntaxCompiledRule> Rules { get; }

    /// <summary>Gets the resolved switch applied when a line ends while this context is active.</summary>
    public SyntaxContextTarget LineEndTarget { get; }

    /// <summary>Gets the resolved switch applied when an empty line is encountered.</summary>
    public SyntaxContextTarget LineEmptyTarget { get; }

    /// <summary>Gets the resolved switch applied when no rule in this context matches.</summary>
    public SyntaxContextTarget FallthroughTarget { get; }

    /// <summary>Gets whether this context participates in indentation-based folding.</summary>
    public bool IndentationBasedFoldingEnabled { get; }

    /// <summary>
    /// Gets whether repeated empty-line context switching stops upon reaching this context.
    /// </summary>
    public bool StopEmptyLineContextSwitchLoop { get; }
}
