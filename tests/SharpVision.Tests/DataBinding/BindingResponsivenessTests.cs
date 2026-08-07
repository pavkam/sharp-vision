// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using Performance;

using SharpVision.DataBinding;

using Support;

/// <summary>Gates scalar binding allocation and feedback responsiveness.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class BindingResponsivenessTests
{
    /// <summary>Verifies warmed direct notifications retain a bounded per-update allocation.</summary>
    [Fact]
    public void Source_WhenTenThousandChangesCommit_HasBoundedManagedAllocation()
    {
        var model = new BindingModel { Name = "A" };
        var target = new ControlText();
        using var binding = target.Bind(model, source => source.Name);
        model.Name = "B";
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++)
        {
            model.Name = (index & 1) == 0 ? "A" : "B";
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBeLessThanOrEqualTo(256L * 10_000);
        target.Content.ShouldBe("B");
    }

    /// <summary>Verifies repeated target changes produce one model commit each without feedback recursion.</summary>
    [Fact]
    public void Target_WhenTenThousandChangesCommit_DoesNotRecurse()
    {
        var model = new BindingModel { Name = "A" };
        var target = new TextInput();
        using var binding = target.Bind(model, source => source.Name);

        for (var index = 0; index < 10_000; index++)
        {
            target.Text = (index & 1) == 0 ? "B" : "A";
        }

        model.Name.ShouldBe("A");
        target.Text.ShouldBe("A");
    }
}
