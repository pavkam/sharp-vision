// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the inherited, opt-in semantic text-selection contract.</summary>
public sealed partial class ControlBaseTests
{
    /// <summary>Verifies every control starts without active text-selection behavior.</summary>
    [Fact]
    public void Constructor_WhenCreated_DisablesTextSelection()
    {
        var control = new Stack { Children = { new ControlText("text") } };

        control.IsTextSelectionEnabled.ShouldBeFalse();
        control.TextSelection.ShouldBe(default);
        control.SelectedText.ShouldBeEmpty();
        _ = Should.Throw<InvalidOperationException>(
            () => control.SetTextSelection(new Selection(0, 1)));
    }

    /// <summary>Verifies one enabled aggregate validates and publishes a directional range.</summary>
    [Fact]
    public void SetTextSelection_WhenEnabled_PublishesOneDirectionalChange()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("A e\u0301") }
        };
        TextSelectionChangedEventArgs? observed = null;
        control.TextSelectionChanged += (_, eventArgs) => observed = eventArgs;

        control.SetTextSelection(new Selection(4, 2));

        control.TextSelection.ShouldBe(new Selection(4, 2));
        control.SelectedText.ShouldBe("e\u0301");
        control.CopySelectedText().ShouldBe("e\u0301");
        observed.ShouldNotBeNull().PreviousSelection.ShouldBe(default);
        observed.Selection.ShouldBe(new Selection(4, 2));
    }

    /// <summary>Verifies reentry from an earlier common-event subscriber prevents later subscribers
    /// from receiving the obsolete outer transition.</summary>
    [Fact]
    public void TextSelectionChanged_WhenSubscriberReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("abcd") }
        };
        var observed = new List<(Selection EventSelection, Selection LiveSelection)>();
        control.TextSelectionChanged += (_, eventArgs) =>
        {
            if (eventArgs.Selection == new Selection(0, 1))
            {
                control.SetTextSelection(new Selection(0, 2));
            }
        };
        control.TextSelectionChanged += (_, eventArgs) =>
            observed.Add((eventArgs.Selection, control.TextSelection));

        // Act
        control.SetTextSelection(new Selection(0, 1));

        // Assert
        observed.ShouldBe([(new Selection(0, 2), new Selection(0, 2))]);
    }

    /// <summary>Verifies invalid grapheme endpoints are rejected without observable mutation.</summary>
    [Fact]
    public void SetTextSelection_WhenEndpointSplitsGrapheme_ThrowsBeforeMutation()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("e\u0301") }
        };
        var raised = 0;
        control.TextSelectionChanged += (_, _) => raised++;

        _ = Should.Throw<ArgumentException>(
            () => control.SetTextSelection(new Selection(0, 1)));

        control.TextSelection.ShouldBe(default);
        raised.ShouldBe(0);
    }

    /// <summary>Verifies disabling commits capability state, cancels the range, and publishes once.</summary>
    [Fact]
    public void IsTextSelectionEnabled_WhenDisabled_ClearsSelectionOnce()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("text") }
        };
        control.SetTextSelection(new Selection(0, 4));
        var raised = 0;
        control.TextSelectionChanged += (_, _) => raised++;

        control.IsTextSelectionEnabled = false;

        control.IsTextSelectionEnabled.ShouldBeFalse();
        control.TextSelection.ShouldBe(default);
        control.SelectedText.ShouldBeEmpty();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies replacing a semantic source clears a range even when its text is identical.</summary>
    [Fact]
    public void TextSelection_WhenSourceIdentityChanges_ClearsStaleRangeOnce()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("same") }
        };
        control.SetTextSelection(new Selection(0, 4));
        var raised = 0;
        control.TextSelectionChanged += (_, _) => raised++;

        control.Children.Clear();
        control.Children.Add(new ControlText("same"));
        var selection = control.TextSelection;

        selection.ShouldBe(default);
        control.SelectedText.ShouldBeEmpty();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies a nonzero collapsed caret is stale when its source identity changes.</summary>
    [Fact]
    public void TextSelection_WhenCollapsedCaretSourceChanges_ClearsToDefault()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("same") }
        };
        control.SetTextSelection(new Selection(4, 4));

        control.Children.Clear();
        control.Children.Add(new ControlText("same"));

        control.TextSelection.ShouldBe(default);
    }
}
