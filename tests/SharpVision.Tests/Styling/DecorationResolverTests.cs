// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies decoration overlay resolution precedence between legacy attribute flags and
/// typed underline overrides, and the inline-markup span merge built on top of it.</summary>
public sealed class DecorationResolverTests
{
    /// <summary>Verifies an explicit typed underline override wins over an inherited legacy
    /// underline attribute flag, clearing the legacy flag instead of discarding the override (see
    /// the FIGfont-adjacent bug class: sibling precedence rules must agree on which override wins).</summary>
    [Fact]
    public void Resolve_WhenTypedUnderlineOverridesInheritedLegacyFlag_ClearsLegacyFlagAndKeepsTypedValue()
    {
        var inherited = new TerminalStyle(attributes: TerminalAttributes.Underline);

        var (attributes, underline, _) = DecorationResolver.Resolve(
            inherited,
            underline: Underline.Curly);

        underline.ShouldBe(Underline.Curly);
        (attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies an explicit legacy attribute override still clears any inherited typed
    /// underline when no typed override is supplied.</summary>
    [Fact]
    public void Resolve_WhenLegacyAttributeIsSetWithoutTypedOverride_ClearsInheritedTypedUnderline()
    {
        var inherited = new TerminalStyle(underline: Underline.Paired);

        var (attributes, underline, _) = DecorationResolver.Resolve(
            inherited,
            attributes: TerminalAttributes.Underline);

        underline.ShouldBe(Underline.None);
        (attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);
    }

    /// <summary>Verifies neither override present simply forwards the inherited style unchanged.</summary>
    [Fact]
    public void Resolve_WhenNoOverridesSupplied_ForwardsInheritedDecoration()
    {
        var inherited = new TerminalStyle(underline: Underline.Dashed, underlineColor: Color.Rgb(255, 0, 0));

        var (attributes, underline, underlineColor) = DecorationResolver.Resolve(inherited);

        attributes.ShouldBe(TerminalAttributes.None);
        underline.ShouldBe(Underline.Dashed);
        underlineColor.ShouldBe(Color.Rgb(255, 0, 0));
    }

    /// <summary>Verifies a fully absent underline (neither legacy flag nor typed variant) resets
    /// the underline color to the default rather than leaking an inherited color with no underline
    /// to render it against.</summary>
    [Fact]
    public void Resolve_WhenUnderlineIsFullyCleared_ResetsUnderlineColorToDefault()
    {
        var inherited = new TerminalStyle(underline: Underline.Curly, underlineColor: Color.Rgb(0, 0, 255));

        var (_, underline, underlineColor) = DecorationResolver.Resolve(
            inherited,
            underline: Underline.None);

        underline.ShouldBe(Underline.None);
        underlineColor.ShouldBe(Color.Default);
    }

    /// <summary>Verifies a span requesting one blink variant clears an inherited blink variant of
    /// the other kind, the same way a directly-authored style must, since a cell style can never
    /// carry both bits at once.</summary>
    [Fact]
    public void Merge_WhenSpanRequestsBlink_ClearsInheritedRapidBlink()
    {
        var inherited = new TerminalStyle(attributes: TerminalAttributes.RapidBlink);

        var merged = DecorationResolver.Merge(
            inherited,
            attributes: TerminalAttributes.Blink,
            underline: Underline.None,
            foreground: null,
            background: null,
            underlineColor: null,
            link: null);

        (merged.Attributes & TerminalAttributes.RapidBlink).ShouldBe(TerminalAttributes.None);
        (merged.Attributes & TerminalAttributes.Blink).ShouldBe(TerminalAttributes.Blink);
    }

    /// <summary>Verifies a span's typed underline variant supersedes an inherited legacy
    /// straight-underline attribute, clearing the legacy bit so the two never both reach the merged
    /// cell style.</summary>
    [Fact]
    public void Merge_WhenSpanHasTypedUnderline_DropsLegacyUnderlineAttribute()
    {
        var inherited = new TerminalStyle(attributes: TerminalAttributes.Underline);

        var merged = DecorationResolver.Merge(
            inherited,
            attributes: TerminalAttributes.None,
            underline: Underline.Curly,
            foreground: null,
            background: null,
            underlineColor: null,
            link: null);

        merged.Underline.ShouldBe(Underline.Curly);
        (merged.Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);
    }
}
