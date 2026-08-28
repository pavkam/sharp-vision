// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Compares one registered non-appearance Theme value across a prospective transition.</summary>
internal interface IThemeValueDependency
{
    /// <summary>Gets the earliest phase affected when the registered value changes.</summary>
    /// <param name="previous">The previous Theme, or null for the library fallback.</param>
    /// <param name="current">The prospective Theme, or null for the library fallback.</param>
    /// <returns>The declared impact when resolved values differ; otherwise none.</returns>
    public InvalidationImpact GetImpact(Theme? previous, Theme? current);
}
