// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Terminal.Clipboard;
using Terminal.Kitty.Clipboard;

/// <summary>Proves clipboard reply events own the completed payload they publish.</summary>
public sealed class KittyClipboardReplyEventArgsTests
{
    /// <summary>Verifies later caller mutation cannot rewrite a previously constructed OSC 52 reply.</summary>
    [Fact]
    public void Constructor_WhenTextBufferMutates_PreservesSnapshot()
    {
        byte[] text = [1, 2, 3];
        var eventArgs = new KittyClipboardReplyEventArgs(
            Selection.Clipboard,
            null,
            text,
            ReplyStatus.None,
            null);

        text[0] = 9;

        eventArgs.Text.ShouldNotBeNull().Span[0].ShouldBe((byte) 1);
    }
}
