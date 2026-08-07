// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exposes the optional-glyph mutation helpers for testing.</summary>
internal sealed class GlyphProbe: ControlBase
{
    private Rune? _testGlyph;

    /// <summary>Gets the stored nullable glyph without applying a fallback.</summary>
    internal Rune? RawTestGlyph => _testGlyph;

    /// <summary>Gets or sets the test glyph through the optional-glyph helper.</summary>
    internal Rune TestGlyph
    {
        get => _testGlyph ?? new Rune('?');
        set => SetOptionalGlyph(ref _testGlyph, value, nameof(TestGlyph));
    }

    /// <summary>Clears the stored glyph through the optional-glyph reset helper.</summary>
    /// <returns>True when a stored glyph was cleared; otherwise, false.</returns>
    internal bool ResetTestGlyph() => ResetOptionalGlyph(ref _testGlyph, nameof(TestGlyph));
}
