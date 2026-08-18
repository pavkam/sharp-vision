// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>Verifies the XTGETTCAP request wire encoding.</summary>
public sealed class XtermGetCapTests
{
    /// <summary>Verifies approved names are uppercase-hex encoded exactly.</summary>
    [Fact]
    public void Query_WhenNamesAreApproved_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        CapabilityName[] names = [CapabilityName.DirectColor, CapabilityName.Up];

        XtermGetCap.Query(new ProtocolWriter(destination), names);

        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe(
            "\u001bP+q524742;6B63757531\u001b\\");
    }
}
