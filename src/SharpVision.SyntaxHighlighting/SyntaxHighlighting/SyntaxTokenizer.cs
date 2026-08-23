// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>
/// Tokenizes a complete source-code document against one compiled <see cref="SyntaxGrammar"/>,
/// reproducing upstream KSyntaxHighlighting's per-line context-stack algorithm
/// (<c>AbstractHighlighter::highlightLine</c>) over the whole buffer at once.
/// </summary>
/// <remarks>
/// Upstream tokenizes incrementally, one already-edited line at a time, carrying an opaque
/// <c>State</c> forward so an editor can re-highlight only the lines a keystroke actually changed.
/// The <c>CodeView</c> control displays immutable, already-complete source text, so this
/// type instead tokenizes the entire buffer in one synchronous pass and keeps the context stack as
/// an ordinary local list rather than a serializable, reusable state object - a legitimate
/// simplification for a read-only display that never needs incremental re-highlighting.
/// </remarks>
[PublicAPI]
public static class SyntaxTokenizer
{
    private const int _maxContextSwitchesPerBoundary = 1024;
    private const int _maxLoopIterationsPerLine = 1_000_000;
    private const int _indentationTabWidth = 4;

    /// <summary>Tokenizes a complete document.</summary>
    /// <param name="grammar">The non-null compiled grammar to tokenize against.</param>
    /// <param name="text">The non-null complete source text.</param>
    /// <returns>The tokenized lines and detected fold ranges.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grammar"/> or <paramref name="text"/> is null.</exception>
    [MustUseReturnValue]
    public static SyntaxHighlightResult Tokenize(SyntaxGrammar grammar, string text)
    {
        ArgumentNullException.ThrowIfNull(grammar);
        ArgumentNullException.ThrowIfNull(text);

        var lines = SplitLines(text);
        var stack = new List<SyntaxContextFrame> { new(grammar, 0, []) };
        var openRegions = new Stack<(int Line, string? RegionName)>();
        var foldRanges = new List<SyntaxFoldRange>();
        var indentationEligibility = new bool[lines.Count];
        var emptyLinePatterns = CompileEmptyLinePatterns(grammar.Definition.General.EmptyLineRules);
        var result = new List<SyntaxHighlightedLine>(lines.Count);

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            var startContext = stack[^1].Context;

            if (line.Length == 0)
            {
                ProcessEmptyLine(stack);
                result.Add(new SyntaxHighlightedLine([]));
            }
            else
            {
                result.Add(ProcessLine(stack, line, lineIndex, openRegions, foldRanges));
            }

            indentationEligibility[lineIndex] = startContext.IndentationBasedFoldingEnabled &&
                                                 !IsFoldingEmptyLine(line, emptyLinePatterns);
        }

        CloseUnterminatedRegions(openRegions, lines.Count - 1, foldRanges);
        ComputeIndentationFolds(lines, indentationEligibility, foldRanges);

        foldRanges.Sort(static (left, right) =>
        {
            var byStart = left.StartLine.CompareTo(right.StartLine);
            return byStart != 0 ? byStart : right.EndLine.CompareTo(left.EndLine);
        });

        return new SyntaxHighlightResult(result, foldRanges);
    }

    #region Line splitting

    private static List<string> SplitLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return [.. normalized.Split('\n')];
    }

    #endregion

    #region Empty-line context switching

    private static void ProcessEmptyLine(List<SyntaxContextFrame> stack)
    {
        var iterations = 0;

        while (!stack[^1].Context.LineEmptyTarget.IsStay)
        {
            if (!ApplySwitch(stack, stack[^1].Context.LineEmptyTarget, []))
            {
                break;
            }

            if (stack[^1].Context.StopEmptyLineContextSwitchLoop)
            {
                break;
            }

            if (++iterations > _maxContextSwitchesPerBoundary)
            {
                break;
            }
        }
    }

    #endregion

    #region Main per-line loop

    private static SyntaxHighlightedLine ProcessLine(
        List<SyntaxContextFrame> stack,
        string line,
        int lineIndex,
        Stack<(int Line, string? RegionName)> openRegions,
        List<SyntaxFoldRange> foldRanges)
    {
        var offset = 0;
        var beginOffset = 0;
        var lineContinuation = false;
        var firstNonSpace = -1;
        var currentStyle = stack[^1].Context.AttributeStyle;
        var tokens = new List<SyntaxToken>();
        var lastOffset = -1;
        var stallCount = 0;

        while (offset < line.Length)
        {
            if (offset == lastOffset)
            {
                if (++stallCount > _maxLoopIterationsPerLine)
                {
                    break;
                }
            }
            else
            {
                lastOffset = offset;
                stallCount = 0;
            }

            var topContext = stack[^1].Context;
            var isLookAhead = false;
            var matchedOffset = offset;
            SyntaxDefaultStyle? matchedStyle = null;

            foreach (var rule in topContext.Rules)
            {
                if (rule.Source.Column is { } column && column != offset)
                {
                    continue;
                }

                if (rule.Source.FirstNonSpace)
                {
                    if (firstNonSpace < 0)
                    {
                        firstNonSpace = FirstNonSpaceIndex(line);
                    }

                    if (offset > firstNonSpace)
                    {
                        continue;
                    }
                }

                var match = rule.TryMatch(line, offset, stack[^1].Captures);

                if (!match.Success || match.Length <= 0)
                {
                    continue;
                }

                ApplyFolding(rule.Source, offset, match.Length, openRegions, lineIndex, foldRanges);

                if (rule.Source.LookAhead)
                {
                    _ = ApplySwitch(stack, rule.ResolvedTarget, match.Captures);
                    isLookAhead = true;
                    break;
                }

                _ = ApplySwitch(stack, rule.ResolvedTarget, match.Captures);
                matchedOffset = offset + match.Length;
                matchedStyle = rule.ResolvedStyle ?? stack[^1].Context.AttributeStyle;

                if (matchedOffset == line.Length && rule.Source.Kind == SyntaxRuleKind.LineContinuation)
                {
                    lineContinuation = true;
                }

                break;
            }

            if (isLookAhead)
            {
                continue;
            }

            if (matchedStyle is null)
            {
                topContext = stack[^1].Context;

                if (!topContext.FallthroughTarget.IsStay)
                {
                    _ = ApplySwitch(stack, topContext.FallthroughTarget, []);
                    continue;
                }

                matchedOffset = offset + 1;
                matchedStyle = topContext.AttributeStyle;
            }

            if (matchedStyle.Value != currentStyle)
            {
                if (offset > 0)
                {
                    Debug.Assert(
                        tokens.Count == 0 || tokens[^1].Start + tokens[^1].Length == beginOffset,
                        "Every emitted token tiles the line exactly: it starts where the previous one ended, with no gap or overlap.");

                    tokens.Add(new SyntaxToken(beginOffset, offset - beginOffset, currentStyle));
                }

                beginOffset = offset;
                currentStyle = matchedStyle.Value;
            }

            // Every path that reaches here - a successful rule match (already filtered to a
            // positive length), a fallthrough switch (which "continue"s before arriving here), or
            // the no-rule-matched fallback (offset + 1) - strictly advances past the current
            // offset, so the outer while loop always makes forward progress on a "clean" pass. The
            // stall counter above exists only to bound pathological cases that still manage to
            // revisit the same offset (for example, a grammar whose fallthrough chain cycles
            // through contexts without ever matching or advancing).
            Debug.Assert(matchedOffset > offset, "A non-lookahead match always advances the offset.");
            offset = matchedOffset;
        }

        if (beginOffset < offset)
        {
            Debug.Assert(
                tokens.Count == 0 || tokens[^1].Start + tokens[^1].Length == beginOffset,
                "Every emitted token tiles the line exactly: it starts where the previous one ended, with no gap or overlap.");

            tokens.Add(new SyntaxToken(beginOffset, offset - beginOffset, currentStyle));
        }

        if (!lineContinuation)
        {
            var iterations = 0;

            while (!stack[^1].Context.LineEndTarget.IsStay)
            {
                if (!ApplySwitch(stack, stack[^1].Context.LineEndTarget, []))
                {
                    break;
                }

                if (++iterations > _maxContextSwitchesPerBoundary)
                {
                    break;
                }
            }
        }

        return new SyntaxHighlightedLine(tokens);
    }

    private static int FirstNonSpaceIndex(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (!char.IsWhiteSpace(line[i]))
            {
                return i;
            }
        }

        return line.Length;
    }

    #endregion

    #region Context stack

    /// <summary>
    /// Applies one resolved context switch to the stack.
    /// </summary>
    /// <returns>
    /// Whether the caller should keep chasing further stay-less switches on the new top context:
    /// true whenever a push happened, and for a pure pop, true only when the pop stayed within
    /// bounds (never removing the last remaining frame).
    /// </returns>
    private static bool ApplySwitch(List<SyntaxContextFrame> stack, SyntaxContextTarget target, IReadOnlyList<string> captures)
    {
        if (target.IsStay)
        {
            return false;
        }

        var poppedToBottom = false;

        for (var i = 0; i < target.PopCount; i++)
        {
            if (stack.Count <= 1)
            {
                poppedToBottom = true;
                break;
            }

            stack.RemoveAt(stack.Count - 1);
        }

        // The pop loop above always stops before removing the last remaining frame, so the root
        // context frame pushed at the start of Tokenize is never popped and the stack can never
        // become empty; every caller's stack[^1] access stays safe.
        Debug.Assert(stack.Count > 0, "The root context frame is never popped, so the stack is never empty.");

        if (target.Pushes.Count == 0)
        {
            return !poppedToBottom;
        }

        foreach (var push in target.Pushes)
        {
            stack.Add(new SyntaxContextFrame(push.Grammar, push.ContextIndex, captures));
        }

        return true;
    }

    #endregion

    #region Region folding

    private static void ApplyFolding(
        SyntaxRule rule,
        int offset,
        int matchLength,
        Stack<(int Line, string? RegionName)> openRegions,
        int lineIndex,
        List<SyntaxFoldRange> foldRanges)
    {
        _ = offset;
        _ = matchLength;

        if (rule.EndRegion is not null)
        {
            CloseRegion(openRegions, lineIndex, foldRanges);
        }

        if (rule.BeginRegion is not null)
        {
            openRegions.Push((lineIndex, rule.BeginRegion));
        }
    }

    private static void CloseRegion(Stack<(int Line, string? RegionName)> openRegions, int lineIndex, List<SyntaxFoldRange> foldRanges)
    {
        if (openRegions.Count == 0)
        {
            return;
        }

        var (startLine, regionName) = openRegions.Pop();
        foldRanges.Add(new SyntaxFoldRange(startLine, lineIndex, SyntaxFoldRangeKind.Region, regionName));
    }

    private static void CloseUnterminatedRegions(Stack<(int Line, string? RegionName)> openRegions, int lastLine, List<SyntaxFoldRange> foldRanges)
    {
        // An unterminated beginRegion (a syntax error, or simply the end of the visible buffer)
        // still folds everything from its start to the end of the document, rather than being
        // silently dropped.
        while (openRegions.Count > 0)
        {
            var (startLine, regionName) = openRegions.Pop();

            if (lastLine > startLine)
            {
                foldRanges.Add(new SyntaxFoldRange(startLine, lastLine, SyntaxFoldRangeKind.Region, regionName));
            }
        }
    }

    #endregion

    #region Indentation folding

    private static List<Regex> CompileEmptyLinePatterns(IReadOnlyList<SyntaxEmptyLineRule> rules)
    {
        var patterns = new List<Regex>(rules.Count);

        foreach (var rule in rules)
        {
            try
            {
                patterns.Add(new Regex(rule.Pattern, RegexOptions.CultureInvariant | (rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)));
            }
            catch (RegexParseException)
            {
                // An invalid pattern in a third-party definition simply never matches.
            }
        }

        return patterns;
    }

    private static bool IsFoldingEmptyLine(string line, List<Regex> emptyLinePatterns)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        foreach (var pattern in emptyLinePatterns)
        {
            if (pattern.IsMatch(line))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndentationOf(string line)
    {
        var width = 0;

        foreach (var c in line)
        {
            if (c == '\t')
            {
                width = ((width / _indentationTabWidth) + 1) * _indentationTabWidth;
            }
            else if (c == ' ')
            {
                width++;
            }
            else
            {
                break;
            }
        }

        return width;
    }

    private static void ComputeIndentationFolds(List<string> lines, bool[] eligibility, List<SyntaxFoldRange> foldRanges)
    {
        var stack = new List<(int Level, int StartLine)>();
        var previousEligible = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            if (!eligibility[i])
            {
                continue;
            }

            var level = IndentationOf(lines[i]);

            while (stack.Count > 0 && stack[^1].Level >= level)
            {
                var (_, StartLine) = stack[^1];
                stack.RemoveAt(stack.Count - 1);

                if (previousEligible > StartLine)
                {
                    foldRanges.Add(new SyntaxFoldRange(StartLine, previousEligible, SyntaxFoldRangeKind.Indentation, null));
                }
            }

            stack.Add((level, i));
            previousEligible = i;
        }

        while (stack.Count > 0)
        {
            var (_, StartLine) = stack[^1];
            stack.RemoveAt(stack.Count - 1);

            if (previousEligible > StartLine)
            {
                foldRanges.Add(new SyntaxFoldRange(StartLine, previousEligible, SyntaxFoldRangeKind.Indentation, null));
            }
        }
    }

    #endregion
}
