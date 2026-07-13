// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Marks a control whose resolved style cascades to its descendants.</summary>
/// <remarks>
/// During resolution every ancestor implementing this interface contributes its themed and
/// per-instance style values to a descendant, ordered so the nearest scope wins over farther ones
/// and a descendant's own values win over any scope. This is the public extension point behind
/// container-driven styling such as the built-in list; a third-party container (for example a tree)
/// opts in simply by implementing this interface.
/// </remarks>
public interface IStyleScope
{
}
