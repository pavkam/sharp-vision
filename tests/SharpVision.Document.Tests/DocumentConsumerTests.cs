// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Controls.Document;

using Shouldly;

/// <summary>Verifies the optional Document assembly is consumable through its public node model.</summary>
public sealed class DocumentConsumerTests
{
    /// <summary>Verifies a consumer can build a detached paragraph and attach it to a document.</summary>
    [Fact]
    public void Blocks_WhenBuiltByAConsumer_AcceptThePublicDocumentModel()
    {
        // Arrange
        var paragraph = new DocumentParagraph("Hello");
        var document = new Document();

        // Act
        document.Blocks.Add(paragraph);

        // Assert
        document.Blocks.ShouldHaveSingleItem().ShouldBeSameAs(paragraph);
    }

    /// <summary>Verifies semantic inline containers retain nested inline structure with exclusive ownership.</summary>
    [Fact]
    public void Inlines_WhenSemanticContainersAreNested_PreserveExclusiveOwnership()
    {
        // Arrange
        var text = new DocumentTextRun("important");
        var emphasis = new DocumentEmphasis();
        var strong = new DocumentStrong();

        // Act
        emphasis.Inlines.Add(text);
        strong.Inlines.Add(emphasis);

        // Assert
        strong.Inlines.ShouldHaveSingleItem().ShouldBeSameAs(emphasis);
        emphasis.Inlines.ShouldHaveSingleItem().ShouldBeSameAs(text);
        _ = Should.Throw<ArgumentException>(() => strong.Inlines.Add(text));
    }
}
