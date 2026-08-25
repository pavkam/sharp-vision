// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one leveled heading of flowing inline content.</summary>
/// <remarks>
/// A terminal has no font sizes, so levels differentiate through weight, color, and underline rather
/// than scale: levels 1 and 2 render with <see cref="DocumentStyle.HeadingFace"/>, and levels 3
/// through 6 render in the body face with bold weight added. The document resolves both at paint
/// time, so a theme swap restyles every heading immediately.
/// </remarks>
[PublicAPI]
public sealed class DocumentHeading: DocumentBlock
{
    /// <summary>The lowest valid heading level.</summary>
    public const int MinimumLevel = 1;

    /// <summary>The highest valid heading level.</summary>
    public const int MaximumLevel = 6;

    /// <summary>Initializes an empty heading at a valid level.</summary>
    /// <param name="level">The heading level from <see cref="MinimumLevel"/> through <see cref="MaximumLevel"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is outside the valid range.</exception>
    public DocumentHeading(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, MinimumLevel);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, MaximumLevel);

        Inlines = new DocumentInlineCollection(this);
        Level = level;
    }

    /// <summary>Initializes a heading with one non-null inline-markup text run at a valid level.</summary>
    /// <param name="level">The heading level from <see cref="MinimumLevel"/> through <see cref="MaximumLevel"/>.</param>
    /// <param name="text">The non-null markup string.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is outside the valid range.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentHeading(int level, string text) : this(level)
    {
        ArgumentNullException.ThrowIfNull(text);
        Inlines.Add(new DocumentTextRun(text));
    }

    /// <summary>Gets or sets the heading level from <see cref="MinimumLevel"/> through
    /// <see cref="MaximumLevel"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the valid range.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public int Level
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, MinimumLevel);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumLevel);
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets the owned ordered inline content.</summary>
    public DocumentInlineCollection Inlines { get; }
}
