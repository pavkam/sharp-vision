// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

using SharpVision.DataBinding;
using SharpVision.Tests.DataBinding.Support;

/// <summary>Verifies Pager's target-owned two-way PageIndex binding.</summary>
public sealed class PagerBindingTests
{
    /// <summary>Verifies initial, model-to-control, and control-to-model synchronization.</summary>
    [Fact]
    public void Bind_WhenEitherSideChanges_SynchronizesPageIndexTwoWay()
    {
        var model = new BindingModel { Number = 2 };
        var pager = new Pager { PageCount = 5 };
        using var binding = pager.Bind(model, source => source.Number);

        pager.PageIndex.ShouldBe(2);

        model.Number = 3;
        pager.PageIndex.ShouldBe(3);

        pager.PageIndex = 4;
        model.Number.ShouldBe(4);
    }

    /// <summary>Verifies an invalid model value is rejected without clamping it back into the model.</summary>
    [Fact]
    public void Bind_WhenSourceIndexIsOutsidePageCount_ThrowsWithoutReverseWrite()
    {
        var model = new BindingModel { Number = 5 };
        var pager = new Pager { PageCount = 3 };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => pager.Bind(model, source => source.Number));

        model.Number.ShouldBe(5);
        pager.PageIndex.ShouldBe(0);
    }
}
