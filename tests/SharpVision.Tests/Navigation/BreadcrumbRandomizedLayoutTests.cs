// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies fixed-seed breadcrumb layout equivalence and resize reversibility.</summary>
public sealed class BreadcrumbRandomizedLayoutTests
{
    /// <summary>Verifies 400 path, availability, current, and width mutations against independent arithmetic.</summary>
    [Fact]
    public void Layout_WhenFixedSeedMutatesPathAndWidths_MatchesIndependentModel()
    {
        const int seed = 20260831;
        var transcript = BreadcrumbLayoutModel.Run(seed, operationCount: 400);

        transcript.Actual.ShouldBe(transcript.Expected, transcript.Description);
    }

    /// <summary>Verifies overflow never replaces or reparents semantic item identities across resize cycles.</summary>
    [Fact]
    public async Task Render_WhenWidthCyclesWideTinyWide_RestoresOriginalSemanticItemsAsync()
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem { Text = "Root" };
        var folder = new BreadcrumbItem { Text = "界" };
        var leaf = new BreadcrumbItem { Text = "Leaf" };
        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(folder);
        breadcrumb.Items.Add(leaf);
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(12, 1),
            TestContext.Current.CancellationToken);
        var parents = breadcrumb.Items.Select(item => item.Parent).ToArray();

        await surface.ResizeAsync(new Size(1, 1));
        surface.ShouldRender("…");
        await surface.ResizeAsync(new Size(12, 1));

        surface.ShouldRender("Root›界›Leaf");
        breadcrumb.Items.ShouldBe([root, folder, leaf]);
        breadcrumb.Items.Select(item => item.Parent).ShouldBe(parents);
    }
}
