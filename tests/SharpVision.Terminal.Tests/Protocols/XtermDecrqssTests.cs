// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>Verifies the DECRQSS request wire encoding.</summary>
public sealed class XtermDecrqssTests
{
    /// <summary>Verifies every approved selector uses the exact official command.</summary>
    [Theory]
    [InlineData(StatusName.Rendition, "\u001bP$qm\u001b\\")]
    [InlineData(StatusName.CursorStyle, "\u001bP$q q\u001b\\")]
    [InlineData(StatusName.VerticalMargins, "\u001bP$qr\u001b\\")]
    [InlineData(StatusName.HorizontalMargins, "\u001bP$qs\u001b\\")]
    [InlineData(StatusName.ModifyOtherKeys, "\u001bP$q>4m\u001b\\")]
    [InlineData(StatusName.FormatOtherKeys, "\u001bP$q>4f\u001b\\")]
    public void Query_WhenNameIsApproved_WritesExactBytes(StatusName name, string expected)
    {
        var destination = new ArrayBufferWriter<byte>();

        XtermDecrqss.Query(new ProtocolWriter(destination), name);

        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe(expected);
    }
}
