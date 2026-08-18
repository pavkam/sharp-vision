// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using SharpVision.Controls.Display;
using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
using SharpVision.Terminal.Input;

using Text;

using DisplayText = Controls.Display.Text;
using LayoutStack = Controls.Layout.Stack;

/// <summary>Provides a measured, centered modal message surface with standard action buttons.</summary>
/// <remarks>
/// The static <c>ShowAsync</c> helpers attach a temporary instance to the owning Screen's private
/// presentation plane, or to an explicit or fallback container outside a hosted Screen. They enter
/// the shared Window modality plane and remove the instance after a result is selected. Message text
/// wraps against the application host's available width; no fixed message-box width is assumed.
/// </remarks>
[PublicAPI]
public sealed class MessageBox: Dialog<MessageBoxResult>, IStyled<MessageBoxStyle>
{
    private const int _minimumWidth = 32;
    private const int _minimumHeight = 8;

    private readonly Button[] _buttons;
    private readonly Button? _okButton;
    private readonly Button? _cancelButton;
    private readonly Button? _yesButton;
    private readonly Button? _noButton;
    private readonly StyleSlot<MessageBoxStyle> _style;
    private readonly StyleSlot<ButtonStyle> _buttonStyle;
    private readonly StyleSlot<SeparatorStyle> _separatorStyle;
    private readonly DisplayText _messageText;
    private readonly Grid _actionBar;

    #region Construction and contract

    /// <summary>Initializes a message box with the requested title, message, and button layout.</summary>
    /// <param name="message">The non-null message text.</param>
    /// <param name="title">The non-null window title.</param>
    /// <param name="buttons">The defined standard button layout.</param>
    /// <exception cref="ArgumentNullException">A message or title is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is undefined.</exception>
    public MessageBox(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.Ok)
        : base(MessageBoxResult.Cancel)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);

        ArgumentOutOfRangeException.ThrowIfNotDefined(buttons, nameof(buttons), "The message-box buttons are unknown.");

        _style = InitializeStyle(MessageBoxStyle.Definition);
        Message = message;
        Title = title;
        Buttons = buttons;
        (_buttons, _okButton, _cancelButton, _yesButton, _noButton) = CreateButtons(buttons);
        Header = title;
        CanMove = true;
        CanClose = false;
        MinWidth = _minimumWidth;
        MinHeight = _minimumHeight;
        MaxHeight = 20;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Content = CreateContent(message, _buttons, out _messageText, out _actionBar, out var separator);
        _buttonStyle = InitializePartStyle(
            ButtonStyle.ForwardingDefinition,
            nameof(ButtonStyle));
        foreach (var button in _buttons)
        {
            BindStyle(_buttonStyle, button);
        }
        _separatorStyle = InitializePartStyle(
            SeparatorStyle.ForwardingDefinition,
            nameof(SeparatorStyle));
        BindStyle(_separatorStyle, separator);
        _ = AddHandler(Events.Key, OnMessageBoxKey);
    }

    /// <summary>Gets the non-null message rendered by this box.</summary>
    public string Message { get; }

    /// <summary>Gets the non-null title rendered in the window frame.</summary>
    public string Title { get; }

    /// <summary>Gets the standard button layout rendered by this box.</summary>
    public MessageBoxButtons Buttons { get; }

    /// <summary>Gets or sets the complete local aggregate presentation, or null to let the active
    /// Theme's own "messageBox" section (or its Window fallback) own the frame, message face, and
    /// content geometry.</summary>
    /// <exception cref="InvalidOperationException">The attached MessageBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The MessageBox is disposed.</exception>
    public MessageBoxStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved complete aggregate presentation.</summary>
    public MessageBoxStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets the complete local presentation applied to every generated action
    /// Button, or null to let each Button use its own semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached MessageBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The MessageBox is disposed.</exception>
    public ButtonStyle? ButtonStyle
    {
        get => _buttonStyle.Local;
        set => _buttonStyle.Local = value;
    }

    /// <summary>Gets the resolved Button style applied to every generated action.</summary>
    public ButtonStyle ActualButtonStyle => _buttonStyle.Actual;

    /// <summary>Gets or sets the complete local presentation applied to the divider above the
    /// action row, or null to let the divider use its own Theme-resolved "separator" presentation.</summary>
    /// <exception cref="InvalidOperationException">The attached MessageBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The MessageBox is disposed.</exception>
    public SeparatorStyle? SeparatorStyle
    {
        get => _separatorStyle.Local;
        set => _separatorStyle.Local = value;
    }

    /// <summary>Gets the resolved Separator style applied to the divider above the action row.</summary>
    public SeparatorStyle ActualSeparatorStyle => _separatorStyle.Actual;

    /// <summary>Gets or sets the non-null OK action caption.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached MessageBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The MessageBox is disposed.</exception>
    public string OkText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref field, value, InvalidationImpact.Measure) && _okButton is { } button)
            {
                button.Text = value;
                ApplyEqualWidth(_buttons);
            }
        }
    } = "&OK";

    /// <summary>Gets or sets the non-null Cancel action caption.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached MessageBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The MessageBox is disposed.</exception>
    public string CancelText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref field, value, InvalidationImpact.Measure) && _cancelButton is { } button)
            {
                button.Text = value;
                ApplyEqualWidth(_buttons);
            }
        }
    } = "&Cancel";

    /// <summary>Gets or sets the non-null Yes action caption.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached MessageBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The MessageBox is disposed.</exception>
    public string YesText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref field, value, InvalidationImpact.Measure) && _yesButton is { } button)
            {
                button.Text = value;
                ApplyEqualWidth(_buttons);
            }
        }
    } = "&Yes";

    /// <summary>Gets or sets the non-null No action caption.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached MessageBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The MessageBox is disposed.</exception>
    public string NoText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref field, value, InvalidationImpact.Measure) && _noButton is { } button)
            {
                button.Text = value;
                ApplyEqualWidth(_buttons);
            }
        }
    } = "&No";

    #endregion

    /// <summary>Caps content measurement at 80% of the available presentation width instead of a
    /// fixed cell count, so the box stays content-driven for short messages and grows toward that
    /// cap - never past it - for longer ones. Recomputed every layout pass, so a live presentation
    /// resize retargets the cap on its own without any explicit resize subscription.
    /// Also re-applies the resolved message face and content geometry every pass, so a live Theme
    /// swap updates them coherently even without a local <see cref="Style"/> override.
    /// </summary>
    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var style = ActualStyle;
        _messageText.Face = style.MessageFace;
        _messageText.Margin = style.MessageMargin;
        _actionBar.Margin = style.ActionBarMargin;

        if (constraint.Width is { } available)
        {
            var cap = Math.Max(MinWidth, (int) Math.Round(available * 0.8, MidpointRounding.AwayFromZero));
            constraint = new Constraint(Math.Min(available, cap), constraint.Height);
        }

        return base.MeasureOverride(constraint);
    }

    #region Static presentation helpers

    /// <summary>Shows an OK message box owned by the supplied attached control.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <returns>A task completing with <see cref="MessageBoxResult.Ok"/> or dismissal.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(ControlBase owner, string message) =>
        ShowAsync(owner, message, "Message", MessageBoxButtons.Ok);

    /// <summary>Shows an OK message box with a custom title.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <param name="title">The non-null window title.</param>
    /// <returns>A task completing with the selected result.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(ControlBase owner, string message, string title) =>
        ShowAsync(owner, message, title, MessageBoxButtons.Ok);

    /// <summary>Shows a message box with a custom button layout and the default title.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <param name="buttons">The defined standard button layout.</param>
    /// <returns>A task completing with the selected result.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(ControlBase owner, string message, MessageBoxButtons buttons) =>
        ShowAsync(owner, message, "Message", buttons);

    /// <summary>Shows a titled message box with the requested standard button layout.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <param name="title">The non-null window title.</param>
    /// <param name="buttons">The defined standard button layout.</param>
    /// <param name="buttonStyle">The complete local Button presentation, or null to use each
    /// generated Button's own semantic input profile.</param>
    /// <returns>A task completing when a button or dismissal selects a result.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(
        ControlBase owner,
        string message,
        string title,
        MessageBoxButtons buttons,
        ButtonStyle? buttonStyle = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(message);
        var messageBox = new MessageBox(message, title, buttons) { ButtonStyle = buttonStyle };
        return ShowCoreAsync(owner, messageBox);
    }

    /// <summary>Shows a message box configured through a reusable options carrier, without exposing
    /// the generated child Buttons or divider.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <param name="options">The non-null layout, caption, and style configuration.</param>
    /// <returns>A task completing when a button or dismissal selects a result.</returns>
    /// <exception cref="ArgumentNullException">An argument, or a required member of
    /// <paramref name="options"/>, is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="MessageBoxOptions.Buttons"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(ControlBase owner, string message, MessageBoxOptions options)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Title);
        ArgumentNullException.ThrowIfNull(options.OkText);
        ArgumentNullException.ThrowIfNull(options.CancelText);
        ArgumentNullException.ThrowIfNull(options.YesText);
        ArgumentNullException.ThrowIfNull(options.NoText);

        var messageBox = new MessageBox(message, options.Title, options.Buttons)
        {
            Style = options.Style,
            ButtonStyle = options.ButtonStyle,
            OkText = options.OkText,
            CancelText = options.CancelText,
            YesText = options.YesText,
            NoText = options.NoText
        };
        return ShowCoreAsync(owner, messageBox);
    }

    private static Task<MessageBoxResult> ShowCoreAsync(ControlBase owner, MessageBox messageBox)
    {
        ArgumentNullException.ThrowIfNull(owner);
        owner.Dispatcher?.VerifyAccess();

        var host = PresentationHost.Resolve(owner) ??
            throw new ArgumentException("The owner must be attached beneath a presentation host.", nameof(owner));
        host.Add(messageBox);

        try
        {
            return messageBox.PresentAsync(host, messageBox._buttons[0], CancellationToken.None);
        }
        catch
        {
            _ = host.Remove(messageBox);
            messageBox.Dispose();
            throw;
        }
    }

    #endregion

    private Grid CreateContent(string message, Button[] buttons, out DisplayText messageText, out Grid actionBar, out Separator separator)
    {
        var style = ActualStyle;
        messageText = new DisplayText(message)
        {
            Overflow = Overflow.Wrap,
            TextAlignment = Alignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Face = style.MessageFace,
            Margin = style.MessageMargin
        };
        // Text.MeasureOverride always reports the full wrapped line count and ignores the incoming
        // height constraint, so a message long enough to exceed the row below would otherwise just
        // get silently clipped once the outer MaxHeight forces this Grid to shrink. Hosting it in a
        // vertically auto-scrolling Stack instead means that overflow becomes reachable by scrolling
        // rather than lost, mirroring how FileDialogBase gives its own growable list region scrolling
        // instead of letting it compete for space through an outer Grid-shrink.
        var messageHost = new LayoutStack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Children = { messageText }
        };
        var actionStack = new LayoutStack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        foreach (var button in buttons)
        {
            actionStack.Children.Add(button);
        }
        var actions = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        actions.Columns.Add(Track.Star(1));
        actions.Columns.Add(Track.Auto());
        actions.Columns.Add(Track.Star(1));
        Grid.SetColumn(actionStack, 1);
        actions.Children.Add(actionStack);
        actionBar = CreateActionBar(actions, buttons, out separator);
        actionBar.Margin = style.ActionBarMargin;
        var content = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowSpacing = 1
        };
        // A generous but bounded ceiling, not just a floor. Tracks.Shrink drains Auto rows from
        // the last index backward, so without any cap here a message long enough to want more
        // than the interior budget would first crush the action bar (the last row) to nothing
        // before this row ever gave up a single cell - the actual buttons could end up with zero
        // height, or (for a bordered Button style) a nonzero height too small to fit any content.
        // A per-button-style floor on the action-bar row cannot fix this cleanly: the buttons'
        // resolved style is not settled by the time this Grid is built (FilePickerDialog and
        // MessageBox itself both apply style overrides through a StyleSlot bound AFTER content
        // construction), so no floor picked here can reliably match the real button shape.
        // Capping the message row's own maximum instead keeps the fix entirely on the side that
        // is actually unbounded and actually safe to bound - the message now scrolls via
        // messageHost's AutoScroll instead of losing rows - without needing any assumption about
        // button styling at all: 10 rows leaves comfortable headroom under MaxHeight(20) for
        // border, RowSpacing, and even the most generous realistic action bar (separator, a
        // bordered button, and its shadow).
        content.Rows.Add(Track.Auto(minimum: 3, maximum: 10));
        content.Rows.Add(Track.Auto());
        Grid.SetRow(messageHost, 0);
        Grid.SetRow(actionBar, 1);
        content.Children.Add(messageHost);
        content.Children.Add(actionBar);
        return content;
    }

    private (Button[] All, Button? Ok, Button? Cancel, Button? Yes, Button? No) CreateButtons(MessageBoxButtons buttons)
    {
        Button? ok = null;
        Button? cancel = null;
        Button? yes = null;
        Button? no = null;
        Button[] result;

        switch (buttons)
        {
            case MessageBoxButtons.Ok:
                ok = CreateButton(OkText, MessageBoxResult.Ok, isDefault: true);
                result = [ok];
                break;
            case MessageBoxButtons.OkCancel:
                ok = CreateButton(OkText, MessageBoxResult.Ok, isDefault: true);
                cancel = CreateButton(CancelText, MessageBoxResult.Cancel, isCancel: true);
                result = [ok, cancel];
                break;
            case MessageBoxButtons.YesNo:
                yes = CreateButton(YesText, MessageBoxResult.Yes, isDefault: true);
                no = CreateButton(NoText, MessageBoxResult.No);
                result = [yes, no];
                break;
            case MessageBoxButtons.YesNoCancel:
                yes = CreateButton(YesText, MessageBoxResult.Yes, isDefault: true);
                no = CreateButton(NoText, MessageBoxResult.No);
                cancel = CreateButton(CancelText, MessageBoxResult.Cancel, isCancel: true);
                result = [yes, no, cancel];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(buttons), buttons, "The message-box buttons are unknown.");
        }

        ApplyEqualWidth(result);
        return (result, ok, cancel, yes, no);
    }

    private void ApplyEqualWidth(Button[] buttons)
    {
        var width = buttons.Max(button => button.Text.Measure(CellPolicy.AmbiguousWidth, button.UseMnemonic) + 4);
        foreach (var button in buttons)
        {
            button.Width = Length.Cells(width);
        }
    }

    private Button CreateButton(string label, MessageBoxResult result, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button
        {
            Text = label,
            IsDefault = isDefault,
            IsCancel = isCancel
        };
        button.Click += (_, _) => Complete(result);
        return button;
    }

    private void OnMessageBoxKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase == RoutingPhase.Bubble &&
            eventArgs.Stroke.Action == KeyAction.Press &&
            eventArgs.Stroke.Code == Code.Escape)
        {
            eventArgs.IsHandled = Cancel();
        }
    }

    private protected override WindowStyle ResolveCloseChromeStyle(Theme? theme) =>
        MessageBoxStyle.Definition.Resolve(_style.LocalValue, theme);

}
