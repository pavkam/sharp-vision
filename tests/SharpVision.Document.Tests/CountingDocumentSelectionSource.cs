// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Text;

/// <summary>Counts complete selectable-snapshot materializations for freshness-cost tests.</summary>
internal sealed class CountingDocumentSelectionSource: ControlBase, ISelectableTextSource
{
    private string _text;

    /// <summary>Initializes one authoritative ASCII source.</summary>
    /// <param name="text">The non-null source text.</param>
    internal CountingDocumentSelectionSource(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
    }

    /// <summary>Gets the number of complete snapshots requested.</summary>
    internal int SnapshotCalls { get; private set; }

    /// <summary>Replaces semantic text and invalidates the retained control.</summary>
    /// <param name="text">The non-null replacement.</param>
    internal void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.Equals(_text, text, StringComparison.Ordinal))
        {
            return;
        }

        _text = text;
        Invalidate();
    }

    /// <inheritdoc/>
    public override SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        SnapshotCalls++;
        var glyphs = new SelectableTextGlyph[_text.Length];

        for (var index = 0; index < glyphs.Length; index++)
        {
            glyphs[index] = new SelectableTextGlyph(
                new Selection(index, index + 1),
                new Rect(index, 0, 1, 1));
        }

        return new SelectableTextSnapshot(_text, glyphs, isAuthoritative: true);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(_text.Length, 1);
    }
}
