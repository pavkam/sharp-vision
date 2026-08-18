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
}
