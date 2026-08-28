// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies exhaustive binding teardown when user event accessors fail.</summary>
public sealed partial class BindingTests
{
    /// <summary>Verifies one source-unsubscription failure does not retain registry membership.</summary>
    [Fact]
    public void Dispose_WhenSourceRemovalThrows_StillAllowsTargetPropertyRebinding()
    {
        var source = new ThrowingBindingNode { Value = "first", ThrowOnRemove = true };
        var target = new ControlText();
        var binding = target.Bind(source, value => value.Value);

        _ = Should.Throw<InvalidOperationException>(binding.Dispose);
        var replacement = new BindingModel { Name = "second" };
        using var rebound = target.Bind(replacement, value => value.Name);

        binding.IsDisposed.ShouldBeTrue();
        target.Content.ShouldBe("second");
    }
}
