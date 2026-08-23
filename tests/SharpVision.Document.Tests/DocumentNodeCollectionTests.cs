// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Documents.Document;

/// <summary>Verifies the ordered single-owner semantics <see cref="DocumentNodeCollection{TNode}"/>
/// gives <see cref="DocumentBlockCollection"/>, <see cref="DocumentInlineCollection"/>, and
/// <see cref="DocumentListItemCollection"/>.</summary>
public sealed class DocumentNodeCollectionTests
{
    /// <summary>Verifies Add appends in call order.</summary>
    [Fact]
    public void Add_WhenNodesAreAppended_KeepsCallOrder()
    {
        // Arrange
        var document = new Document();
        var first = new DocumentParagraph("First");
        var second = new DocumentParagraph("Second");

        // Act
        document.Blocks.Add(first);
        document.Blocks.Add(second);

        // Assert
        document.Blocks.Count.ShouldBe(2);
        document.Blocks[0].ShouldBeSameAs(first);
        document.Blocks[1].ShouldBeSameAs(second);
    }

    /// <summary>Verifies Insert places a node at the requested position, including the end.</summary>
    [Fact]
    public void Insert_WhenPositionIsInsideTheRange_PlacesTheNodeThere()
    {
        // Arrange
        var document = new Document();
        var first = new DocumentParagraph("First");
        var middle = new DocumentParagraph("Middle");
        var last = new DocumentParagraph("Last");
        document.Blocks.Add(first);

        // Act
        document.Blocks.Insert(document.Blocks.Count, last);
        document.Blocks.Insert(1, middle);

        // Assert
        document.Blocks[0].ShouldBeSameAs(first);
        document.Blocks[1].ShouldBeSameAs(middle);
        document.Blocks[2].ShouldBeSameAs(last);
    }

    /// <summary>Verifies a null node is rejected before the sequence changes.</summary>
    [Fact]
    public void Add_WhenNodeIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var document = new Document();

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => document.Blocks.Add(null!));
        _ = Should.Throw<ArgumentNullException>(() => document.Blocks.Insert(0, null!));
        _ = Should.Throw<ArgumentNullException>(() => document.Blocks.Remove(null!));
        document.Blocks.Count.ShouldBe(0);
    }

    /// <summary>Verifies an insertion position outside zero through Count is rejected.</summary>
    [Fact]
    public void Insert_WhenPositionIsOutsideTheRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var document = new Document();
        document.Blocks.Add(new DocumentParagraph("First"));

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.Blocks.Insert(-1, new DocumentParagraph("X")));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.Blocks.Insert(2, new DocumentParagraph("X")));
        document.Blocks.Count.ShouldBe(1);
    }

    /// <summary>Verifies reading or removing an out-of-range position is rejected.</summary>
    [Fact]
    public void RemoveAt_WhenPositionIsOutsideTheEntries_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var document = new Document();
        document.Blocks.Add(new DocumentParagraph("First"));

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.Blocks.RemoveAt(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.Blocks.RemoveAt(1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => _ = document.Blocks[1]);
        document.Blocks.Count.ShouldBe(1);
    }

    /// <summary>Verifies a node already owned by a document is refused by a second collection, and
    /// that neither collection is disturbed by the rejection.</summary>
    [Fact]
    public void Add_WhenNodeAlreadyBelongsToADocument_ThrowsArgumentException()
    {
        // Arrange
        var first = new Document();
        var second = new Document();
        var paragraph = new DocumentParagraph("Shared");
        first.Blocks.Add(paragraph);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => second.Blocks.Add(paragraph));
        first.Blocks.Count.ShouldBe(1);
        second.Blocks.Count.ShouldBe(0);
    }

    /// <summary>Verifies a node already owned by another node is refused, so a tree can never become
    /// a graph even when both owners are detached from any document.</summary>
    [Fact]
    public void Add_WhenNodeAlreadyBelongsToAnotherNode_ThrowsArgumentException()
    {
        // Arrange
        var first = new DocumentBlockQuote();
        var second = new DocumentBlockQuote();
        var paragraph = new DocumentParagraph("Shared");
        first.Blocks.Add(paragraph);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => second.Blocks.Add(paragraph));
        first.Blocks.Count.ShouldBe(1);
    }

    /// <summary>Verifies a node cannot be added to the very collection that already owns it.</summary>
    [Fact]
    public void Add_WhenNodeIsAlreadyInThisCollection_ThrowsArgumentException()
    {
        // Arrange
        var document = new Document();
        var paragraph = new DocumentParagraph("Shared");
        document.Blocks.Add(paragraph);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => document.Blocks.Add(paragraph));
        document.Blocks.Count.ShouldBe(1);
    }

    /// <summary>Verifies Remove detaches the node and hands it back reusable in another tree.</summary>
    [Fact]
    public void Remove_WhenNodeIsFound_LeavesItDetachedAndReusable()
    {
        // Arrange
        var first = new Document();
        var second = new Document();
        var paragraph = new DocumentParagraph("Moved");
        first.Blocks.Add(paragraph);

        // Act
        var removed = first.Blocks.Remove(paragraph);
        second.Blocks.Add(paragraph);

        // Assert
        removed.ShouldBeTrue();
        first.Blocks.Count.ShouldBe(0);
        second.Blocks[0].ShouldBeSameAs(paragraph);
    }

    /// <summary>Verifies Remove reports false for a node this collection never owned.</summary>
    [Fact]
    public void Remove_WhenNodeIsForeign_ReportsFalse()
    {
        // Arrange
        var document = new Document();
        var foreign = new DocumentParagraph("Foreign");

        // Act
        var removed = document.Blocks.Remove(foreign);

        // Assert
        removed.ShouldBeFalse();
    }

    /// <summary>Verifies RemoveAt detaches exactly the node at that position.</summary>
    [Fact]
    public void RemoveAt_WhenPositionIsValid_DetachesOnlyThatNode()
    {
        // Arrange
        var document = new Document();
        var first = new DocumentParagraph("First");
        var second = new DocumentParagraph("Second");
        document.Blocks.Add(first);
        document.Blocks.Add(second);
        var reuse = new Document();

        // Act
        document.Blocks.RemoveAt(0);

        // Assert
        document.Blocks.Count.ShouldBe(1);
        document.Blocks[0].ShouldBeSameAs(second);
        reuse.Blocks.Add(first);
        reuse.Blocks[0].ShouldBeSameAs(first);
    }

    /// <summary>Verifies Clear empties the sequence and detaches every node for reuse.</summary>
    [Fact]
    public void Clear_WhenCollectionHasNodes_DetachesEveryNode()
    {
        // Arrange
        var document = new Document();
        var first = new DocumentParagraph("First");
        var second = new DocumentParagraph("Second");
        document.Blocks.Add(first);
        document.Blocks.Add(second);
        var reuse = new Document();

        // Act
        document.Blocks.Clear();

        // Assert
        document.Blocks.Count.ShouldBe(0);
        reuse.Blocks.Add(first);
        reuse.Blocks.Add(second);
        reuse.Blocks.Count.ShouldBe(2);
    }

    /// <summary>Verifies clearing an already-empty collection is a no-op rather than a fault.</summary>
    [Fact]
    public void Clear_WhenCollectionIsEmpty_DoesNothing()
    {
        // Arrange
        var document = new Document();

        // Act
        document.Blocks.Clear();

        // Assert
        document.Blocks.Count.ShouldBe(0);
    }

    /// <summary>Verifies both the value enumerator and the interface enumerator walk the sequence in
    /// order.</summary>
    [Fact]
    public void GetEnumerator_WhenCollectionIsWalked_YieldsNodesInOrder()
    {
        // Arrange
        var heading = new DocumentHeading(1, "Title");
        var paragraph = new DocumentParagraph("Body");
        var document = new Document { Blocks = { heading, paragraph } };
        var direct = new List<DocumentBlock>();

        // Act
        foreach (var block in document.Blocks)
        {
            direct.Add(block);
        }

        var viaInterface = document.Blocks.ToList();

        // Assert
        direct.ShouldBe([heading, paragraph]);
        viaInterface.ShouldBe([heading, paragraph]);
    }

    /// <summary>Verifies an inline collection enforces the same single-owner rule as a block
    /// collection, so an inline cannot flow inside two paragraphs at once.</summary>
    [Fact]
    public void Add_WhenInlineAlreadyBelongsToAParagraph_ThrowsArgumentException()
    {
        // Arrange
        var first = new DocumentParagraph();
        var second = new DocumentParagraph();
        var run = new DocumentTextRun("Shared");
        first.Inlines.Add(run);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => second.Inlines.Add(run));
        first.Inlines.Count.ShouldBe(1);
        second.Inlines.Count.ShouldBe(0);
    }

    /// <summary>Verifies a list-item collection enforces the same single-owner rule, so an item
    /// cannot appear in two lists at once.</summary>
    [Fact]
    public void Add_WhenListItemAlreadyBelongsToAList_ThrowsArgumentException()
    {
        // Arrange
        var first = new DocumentList();
        var second = new DocumentList();
        var item = new DocumentListItem("Shared");
        first.Items.Add(item);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => second.Items.Add(item));
        first.Items.Count.ShouldBe(1);
        second.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies a node nested several levels below a document is still reported as owned, so
    /// the single-owner rule holds for a whole subtree rather than only its root.</summary>
    [Fact]
    public void Add_WhenNodeIsNestedBelowADocument_IsStillRefusedElsewhere()
    {
        // Arrange
        var run = new DocumentTextRun("Deep");
        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(run);
        var item = new DocumentListItem(paragraph);
        var list = new DocumentList { Items = { item } };
        _ = new Document { Blocks = { list } };
        var other = new DocumentParagraph();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => other.Inlines.Add(run));
        paragraph.Inlines[0].ShouldBeSameAs(run);
    }

    /// <summary>Verifies a node cannot become its own parent.</summary>
    [Fact]
    public void Add_WhenNodeWouldOwnItself_ThrowsAndPreservesDetachedTree()
    {
        // Arrange
        var quote = new DocumentBlockQuote();

        // Act
        var action = () => quote.Blocks.Add(quote);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        quote.Blocks.Count.ShouldBe(0);
        quote.IsAttached.ShouldBeFalse();
    }

    /// <summary>Verifies inserting an ancestor below its descendant cannot create an ownership cycle.</summary>
    [Fact]
    public void Add_WhenCandidateContainsCollectionOwner_ThrowsAndPreservesBothTrees()
    {
        // Arrange
        var ancestor = new DocumentBlockQuote();
        var descendant = new DocumentBlockQuote();
        ancestor.Blocks.Add(descendant);

        // Act
        var action = () => descendant.Blocks.Add(ancestor);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        ancestor.Blocks.ShouldBe([descendant]);
        descendant.Blocks.Count.ShouldBe(0);
        ancestor.IsAttached.ShouldBeFalse();
        descendant.ParentNode.ShouldBeSameAs(ancestor);
    }

    /// <summary>Verifies hostile programmatic nesting is rejected before recursive layout can
    /// exhaust the process stack.</summary>
    [Fact]
    public void Add_WhenTreeWouldExceedMaximumDepth_ThrowsBeforeMutation()
    {
        // Arrange
        DocumentBlock root = new DocumentParagraph();

        for (var depth = 1; depth < DocumentTreeDepthValidator.MaximumDepth; depth++)
        {
            root = new DocumentBlockQuote(root);
        }

        var candidate = new DocumentBlockQuote();

        // Act
        var action = () => candidate.Blocks.Add(root);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        candidate.Blocks.ShouldBeEmpty();
        root.IsAttached.ShouldBeFalse();
    }

    /// <summary>Verifies dispatcher access is checked before an attached collection changes.</summary>
    [Fact]
    public async Task Add_WhenAttachedDocumentIsMutatedOffDispatcher_ThrowsBeforeMutationAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var document = new Document();
        await dispatcher.InvokeAsync(
            () => document.Attach(dispatcher),
            TestContext.Current.CancellationToken);
        var candidate = new DocumentParagraph("candidate");

        // Act
        var action = () => document.Blocks.Add(candidate);

        // Assert
        _ = action.ShouldThrow<InvalidOperationException>();
        document.Blocks.Count.ShouldBe(0);
        candidate.IsAttached.ShouldBeFalse();
    }

    /// <summary>Verifies disposal is checked before an owned collection detaches its nodes.</summary>
    [Fact]
    public void Clear_WhenOwningDocumentIsDisposed_ThrowsBeforeMutation()
    {
        // Arrange
        var paragraph = new DocumentParagraph("body");
        var document = new Document { Blocks = { paragraph } };
        document.Dispose();

        // Act
        var action = document.Blocks.Clear;

        // Assert
        _ = action.ShouldThrow<ObjectDisposedException>();
        document.Blocks.ShouldBe([paragraph]);
        paragraph.OwnerDocument.ShouldBeSameAs(document);
    }
}
