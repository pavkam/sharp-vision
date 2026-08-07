// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using SharpVision.Surfaces;

/// <summary>Exposes the protected floating-surface lifecycle for focused contract tests.</summary>
internal sealed class FloatingSurfaceProbe: FloatingSurfaceBase
{
    /// <summary>Gets whether the common surface lifecycle is currently presented.</summary>
    internal bool IsPresented => SurfacePresented;

    /// <summary>Commits a presented surface with the supplied visible bounds.</summary>
    /// <param name="bounds">The committed surface rectangle.</param>
    internal void PublishBounds(Rect bounds) => OpenSurface(() => SurfaceBounds = bounds);

    /// <summary>Runs one test-owned family commit through the protected opening transaction.</summary>
    /// <param name="commitOpenState">The non-null test-owned state commit.</param>
    internal void OpenForTest(Action commitOpenState) => OpenSurface(commitOpenState);

    /// <summary>Commits bounds and then raises the supplied failure from the family callback.</summary>
    /// <param name="bounds">The provisional surface rectangle.</param>
    /// <param name="failure">The non-null family callback failure.</param>
    internal void PublishBoundsAndThrow(Rect bounds, Exception failure) =>
        OpenSurface(() =>
        {
            SurfaceBounds = bounds;
            throw failure;
        });

    /// <summary>Closes the presented surface with optional family-specific state callbacks.</summary>
    /// <param name="commitClosingState">Commits the family-specific closing state.</param>
    /// <param name="commitUnavailableState">Makes family-specific content unavailable.</param>
    /// <returns><see langword="true"/> when a presented surface was closed.</returns>
    internal bool CloseForTest(
        Action? commitClosingState = null,
        Action? commitUnavailableState = null) =>
        CloseSurface(
            commitClosingState ?? (static () => { }),
            commitUnavailableState ?? (static () => { }));

    /// <summary>Enters an application-owned modal presentation rooted at this surface.</summary>
    /// <param name="outsideInteraction">The outside-input policy.</param>
    /// <param name="initialFocus">The optional initial focus target.</param>
    /// <returns>The modal lifetime owned by this surface.</returns>
    internal ModalScope EnterModalForTest(
        OutsideInteraction outsideInteraction,
        ControlBase? initialFocus = null) =>
        EnterSurfaceModal(outsideInteraction, initialFocus);

    /// <summary>Ends this surface's application-owned modal presentation.</summary>
    internal void ExitModalForTest() => ExitSurfaceModal();
}
