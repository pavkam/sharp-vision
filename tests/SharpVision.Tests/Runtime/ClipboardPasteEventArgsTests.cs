// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Terminal.Clipboard;

/// <summary>Verifies terminal-initiated clipboard paste event ownership and validation.</summary>
public sealed class ClipboardPasteEventArgsTests
{
    /// <summary>Verifies mutable caller storage cannot change the published notification.</summary>
    [Fact]
    public void Constructor_WhenCallerStorageChanges_PreservesOwnedValues()
    {
        var mimeTypes = new[] { "text/plain", "image/png" };
        var password = "secret"u8.ToArray();

        var args = new ClipboardPasteEventArgs(Selection.Primary, mimeTypes, password);
        mimeTypes[0] = "application/json";
        password.AsSpan().Clear();

        args.Selection.ShouldBe(Selection.Primary);
        args.MimeTypes.ShouldBe(["text/plain", "image/png"]);
        args.Password.ToArray().ShouldBe("secret"u8.ToArray());
    }

    /// <summary>Verifies malformed MIME inventory entries are rejected before state is published.</summary>
    /// <param name="mimeType">The invalid MIME entry.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("text/plain image/png")]
    public void Constructor_WhenMimeTypeIsNotAToken_Throws(string mimeType)
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new ClipboardPasteEventArgs(Selection.Clipboard, [mimeType], ReadOnlyMemory<byte>.Empty));

        exception.ParamName.ShouldBe("mimeTypes");
    }
}
