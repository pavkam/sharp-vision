// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies save-file dialog options reject invalid state at their public boundary.</summary>
public sealed class SaveFileOptionsTests
{
    /// <summary>Verifies a null filename is rejected before it can replace the current value.</summary>
    [Fact]
    public void InitialFileName_WhenNull_ThrowsArgumentNullExceptionWithoutChangingValue()
    {
        var options = new SaveFileOptions { InitialFileName = "report.csv" };

        _ = Should.Throw<ArgumentNullException>(() => options.InitialFileName = null!);

        options.InitialFileName.ShouldBe("report.csv");
    }
}
