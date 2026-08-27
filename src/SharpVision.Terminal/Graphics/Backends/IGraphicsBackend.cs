// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

using Graphics;

using Kitty.Graphics;

using Rendering;

/// <summary>Defines one renderer-owned transactional terminal graphics backend.</summary>
/// <remarks>
/// <see cref="Prepare"/> must perform every allocation required by the following synchronous
/// writers. The renderer orders uploads, cell replacement, placements, and stale removals in one
/// transport batch. Every successful prepare is concluded exactly once: <see cref="Commit"/> after
/// a successful flush, including byte-quiet preparations whose result reports unchanged, or
/// <see cref="Invalidate"/> after any subsequent failure.
/// Active frame placement spans never contain <see cref="Placement.Empty"/>. Implementations assert
/// <see cref="Placement.IsEmpty"/> is false before dereferencing <see cref="Placement.Image"/>.
/// </remarks>
internal interface IGraphicsBackend: IDisposable
{
    /// <summary>Accepts an asynchronous Kitty graphics response relevant to retained ownership.</summary>
    /// <param name="response">The non-null decoded response.</param>
    public void Accept(KittyGraphicsResponse response) => ArgumentNullException.ThrowIfNull(response);

    /// <summary>Prepares a finite transaction without producing terminal output.</summary>
    /// <param name="front">The prior semantic front frame, or null before the first commit.</param>
    /// <param name="back">The borrowed target frame.</param>
    /// <param name="full">Whether remote graphics state must be reconstructed completely.</param>
    /// <param name="context">The active profile and measured pixel geometry.</param>
    /// <returns>The finite prepared operation counts.</returns>
    public GraphicsBackendResult Prepare(
        Frame? front,
        Frame back,
        bool full,
        GraphicsContext context = default);

    /// <summary>Writes every prepared image upload without allocating.</summary>
    /// <param name="destination">The renderer-owned bounded batch.</param>
    public void WriteUploads(IBufferWriter<byte> destination);

    /// <summary>Writes every prepared replacement placement without allocating.</summary>
    /// <param name="destination">The renderer-owned bounded batch.</param>
    public void WritePlacements(IBufferWriter<byte> destination);

    /// <summary>Writes every prepared stale placement or image removal without allocating.</summary>
    /// <param name="destination">The renderer-owned bounded batch.</param>
    public void WriteRemovals(IBufferWriter<byte> destination);

    /// <summary>Commits prepared local backend state after successful or byte-quiet completion.</summary>
    /// <remarks>This method must not throw and must not allocate.</remarks>
    public void Commit();

    /// <summary>Invalidates prepared state and requires complete reconstruction on the next prepare.</summary>
    /// <remarks>
    /// Implementations must conservatively retain identifiers whose commands may have partially
    /// reached the terminal until a later remote cleanup is flushed. This method must not throw.
    /// </remarks>
    public void Invalidate();

    /// <summary>Prepares bounded commands that release every committed or uncertain remote identity.</summary>
    /// <returns>The number of prepared remote cleanup commands.</returns>
    public int PrepareCleanup();

    /// <summary>Writes the prepared remote cleanup without allocating.</summary>
    /// <param name="destination">The renderer-owned bounded batch.</param>
    public void WriteCleanup(IBufferWriter<byte> destination);

    /// <summary>Commits local release of committed and uncertain identifiers after cleanup flush.</summary>
    /// <remarks>This method must not throw and must not allocate.</remarks>
    public void CommitCleanup();
}
