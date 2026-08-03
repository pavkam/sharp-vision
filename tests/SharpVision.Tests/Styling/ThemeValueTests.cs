// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies literal and semantic theme-value authoring.</summary>
public sealed class ThemeValueTests
{
    /// <summary>Verifies a concrete terminal color remains a literal value.</summary>
    [Fact]
    public void ColorValue_WhenAssignedLiteral_PreservesConcreteColor()
    {
        ColorValue value = Color.Rgb(12, 34, 56);

        value.IsLiteral.ShouldBeTrue();
        value.Literal.ShouldBe(Color.Rgb(12, 34, 56));
    }

    /// <summary>Verifies a known theme color remains a semantic reference.</summary>
    [Fact]
    public void ColorValue_WhenAssignedThemeColor_PreservesSemanticRole()
    {
        ColorValue value = ThemeColor.ActiveText;

        value.IsThemeValue.ShouldBeTrue();
        value.ThemeColor.ShouldBe(ThemeColor.ActiveText);
    }

    /// <summary>Verifies a known theme attribute remains a semantic reference.</summary>
    [Fact]
    public void AttributeValue_WhenAssignedThemeAttribute_PreservesSemanticRole()
    {
        AttributeValue value = ThemeDecoration.FocusedText;

        value.IsThemeValue.ShouldBeTrue();
        value.ThemeDecoration.ShouldBe(ThemeDecoration.FocusedText);
    }

    /// <summary>Verifies zero-initialized semantic values remain concrete terminal defaults.</summary>
    [Fact]
    public void Default_WhenCreated_RepresentsLiteralTerminalDefaults()
    {
        var color = default(ColorValue);
        var attributes = default(AttributeValue);

        color.IsLiteral.ShouldBeTrue();
        color.Literal.ShouldBe(Color.Default);
        attributes.IsLiteral.ShouldBeTrue();
        attributes.Literal.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies diagnostics format either discriminated branch without accessing the inactive branch.</summary>
    [Fact]
    public void ToString_WhenEitherBranchIsActive_DoesNotThrow()
    {
        ColorValue literalColor = Color.Rgb(1, 2, 3);
        ColorValue semanticColor = ThemeColor.Accent;
        AttributeValue literalAttributes = TerminalAttributes.Bold;
        AttributeValue semanticAttributes = ThemeDecoration.FocusedText;

        _ = Should.NotThrow(() => literalColor.ToString());
        semanticColor.ToString().ShouldBe("ThemeColor.Accent");
        _ = Should.NotThrow(() => literalAttributes.ToString());
        semanticAttributes.ToString().ShouldBe("ThemeDecoration.FocusedText");
    }
}
