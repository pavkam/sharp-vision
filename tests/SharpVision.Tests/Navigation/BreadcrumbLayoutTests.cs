// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies fixed-seed breadcrumb layout equivalence and resize reversibility.</summary>
public sealed class BreadcrumbLayoutTests
{
    /// <summary>Verifies the default separator reserves one cell on each side in natural measurement.</summary>
    [Fact]
    public void Measure_WhenPathUsesDefaultSeparatorSpacing_ReservesCompleteSeparatorExtents()
    {
        var breadcrumb = new Breadcrumb();
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Root" });
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Docs" });
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Leaf" });

        breadcrumb.Measure(new Constraint(width: null, 1));

        breadcrumb.DesiredSize.ShouldBe(new Size(18, 1));
    }

    /// <summary>Verifies an empty or single-entry path reserves no separator extent.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4)]
    public void Measure_WhenFewerThanTwoItemsParticipate_ReservesNoSeparatorExtent(int itemCount, int expectedWidth)
    {
        var breadcrumb = new Breadcrumb();

        for (var index = 0; index < itemCount; index++)
        {
            breadcrumb.Items.Add(new BreadcrumbItem { Text = "Root" });
        }

        breadcrumb.Measure(new Constraint(width: null, 1));

        breadcrumb.DesiredSize.Width.ShouldBe(expectedWidth);
    }

    /// <summary>Verifies asymmetric and zero separator spacing contribute exact natural cells.</summary>
    [Theory]
    [InlineData(0, 0, 9)]
    [InlineData(2, 3, 14)]
    public void Measure_WhenSeparatorSpacingChanges_UsesExactConfiguredExtent(int before, int after, int expectedWidth)
    {
        var breadcrumb = new Breadcrumb
        {
            Style = BreadcrumbStyle.Default with
            {
                SeparatorSpacingBefore = before,
                SeparatorSpacingAfter = after
            }
        };
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Root" });
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Leaf" });

        breadcrumb.Measure(new Constraint(width: null, 1));

        breadcrumb.DesiredSize.Width.ShouldBe(expectedWidth);
    }

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
        var breadcrumb = new Breadcrumb
        {
            Style = BreadcrumbStyle.Default with
            {
                SeparatorSpacingBefore = 0,
                SeparatorSpacingAfter = 0
            }
        };
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
