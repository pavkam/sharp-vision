namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Showcase;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using Wrapping = SharpVision.Text.Wrapping;

/// <summary>
/// Base documentation page control. Subclasses add live specimens by overriding
/// <see cref="BuildExamples"/> and composing child controls on the supplied stack.
/// </summary>
internal abstract class ShowcasePane: ControlStack
{
    private readonly InteractionDescription[] _interactions;
    private readonly PropertyDescription[] _properties;

    /// <summary>Initializes one detached showcase page with catalog metadata and documentation chrome.</summary>
    /// <param name="name">The non-empty concrete control type name.</param>
    /// <param name="summary">The non-empty purpose and usage summary.</param>
    /// <param name="interactions">The non-empty supported interaction descriptions.</param>
    /// <param name="properties">The non-empty property documentation sequence.</param>
    /// <exception cref="ArgumentException">Required metadata is blank or incomplete.</exception>
    /// <exception cref="ArgumentNullException">A metadata sequence is null.</exception>
    protected ShowcasePane(
        string name,
        string summary,
        IEnumerable<InteractionDescription> interactions,
        IEnumerable<PropertyDescription> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(properties);
        var copiedInteractions = interactions.ToArray();
        var copiedProperties = properties.ToArray();
        ValidateMetadata(copiedInteractions, copiedProperties);

        Name = name;
        Summary = summary;
        Interaction = string.Join(' ', copiedInteractions.Select(static item => item.Result));
        _interactions = copiedInteractions;
        _properties = copiedProperties;

        Padding = new Thickness(1);
        Spacing = 1;
        Children.Add(Heading(name, summary));
        Children.Add(Section("Practical recipe"));
        Children.Add(Narrative(summary, _interactions));
        Children.Add(Section("Examples"));
        var examples = new ControlStack { Spacing = 1 };
        BuildExamples(examples);
        Children.Add(Card(examples));
        Children.Add(Section("Technical details"));
        Children.Add(TechnicalDetails());
        Children.Add(Section("Interaction"));
        Children.Add(InteractionTable());
    }

    /// <summary>Gets the exact concrete control type name used by navigation.</summary>
    internal string Name { get; }

    /// <summary>Gets the control's concise purpose and intended use.</summary>
    internal string Summary { get; }

    /// <summary>Gets keyboard, pointer, focus, or display-only behavior guidance.</summary>
    internal string Interaction { get; }

    /// <summary>Gets immutable structured interaction descriptions.</summary>
    internal IReadOnlyList<InteractionDescription> Interactions => _interactions;

    /// <summary>Gets immutable documentation for the control's meaningful properties.</summary>
    internal IReadOnlyList<PropertyDescription> Properties => _properties;

    /// <summary>Composes the live example specimens for this control.</summary>
    /// <param name="examples">The detached examples stack owned by this page.</param>
    protected abstract void BuildExamples(ControlStack examples);

    private static void ValidateMetadata(
        InteractionDescription[] interactions,
        PropertyDescription[] properties)
    {
        if (interactions.Length == 0 || interactions.Any(static interaction =>
                string.IsNullOrWhiteSpace(interaction.Input) ||
                string.IsNullOrWhiteSpace(interaction.Behavior) ||
                string.IsNullOrWhiteSpace(interaction.Result)))
        {
            throw new ArgumentException("Every showcase page requires at least one complete interaction description.");
        }

        if (properties.Length == 0)
        {
            throw new ArgumentException("A showcase page requires property documentation.");
        }

        if (properties.Any(static property =>
            string.IsNullOrWhiteSpace(property.Name) ||
            string.IsNullOrWhiteSpace(property.Type) ||
            string.IsNullOrWhiteSpace(property.Default) ||
            string.IsNullOrWhiteSpace(property.Description)))
        {
            throw new ArgumentException("Every property description must be initialized.");
        }
    }

    private static ControlBorder Heading(string title, string summary)
    {
        var text = new ControlRichText
        {
            Padding = new Thickness(1, 0),
            Style = Palette.HeaderText(),
            Wrapping = Wrapping.Word,
        };
        text.Inlines.Add(new ControlRun(title)
        {
            Foreground = Palette.Accent,
            Attributes = TerminalAttributes.Bold,
        });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun("Overview")
        {
            Foreground = Palette.Success,
            Attributes = TerminalAttributes.Bold,
        });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun(summary));
        return new ControlBorder
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderColor = Palette.Accent,
            Background = Palette.Canvas,
            Child = text,
        };
    }

    private static ControlRichText Section(string title)
    {
        var text = new ControlRichText { Wrapping = Wrapping.Word };
        text.Inlines.Add(new ControlRun(title)
        {
            Foreground = Palette.Warning,
            Attributes = TerminalAttributes.Bold,
        });
        return text;
    }

    private static ControlBorder Card(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new ControlBorder
        {
            Padding = new Thickness(1),
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            BorderColor = Palette.Border,
            Background = Palette.Surface,
            Child = content,
        };
    }

    private static ControlRichText Narrative(string summary, InteractionDescription[] interactions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(interactions);

        var text = new ControlRichText
        {
            Padding = new Thickness(1, 0),
            Style = Palette.HeaderText(),
            Wrapping = Wrapping.Word,
        };

        text.Inlines.Add(new ControlRun("Use this control when ")
        {
            Foreground = Palette.Success,
            Attributes = TerminalAttributes.Bold,
        });
        text.Inlines.Add(new ControlRun(summary));
        text.Inlines.Add(new ControlRun(" Explore it in the live example below: ")
        {
            Foreground = Palette.Accent,
            Attributes = TerminalAttributes.Bold,
        });

        for (var index = 0; index < interactions.Length; index++)
        {
            var interaction = interactions[index];
            if (index > 0)
            {
                text.Inlines.Add(new ControlRun(" "));
            }

            text.Inlines.Add(new ControlRun($"{interaction.Input} — ")
            {
                Foreground = Palette.Warning,
                Attributes = TerminalAttributes.Bold,
            });
            text.Inlines.Add(new ControlRun($"{interaction.Behavior}; {interaction.Result}"));
        }

        text.Inlines.Add(new ControlRun(
            " Resize the terminal as you explore: this narrative, the live specimens, and the tables below reflow together so the control's behavior stays readable at every width.")
        {
            Foreground = Palette.Muted,
        });
        return text;
    }

    private ControlTable TechnicalDetails()
    {
        var table = new ControlTable
        {
            ShowGridLines = true,
            GridLineColor = Palette.Border,
            HeaderForeground = Palette.Text,
            HeaderBackground = Palette.Highlight,
            CellPadding = new Thickness(1, 0),
        };
        table.Columns.Add(TableColumn.Fixed("Property", 18));
        table.Columns.Add(TableColumn.Fixed("Type", 16));
        table.Columns.Add(TableColumn.Fixed("Default", 18));
        table.Columns.Add(TableColumn.Fill("Meaning"));

        foreach (var property in _properties)
        {
            table.Rows.Add(new TableRow([
                new ControlText(property.Name) { Foreground = Palette.Accent, Attributes = TerminalAttributes.Bold },
                new ControlText(property.Type) { Foreground = Palette.Muted },
                new ControlText(property.Default) { Foreground = Palette.Muted },
                new ControlText(property.Description) { Wrapping = Wrapping.Word },
            ]));
        }

        return table;
    }

    private ControlTable InteractionTable()
    {
        var table = new ControlTable
        {
            ShowGridLines = true,
            GridLineColor = Palette.Border,
            HeaderForeground = Palette.Text,
            HeaderBackground = Palette.Highlight,
            CellPadding = new Thickness(1, 0),
        };
        table.Columns.Add(TableColumn.Fixed("Input", 18));
        table.Columns.Add(TableColumn.Fixed("Behavior", 28));
        table.Columns.Add(TableColumn.Fill("Result"));

        foreach (var interaction in _interactions)
        {
            table.Rows.Add(new TableRow([
                new ControlText(interaction.Input) { Foreground = Palette.Accent, Attributes = TerminalAttributes.Bold },
                new ControlText(interaction.Behavior) { Foreground = Palette.Warning, Wrapping = Wrapping.Word },
                new ControlText(interaction.Result) { Wrapping = Wrapping.Word },
            ]));
        }

        return table;
    }
}
