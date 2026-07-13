// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Protocols;

using TerminalAttributes = TerminalAttributes;
using TerminalStyle = TerminalStyle;

/// <summary>Defines styled text carrying a semantic terminal hyperlink target.</summary>
public sealed class Hyperlink: Inline
{
    /// <summary>Initializes detached non-null link content and target.</summary>
    /// <param name="content">The non-null visible UTF-16 content.</param>
    /// <param name="target">The non-empty control-free target.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="target"/> is invalid.</exception>
    public Hyperlink(string content, string target)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
        Target = ValidateTarget(target);
    }

    /// <summary>Gets or sets non-null visible UTF-16 content.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
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
    }

    /// <summary>Gets or sets the non-empty control-free semantic target.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value is empty or contains a control.</exception>
    public string Target
    {
        get;
        set
        {
            var valid = ValidateTarget(value);
            VerifyMutable();

            if (field == valid)
            {
                return;
            }

            field = valid;
            Changed();
        }
    }

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

    private static string ValidateTarget(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = new TerminalStyle(hyperlink: value);
        return value;
    }
}
