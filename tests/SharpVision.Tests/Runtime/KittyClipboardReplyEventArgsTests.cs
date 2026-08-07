// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Terminal.Clipboard;
using Terminal.Kitty.Clipboard;

/// <summary>Proves clipboard reply events own the completed payload they publish.</summary>
public sealed class KittyClipboardReplyEventArgsTests
{
    /// <summary>Verifies one reply cannot claim success through both clipboard protocols.</summary>
    [Fact]
    public void Constructor_WhenBothSuccessPayloadsArePresent_Throws()
    {
        using var kittyResult = new KittyClipboardResult([]);

        var action = () => new KittyClipboardReplyEventArgs(
            Selection.Clipboard,
            kittyResult,
            new byte[] { 1 },
            KittyClipboardReplyStatus.None,
            null);

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("text");
    }

    /// <summary>Verifies an event cannot publish an undefined clipboard selection.</summary>
    [Fact]
    public void Constructor_WhenSelectionIsUndefined_Throws()
    {
        var selection = (Selection) int.MaxValue;

        var action = () => new KittyClipboardReplyEventArgs(
            selection,
            null,
            null,
            KittyClipboardReplyStatus.None,
            null);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("selection");
    }

    /// <summary>Verifies an event cannot publish an undefined Kitty failure status.</summary>
    [Fact]
    public void Constructor_WhenFailureIsUndefined_Throws()
    {
        var failure = (KittyClipboardReplyStatus) int.MaxValue;

        var action = () => new KittyClipboardReplyEventArgs(
            Selection.Clipboard,
            null,
            null,
            failure,
            null);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("failure");
    }

    /// <summary>Verifies later caller mutation cannot rewrite a previously constructed OSC 52 reply.</summary>
    [Fact]
    public void Constructor_WhenTextBufferMutates_PreservesSnapshot()
    {
        byte[] text = [1, 2, 3];
        var eventArgs = new KittyClipboardReplyEventArgs(
            Selection.Clipboard,
            null,
            text,
            KittyClipboardReplyStatus.None,
            null);

        text[0] = 9;

        eventArgs.Text.ShouldNotBeNull().Span[0].ShouldBe((byte) 1);
    }
}
