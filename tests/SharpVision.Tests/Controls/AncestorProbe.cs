// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exposes the protected <see cref="ControlBase.FindAncestor{T}"/> helper for testing.</summary>
internal sealed class AncestorProbe: ControlBase
{
    /// <summary>Returns the nearest ancestor assignable to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The ancestor control type to locate.</typeparam>
    /// <returns>The nearest matching ancestor, or null when none exists.</returns>
    internal T? ExposedFindAncestor<T>() where T : ControlBase => FindAncestor<T>();
}
