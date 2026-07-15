// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests.PackageSpecimens;

/// <summary>Executes a dependency-free public-API smoke proof from the packed consumer assembly.</summary>
public static class PackageProbe
{
    /// <summary>Constructs and mutates externally authored component roles without source-project access.</summary>
    /// <returns>A completed task after every public-surface assertion succeeds.</returns>
    public static Task RunAsync()
    {
        var card = new StatusCard("Service", "Ready")
        {
            Status = "Busy"
        };

        if (card.Status != "Busy" || typeof(StatusCard).GetProperty("Children") is not null)
        {
            throw new InvalidOperationException("Composite public-surface proof failed.");
        }

        var cloud = new TagCloud();
        cloud.Add("red");
        cloud.Add("blue");

        return cloud.Count != 2 || cloud.Tags[1] != "blue" || typeof(TagCloud).GetProperty("Children") is not null
            ? throw new InvalidOperationException("Items public-surface proof failed.")
            : Task.CompletedTask;
    }
}
