// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines a focusable clickable text control styled as a classic hyperlink.</summary>
[PublicAPI]
public sealed class HyperlinkButton: InputBase, IStyled<HyperlinkButtonStyle>
{
    private readonly StyleSlot<HyperlinkButtonStyle> _style;

    /// <summary>Initializes an empty focusable HyperlinkButton with accent-colored underlined text.</summary>
    public HyperlinkButton()
    {
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
        _style = InitializeStyle(HyperlinkButtonStyle.Definition);
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached HyperlinkButton is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The HyperlinkButton is disposed.</exception>
    public HyperlinkButtonStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public HyperlinkButtonStyle ActualStyle => _style.Actual;

    /// <summary>Initializes a focusable HyperlinkButton with the specified text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public HyperlinkButton(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Raised after released state commits and before command execution.</summary>
    public event EventHandler<ActivationEventArgs>? Click;

    /// <summary>Activates an available executable HyperlinkButton through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void PerformClick() => _ = TryActivate(ActivationCause.Programmatic);

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        var command = Command;
        var parameter = CommandParameter;

        if (command is not null && !command.CanExecute(parameter))
        {
            return;
        }

        var eventArgs = new ActivationEventArgs(cause);
        Click?.Invoke(this, eventArgs);
        command?.Execute(parameter);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Click = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        var affixInset = affixes.StartCells + affixes.EndCells;
        var desired = MeasureCaption(new Constraint(
            DeflateConstraint(constraint.Width, affixInset),
            constraint.Height));

        return new Size(desired.Width.Add(affixInset), desired.Height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        ArrangeCaption(DeflateForAffixes(bounds, affixes));
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        base.OnRenderContent(canvas);

        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        RenderAffixes(canvas, ContentBounds, affixes, StartAffix, EndAffix, ResolvedStyle);
    }

    [Pure]
    private static int? DeflateConstraint(int? value, int inset) =>
        value.HasValue ? Math.Max(0, value.Value - inset) : null;
}
