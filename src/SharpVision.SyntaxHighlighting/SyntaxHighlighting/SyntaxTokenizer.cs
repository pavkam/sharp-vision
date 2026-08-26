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

        // A region fold and an indentation fold can legitimately share the exact same start and
        // end line (a language with both region markers and indentation-sensitive folding enabled
        // on the same construct). List<T>.Sort is an unstable introspective sort, so without a
        // deterministic tertiary key the relative order of two such ranges - and therefore which
        // one BuildFoldStartRanges keeps via Dictionary.TryAdd - would be unspecified across runs,
        // in tension with this repository's deterministic-UI-state requirement. Kind breaks the
        // tie deterministically instead.
        foldRanges.Sort(static (left, right) =>
        {
            var byStart = left.StartLine.CompareTo(right.StartLine);

            if (byStart != 0)
            {
                return byStart;
            }

            var byEnd = right.EndLine.CompareTo(left.EndLine);
            return byEnd != 0 ? byEnd : left.Kind.CompareTo(right.Kind);
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

        // Per-line cache of each Keyword/RegExpr rule's own reported SyntaxRuleMatch.SkipOffset
        // (see HasSkipOffset), keyed by rule identity. Without this, a long run of text with no
        // delimiter and no catch-all consuming rule after a Keyword rule (or a RegExpr rule that
        // never matches at all) is quadratic: the outer loop's one-character-at-a-time fallback
        // re-invokes that same rule at every offset, and each invocation independently rescans
        // forward to rediscover the identical boundary its previous invocation already found.
        // Caching that boundary and skipping re-invocation until it is reached restores linear
        // total cost, mirroring upstream KSyntaxHighlighting's own skipOffsets cache in
        // AbstractHighlighter::highlightLine.
        var skipOffsets = new Dictionary<SyntaxCompiledRule, int>(ReferenceEqualityComparer.Instance);

        // A dynamic rule's effective pattern depends on the active context frame's captures, so a
        // cached skip computed under one frame's captures is not valid once a different frame - with
        // different captures - becomes active. Frame identity (not content) is the correct
        // invalidation signal: SyntaxContextFrame.Captures is fixed for the frame's whole lifetime,
        // so a reference change means the frame itself changed.
        IReadOnlyList<string>? lastCaptures = null;

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
            var currentCaptures = stack[^1].Captures;

            if (!ReferenceEquals(lastCaptures, currentCaptures))
            {
                skipOffsets.Clear();
                lastCaptures = currentCaptures;
            }

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

                var cachedSkip = 0;

                if (rule.HasSkipOffset)
                {
                    cachedSkip = skipOffsets.GetValueOrDefault(rule);

                    if (cachedSkip < 0 || cachedSkip > offset)
                    {
                        continue;
                    }
                }

                var match = rule.TryMatch(line, offset, currentCaptures);

                if (rule.HasSkipOffset && (match.SkipOffset < 0 || match.SkipOffset > cachedSkip))
                {
                    skipOffsets[rule] = match.SkipOffset;
                }

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

    private static List<PcreRegex> CompileEmptyLinePatterns(IReadOnlyList<SyntaxEmptyLineRule> rules)
    {
        var patterns = new List<PcreRegex>(rules.Count);

        foreach (var rule in rules)
        {
            patterns.Add(SyntaxRegularExpression.Compile(rule.Pattern, !rule.CaseSensitive, minimal: false));
        }

        return patterns;
    }

    private static bool IsFoldingEmptyLine(string line, List<PcreRegex> emptyLinePatterns)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        foreach (var pattern in emptyLinePatterns)
        {
            try
            {
                if (SyntaxRegularExpression.Match(pattern, line, 0).Success)
                {
                    return true;
                }
            }
            catch (PcreMatchException error) when (SyntaxRegularExpression.IsBudgetExceeded(error))
            {
                // A pathological pattern degrades to "this pattern does not classify the line as
                // blank" rather than blocking the calling thread.
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
