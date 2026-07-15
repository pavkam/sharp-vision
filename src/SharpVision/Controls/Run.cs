// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


/// <summary>Defines one styled mutable UTF-16 text run.</summary>
public sealed class Run: Inline
{
    /// <summary>Initializes an empty detached run.</summary>
    public Run()
    {
    }

    /// <summary>Initializes a detached run with non-null content.</summary>
    /// <param name="content">The non-null UTF-16 content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public Run(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    /// <summary>Gets or sets non-null UTF-16 content.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public string Content
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
        }
    } = string.Empty;

    /// <summary>Gets or sets an optional foreground override.</summary>
    public Color? Foreground
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
        }
    }

    /// <summary>Gets or sets an optional background override.</summary>
    public Color? Background
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
        }
    }

    /// <summary>Gets or sets optional rendition attributes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown flags.</exception>
    public TerminalAttributes? Attributes
    {
        get;
        set
        {
            Decoration.Validate(value, Underline, UnderlineColor);

            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
        }
    }

    /// <summary>Gets or sets an optional typed underline variant.</summary>
    /// <exception cref="ArgumentException">The resulting decoration fields conflict.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public Underline? Underline
    {
        get;
        set
        {
            Decoration.Validate(Attributes, value, UnderlineColor);
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
        }
    }

    /// <summary>Gets or sets an optional semantic underline color.</summary>
    /// <exception cref="ArgumentException">The resulting decoration fields conflict.</exception>
    public Color? UnderlineColor
    {
        get;
        set
        {
            Decoration.Validate(Attributes, Underline, value);
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
        }
    }
}
