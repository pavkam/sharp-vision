// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the protected <see cref="ControlBase.SetOptionalGlyph"/> and <see cref="ControlBase.ResetOptionalGlyph"/> helpers.</summary>
public sealed class SetOptionalGlyphTests
{
    /// <summary>Verifies that setting a glyph fires PropertyChanged.</summary>
    [Fact]
    public void SetOptionalGlyph_WhenNewValue_FiresPropertyChanged()
    {
        var probe = new GlyphProbe();
        var changed = new List<string>();
        probe.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        probe.TestGlyph = new Rune('X');

        probe.TestGlyph.ShouldBe(new Rune('X'));
        changed.ShouldContain(nameof(GlyphProbe.TestGlyph));
    }

    /// <summary>Verifies that setting the same glyph value twice is a no-op.</summary>
    [Fact]
    public void SetOptionalGlyph_WhenSameValue_IsNoOp()
    {
        var probe = new GlyphProbe { TestGlyph = new Rune('X') };
        var changed = new List<string>();
        probe.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        probe.TestGlyph = new Rune('X');

        changed.ShouldBeEmpty();
    }

    /// <summary>Verifies that resetting a set glyph clears it and notifies.</summary>
    [Fact]
    public void ResetOptionalGlyph_WhenGlyphIsSet_ClearsAndNotifies()
    {
        var probe = new GlyphProbe { TestGlyph = new Rune('X') };
        var changed = new List<string>();
        probe.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        var result = probe.ResetTestGlyph();

        result.ShouldBeTrue();
        probe.RawTestGlyph.ShouldBeNull();
        changed.ShouldContain(nameof(GlyphProbe.TestGlyph));
    }

    /// <summary>Verifies that resetting an already-null glyph returns false without notification.</summary>
    [Fact]
    public void ResetOptionalGlyph_WhenAlreadyNull_ReturnsFalse()
    {
        var probe = new GlyphProbe();
        var changed = new List<string>();
        probe.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        var result = probe.ResetTestGlyph();

        result.ShouldBeFalse();
        changed.ShouldBeEmpty();
    }
}
