// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies the KDE context-switch mini-language parser.</summary>
public sealed class SyntaxContextSwitchTests
{
    /// <summary>Verifies every public syntax value with reference-backed empty semantics remains
    /// safely readable when generic code produces its default value.</summary>
    [Fact]
    public void Default_WhenReferenceBackedSyntaxValuesAreRead_UsesDocumentedEmptySemantics()
    {
        default(SyntaxContextSwitch).IsStay.ShouldBeTrue();
        default(SyntaxContextSwitch).Targets.ShouldBeEmpty();
        default(SyntaxContextTarget).IsStay.ShouldBeTrue();
        default(SyntaxContextTarget).Pushes.ShouldBeEmpty();
        default(SyntaxHighlightedLine).Tokens.ShouldBeEmpty();
        default(SyntaxRuleMatch).Captures.ShouldBeEmpty();
        default(SyntaxContextReference).ContextName.ShouldBe(string.Empty);
        default(SyntaxItemData).Name.ShouldBe(string.Empty);
        default(SyntaxFoldRange).Kind.ShouldBe(SyntaxFoldRangeKind.Indentation);

        var info = default(SyntaxDefinitionInfo);
        info.Name.ShouldBe(string.Empty);
        info.File.ShouldBe(string.Empty);
        info.Section.ShouldBe(string.Empty);
        info.Extensions.ShouldBeEmpty();
        info.MimeTypes.ShouldBeEmpty();
        info.AlternativeNames.ShouldBeEmpty();
        info.Author.ShouldBe(string.Empty);
        info.License.ShouldBe(string.Empty);
        info.Sha256.ShouldBe(string.Empty);
        info.SourceRepository.ShouldBe(string.Empty);
        info.SourceCommit.ShouldBe(string.Empty);
    }
    /// <summary>Verifies <c>#stay</c> parses to a no-op switch.</summary>
    [Fact]
    public void Parse_WhenSpecificationIsStay_ReturnsStay() => SyntaxContextSwitch.Parse("#stay").IsStay.ShouldBeTrue();

    /// <summary>Verifies a null specification is equivalent to <c>#stay</c>.</summary>
    [Fact]
    public void Parse_WhenSpecificationIsNull_ReturnsStay() => SyntaxContextSwitch.Parse(null).IsStay.ShouldBeTrue();

    /// <summary>Verifies a bare context name pushes exactly that context.</summary>
    [Fact]
    public void Parse_WhenSpecificationIsBareName_PushesOnce()
    {
        var result = SyntaxContextSwitch.Parse("Foo");

        result.PopCount.ShouldBe(0);
        _ = result.Targets.ShouldHaveSingleItem();
        result.Targets[0].ContextName.ShouldBe("Foo");
        result.Targets[0].DefinitionName.ShouldBeNull();
    }

    /// <summary>Verifies a bare <c>#pop</c> pops without pushing anything.</summary>
    [Fact]
    public void Parse_WhenSpecificationIsSinglePop_PopsWithoutPushing()
    {
        var result = SyntaxContextSwitch.Parse("#pop");

        result.PopCount.ShouldBe(1);
        result.Targets.ShouldBeEmpty();
        result.IsStay.ShouldBeFalse();
    }

    /// <summary>Verifies repeated <c>#pop</c> tokens accumulate a pop count.</summary>
    [Fact]
    public void Parse_WhenSpecificationIsRepeatedPop_CountsEachPop() => SyntaxContextSwitch.Parse("#pop#pop#pop").PopCount.ShouldBe(3);

    /// <summary>Verifies <c>#pop#pop!Name</c> pops twice then pushes the named context.</summary>
    [Fact]
    public void Parse_WhenSpecificationPopsThenPushes_PopsThenPushesOneContext()
    {
        var result = SyntaxContextSwitch.Parse("#pop#pop!Base");

        result.PopCount.ShouldBe(2);
        _ = result.Targets.ShouldHaveSingleItem();
        result.Targets[0].ContextName.ShouldBe("Base");
    }

    /// <summary>Verifies <c>!</c>-separated names after a pop push multiple contexts in order.</summary>
    [Fact]
    public void Parse_WhenSpecificationChainsMultiplePushes_PushesEachInOrder()
    {
        var result = SyntaxContextSwitch.Parse("#pop!A!B");

        result.PopCount.ShouldBe(1);
        result.Targets.Count.ShouldBe(2);
        result.Targets[0].ContextName.ShouldBe("A");
        result.Targets[1].ContextName.ShouldBe("B");
    }

    /// <summary>Verifies a <c>Name##Definition</c> reference splits into context and definition names.</summary>
    [Fact]
    public void Parse_WhenSpecificationReferencesAnotherDefinition_SplitsContextAndDefinitionNames()
    {
        var result = SyntaxContextSwitch.Parse("Normal##JavaScript");

        result.Targets[0].ContextName.ShouldBe("Normal");
        result.Targets[0].DefinitionName.ShouldBe("JavaScript");
    }

    /// <summary>Verifies a bare <c>##Definition</c> reference leaves the context name empty.</summary>
    [Fact]
    public void Parse_WhenSpecificationReferencesOnlyAnotherDefinition_LeavesContextNameEmpty()
    {
        var result = SyntaxContextSwitch.Parse("##JavaScript");

        result.Targets[0].ContextName.ShouldBe(string.Empty);
        result.Targets[0].DefinitionName.ShouldBe("JavaScript");
    }
}
