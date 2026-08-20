// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies immutable result behavior for save-file dialog values. SaveFileDialogTests
/// only ever observes these properties as an incidental outcome of a full dialog flow; this suite
/// exercises the type's own contract directly, including the validation FromPath's internal
/// factory performs before a confirmed instance can be observed.</summary>
public sealed class SaveFileResultTests
{
    /// <summary>Verifies a confirmed result retains its canonical path and reports itself confirmed.</summary>
    [Fact]
    public void FromPath_WhenPathIsValid_OwnsPathAndReportsConfirmed()
    {
        // Arrange
        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-result-contract", "report.csv"));

        // Act
        var confirmed = SaveFileResult.FromPath(path);
        var cancelled = SaveFileResult.Cancelled;

        // Assert
        confirmed.IsConfirmed.ShouldBeTrue();
        confirmed.Path.ShouldBe(path);
        cancelled.IsConfirmed.ShouldBeFalse();
        cancelled.Path.ShouldBeNull();
    }

    /// <summary>Verifies every malformed path is rejected before a confirmed instance can be observed.</summary>
    [Fact]
    public void FromPath_WhenPathIsInvalid_ThrowsValidationExceptions()
    {
        _ = Should.Throw<ArgumentNullException>(() => SaveFileResult.FromPath(null!));
        _ = Should.Throw<ArgumentException>(() => SaveFileResult.FromPath(" "));
        _ = Should.Throw<ArgumentException>(() => SaveFileResult.FromPath("report.csv"));
    }
}
