// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Marks a themeable type as a mergeable fragment - <see cref="Theme.Overlay"/> patches a
/// property whose type implements this interface recursively instead of replacing it outright.</summary>
[PublicAPI]
public interface IAppearanceFragment
{
    /// <summary>Returns an independent copy safe for the overlay to mutate through reflection
    /// without aliasing the original instance's own property values.</summary>
    public IAppearanceFragment Clone();

    /// <summary>Re-checks any invariant that spans more than one member, once the overlay has
    /// finished writing every override onto this fragment.</summary>
    /// <remarks>
    /// Per-member checks belong in the member's own <c>init</c> accessor, which the overlay's
    /// reflective write and an ordinary <c>with</c> expression both reach. A relation between
    /// several members has no such home - no accessor can see members that have not been written
    /// yet - so the overlay calls this once the fragment is whole. Fragments with no cross-member
    /// invariant need not override the default.
    /// </remarks>
    /// <exception cref="ArgumentException">The completed fragment violates a cross-member invariant.</exception>
    public void Validate()
    {
    }
}
