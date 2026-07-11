using SharpVision.Terminal.Protocols;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

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
            if (value.HasValue)
            {
                _ = new TerminalStyle(attributes: value.Value);
            }

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
