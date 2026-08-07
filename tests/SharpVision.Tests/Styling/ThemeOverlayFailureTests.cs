// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.IO;
using System.Reflection;

/// <summary>Verifies every overlay failure surfaces as the labelled theme error the merge promises.
///
/// <para><c>Theme.Overlay</c> wraps leaf conversion in a <c>try</c>/<c>catch</c> whose own comment
/// says every failure becomes a slug-and-path-labelled <c>InvalidDataException</c> - but
/// <c>property.SetValue</c> sat one line <em>below</em> the catch, and the filter matched neither
/// what reflection throws for a rejecting accessor nor what it throws for a get-only property. So
/// the members that validate correctly were exactly the ones whose failures escaped unlabelled,
/// with no theme name and no dotted path to act on.</para>
/// </summary>
public sealed class ThemeOverlayFailureTests
{
    /// <summary>The regression this file exists to pin: a validating init accessor rejecting a
    /// value is a labelled theme error, not a raw TargetInvocationException.</summary>
    [Theory]
    [InlineData(""", "separator": { "normal": { "horizontalGlyph": "世" } } """, "styles.separator")]
    [InlineData(""", "tabControl": { "normal": { "dividerGlyph": "世" } } """, "styles.tabControl")]
    public void Parse_WhenAValidatingMemberRejectsTheValue_ReportsALabelledThemeError(
        string extraStyles,
        string expectedPath)
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(extraStyles: extraStyles));

        var error = Should.Throw<InvalidDataException>(() => Resolve(theme, expectedPath));

        error.Message.Contains(expectedPath, StringComparison.Ordinal).ShouldBeTrue(
            $"the failure must name its dotted path, but read '{error.Message}'");
        error.InnerException.ShouldNotBeNull()
            .ShouldNotBeOfType<TargetInvocationException>(
                "the accessor's own exception must be unwrapped, not the reflection wrapper");
    }

    /// <summary>Verifies a wide glyph is rejected at all, which the labelling above presupposes -
    /// a silently accepted two-cell glyph would make the message question moot.</summary>
    [Fact]
    public void Parse_WhenAGlyphIsWiderThanOneCell_IsRejected()
    {
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(extraStyles: """, "separator": { "normal": { "horizontalGlyph": "世" } } """));

        _ = Should.Throw<InvalidDataException>(
            () => SeparatorStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a get-only computed property is refused by name rather than resolving,
    /// converting, and then crashing inside SetValue.</summary>
    [Theory]
    [InlineData("checkBox", "markWidth")]
    [InlineData("radioButton", "markWidth")]
    [InlineData("radioButton", "uncheckedText")]
    [InlineData("radioButton", "checkedText")]
    public void Parse_WhenASectionAuthorsAComputedProperty_ReportsItAsUnknown(string section, string member)
    {
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(extraStyles: $$""", "{{section}}": { "normal": { "{{member}}": 3 } } """));

        var error = Should.Throw<InvalidDataException>(() => Resolve(theme, $"styles.{section}"));

        error.Message.Contains("is not a known property", StringComparison.Ordinal).ShouldBeTrue(
            $"a derived member must be refused by name, but read '{error.Message}'");
    }

    /// <summary>The counter-case: a settable member with the same shape is still authorable, so
    /// refusing computed properties did not refuse real ones.</summary>
    [Fact]
    public void Parse_WhenASectionAuthorsASettableMember_IsAccepted()
    {
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(extraStyles: """, "checkBox": { "normal": { "markStyle": "tick" } } """));

        CheckBoxStyle.Definition.Resolve(null, theme).MarkStyle.ShouldBe(CheckBoxMarkStyle.Tick);
    }

    // Section binding is deferred, so a rejection surfaces on first resolution rather than at Parse.
    private static void Resolve(Theme theme, string path)
    {
        if (path.Contains("separator", StringComparison.Ordinal))
        {
            _ = SeparatorStyle.Definition.Resolve(null, theme);
        }
        else if (path.Contains("tabControl", StringComparison.Ordinal))
        {
            _ = TabControlStyle.Definition.Resolve(null, theme);
        }
        else if (path.Contains("radioButton", StringComparison.Ordinal))
        {
            _ = RadioButtonStyle.Definition.Resolve(null, theme);
        }
        else
        {
            _ = CheckBoxStyle.Definition.Resolve(null, theme);
        }
    }
}
