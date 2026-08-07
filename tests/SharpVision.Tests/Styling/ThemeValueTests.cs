// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies literal and semantic theme-value authoring.</summary>
public sealed class ThemeValueTests
{
    /// <summary>Verifies a concrete terminal color remains a literal value.</summary>
    [Fact]
    public void ControlColor_WhenAssignedLiteral_PreservesConcreteColor()
    {
        ControlColor value = Color.Rgb(12, 34, 56);

        value.IsLiteral.ShouldBeTrue();
        value.Literal.ShouldBe(Color.Rgb(12, 34, 56));
    }

    /// <summary>Verifies a known theme color remains a semantic reference.</summary>
    [Fact]
    public void ControlColor_WhenAssignedSemanticColor_PreservesSemanticReference()
    {
        ControlColor value = SemanticColor.ActiveText;

        value.Semantic.ShouldBeTrue();
        value.SemanticColor.ShouldBe(SemanticColor.ActiveText);
    }

    /// <summary>Verifies a known theme attribute remains a semantic reference.</summary>
    [Fact]
    public void ControlDecoration_WhenAssignedSemanticDecoration_PreservesSemanticReference()
    {
        ControlDecoration value = SemanticDecoration.FocusedText;

        value.Semantic.ShouldBeTrue();
        value.SemanticDecoration.ShouldBe(SemanticDecoration.FocusedText);
    }

    /// <summary>Verifies zero-initialized semantic values remain concrete terminal defaults.</summary>
    [Fact]
    public void Default_WhenCreated_RepresentsLiteralTerminalDefaults()
    {
        var color = default(ControlColor);
        var attributes = default(ControlDecoration);

        color.IsLiteral.ShouldBeTrue();
        color.Literal.ShouldBe(Color.Default);
        attributes.IsLiteral.ShouldBeTrue();
        attributes.Literal.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies diagnostics format either discriminated branch without accessing the inactive branch.</summary>
    [Fact]
    public void ToString_WhenEitherBranchIsActive_DoesNotThrow()
    {
        ControlColor literalColor = Color.Rgb(1, 2, 3);
        ControlColor semanticColor = SemanticColor.Accent;
        ControlDecoration literalAttributes = TerminalAttributes.Bold;
        ControlDecoration semanticAttributes = SemanticDecoration.FocusedText;

        _ = Should.NotThrow(() => literalColor.ToString());
        semanticColor.ToString().ShouldBe("SemanticColor.Accent");
        _ = Should.NotThrow(() => literalAttributes.ToString());
        semanticAttributes.ToString().ShouldBe("SemanticDecoration.FocusedText");
    }
}
