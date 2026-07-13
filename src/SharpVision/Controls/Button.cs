// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Windows.Input;

using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Threading;

using TerminalAttributes = TerminalAttributes;
using TerminalCanvas = TerminalCanvas;

/// <summary>Defines a focusable command control with one optional owned content child.</summary>
public sealed partial class Button: Pressable
{
    static Button()
    {
        _ = BorderThicknessProperty.RegisterClassDefault<Button>(new Thickness(1));
        _ = PaddingProperty.RegisterClassDefault<Button>(new Thickness(1));
        _ = HasShadowProperty.RegisterClassDefault<Button>(true);
        _ = ShadowOffsetProperty.RegisterClassDefault<Button>(new Point(1, 1));
        _ = BorderStyleProperty.RegisterClassDefault<Button>(Glyphs.Rounded);
        _ = ShadowAttributesProperty.RegisterClassDefault<Button>(TerminalAttributes.Dim);
    }

    private ICommand? _command;

    #region Construction and command properties

    /// <summary>Initializes an empty focusable Button with rounded border, internal padding, and compact shadow.</summary>
    public Button() : base(capacity: 1)
    {
    }

    /// <summary>Raised after released state commits and before command execution.</summary>
    public event EventHandler<ActivationEventArgs>? Click;

    /// <summary>Gets or atomically sets the optional owned content.</summary>
    /// <exception cref="ArgumentException">The value cannot be owned by this Button.</exception>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button or value is disposed.</exception>
    public Control? Content
    {
        get => Children.Count == 0 ? null : Children[0];
        set => Children.SetOnly(value);
    }

    /// <summary>Gets or sets the optional command invoked after Click.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public ICommand? Command
    {
        get => _command;
        set
        {
            VerifyMutable();

            if (EqualityComparer<ICommand?>.Default.Equals(_command, value))
            {
                return;
            }

            _command?.CanExecuteChanged -= OnCanExecuteChanged;
            _ = Set(ref _command, value, Invalidation.Render);
            _command?.CanExecuteChanged += OnCanExecuteChanged;
        }
    }

    /// <summary>Gets or sets the borrowed parameter passed to command queries and execution.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public object? CommandParameter
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets whether an owning Window treats Enter as a fallback activation.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public bool IsDefault
    {
        get;
        set => _ = Set(ref field, value, Invalidation.None);
    }

    /// <summary>Gets or sets whether an owning Window treats Escape as a fallback activation.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public bool IsCancel
    {
        get;
        set => _ = Set(ref field, value, Invalidation.None);
    }

    /// <summary>Gets or sets the validated physical glyph family used by the button border.</summary>
    /// <exception cref="InvalidOperationException">The attached button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The button is disposed.</exception>
    public Glyphs Glyphs
    {
        get => BorderStyle;
        set => BorderStyle = value;
    }

    #endregion

    #region Activation and lifecycle


    /// <summary>Activates an available executable Button through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public void PerformClick()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        ICommand? command = Command;
        var parameter = CommandParameter;

        if (command is not null && !command.CanExecute(parameter))
        {
            return;
        }

        ActivationEventArgs eventArgs = new ActivationEventArgs(cause);
        Click?.Invoke(this, eventArgs);
        command?.Execute(parameter);
    }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        Control? content = Content;

        if (content is null)
        {
            return default;
        }

        content.Measure(constraint);
        return new Size(
            Add(content.DesiredSize.Width, content.Margin.Horizontal),
            Add(content.DesiredSize.Height, content.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds) =>
        Content?.Arrange(FaceContentBounds(bounds), widthResolved: true, heightResolved: true);

    /// <inheritdoc/>
    protected override Rect VisualBounds =>
        ControlChrome.ExpandVisualBounds(FaceBounds, HasShadow, ShadowOffset);

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        Rect face = FaceBounds;
        ControlChrome.Render(
            this,
            canvas,
            GetVisualState(),
            new ChromeRenderOptions
            {
                BodyBounds = face,
                ShadowExcludeBounds = face,
                ShadowAppearanceSource = NormalStyle,
                PreserveButtonShadowGap = true,
                ClearBodyWhenPressedWithShadow = true,
            });
    }

    /// <inheritdoc/>
    protected override void OnPressedChanged(bool pressed)
    {
        base.OnPressedChanged(pressed);

        if (!HasShadow || Content is not { } content)
        {
            return;
        }

        // Pointer and keyboard press state must be drawable before a later
        // layout drain, so keep owned content in the same translated face box.
        content.Arrange(FaceContentBounds(ContentBounds), widthResolved: true, heightResolved: true);
        Invalidate(Invalidation.Arrange);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed && _command is not null)
        {
            _command.CanExecuteChanged -= OnCanExecuteChanged;
            _command = null;
            Click = null;
        }
    }

    #endregion

    #region Layout and rendering

    private static int Add(int left, int right)
    {
        var value = (long) left + right;
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    private Rect FaceBounds => IsPressed && HasShadow ? ControlChrome.Shift(Bounds, ShadowOffset) : Bounds;

    private Rect FaceContentBounds(Rect bounds) =>
        IsPressed && HasShadow ? ControlChrome.Shift(bounds, ShadowOffset) : bounds;

    #endregion

    #region Command notification

    private void OnCanExecuteChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (IsDisposed)
        {
            return;
        }

        Dispatcher? dispatcher = Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() =>
            {
                if (!IsDisposed)
                {
                    Invalidate(Invalidation.Render);
                }
            });
            return;
        }

        Invalidate(Invalidation.Render);
    }

    #endregion
}
