using SharpVision.Terminal.Protocols;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

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
}
