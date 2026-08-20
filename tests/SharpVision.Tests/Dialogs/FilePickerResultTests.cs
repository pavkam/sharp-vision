// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies immutable result behavior for file-picker values.</summary>
public sealed class FilePickerResultTests
{
    /// <summary>Verifies accepted and cancelled results own stable path snapshots.</summary>
    [Fact]
    public void Create_WhenResultIsProduced_OwnsPathsAndExposesSinglePathConvenience()
    {
        // Arrange: fully qualified paths must be constructed per platform — a bare "/workspace/a.cs"
        // is fully qualified on Unix but only rooted (not fully qualified) on Windows, which
        // Path.IsPathFullyQualified requires a drive or UNC root to satisfy.
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-result-contract"));
        var first = Path.Combine(root, "a.cs");
        var second = Path.Combine(root, "b.cs");
        var paths = new[] { first, second };

        // Act
        var accepted = FilePickerResult.Accept(paths);
        paths[0] = Path.Combine(root, "changed.cs");
        var cancelled = FilePickerResult.Cancelled;

        // Assert
        accepted.IsAccepted.ShouldBeTrue();
        accepted.Paths.Count.ShouldBe(2);
        accepted.Paths[0].ShouldBe(first);
        accepted.Paths[1].ShouldBe(second);
        accepted.SelectedPath.ShouldBe(first);
        cancelled.IsAccepted.ShouldBeFalse();
        cancelled.Paths.ShouldBeEmpty();
        cancelled.SelectedPath.ShouldBeNull();
    }

    /// <summary>Verifies every malformed accepted-path argument is rejected before an instance can
    /// be observed - Accept is reached internally from FilePickerDialog's own accept flow, but its
    /// own validation contract was previously never exercised directly.</summary>
    [Fact]
    public void Accept_WhenPathsAreInvalid_ThrowsValidationExceptions()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-result-invalid"));
        var valid = Path.Combine(root, "a.cs");

        _ = Should.Throw<ArgumentNullException>(() => FilePickerResult.Accept(null!));
        _ = Should.Throw<ArgumentException>(() => FilePickerResult.Accept([]));
        _ = Should.Throw<ArgumentNullException>(() => FilePickerResult.Accept([null!]));
        _ = Should.Throw<ArgumentException>(() => FilePickerResult.Accept([" "]));
        _ = Should.Throw<ArgumentException>(() => FilePickerResult.Accept(["a.cs"]));
        _ = Should.Throw<ArgumentException>(() => FilePickerResult.Accept([valid, "relative.cs"]));
    }
}
