// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Layout;

using Terminal.Capabilities;

using DisplayText = Display.Text;
using LayoutStack = Layout.Stack;
/// <summary>Selects one terminal color through capability-adaptive retained graphical and text controls.</summary>
[PublicAPI]
public sealed class ColorPicker: CompositeControlBase, IStyled<ColorPickerStyle>
{
    private readonly LayoutStack _monochromeRoot;
    private readonly ControlBase _rgbRoot;
    private bool _isValueTextValid = true;
    private bool _synchronizing;
    private Color _value = Color.Rgb(255, 0, 0);
    private readonly CallbackTransitionStream _valueTransitions = new();
    private readonly StyleSlot<ColorPickerStyle> _style;

    #region Construction and public state

    /// <summary>Initializes a retained adaptive picker with an RGB-red detached value.</summary>
    public ColorPicker()
    {
        _style = InitializeStyle(ColorPickerStyle.Definition, OnStyleChanged);
        TabNavigation = TabNavigation.Continue;
        Plane = new ColorPlane();
        HueSlider = CreateSlider(359);
        RedSlider = CreateSlider(byte.MaxValue);
        GreenSlider = CreateSlider(byte.MaxValue);
        BlueSlider = CreateSlider(byte.MaxValue);
        Preview = new ColorSwatch { Width = Length.Cells(19) };
        ValueTextInput = new TextInput
        {
            MaxLength = 18,
            ScrollBars = ScrollBars.None,
            Width = Length.Cells(19),
            Height = Length.Cells(1)
        };
        _rgbRoot = CreateRgbRoot();
        _monochromeRoot = CreateMonochromeRoot();
        var root = new Overlay { Children = { _rgbRoot, _monochromeRoot } };
        InitializeContent(root);
        Subscribe();
        SynchronizeParts();
        ApplyDepth(EffectiveColorDepth);
    }

    /// <summary>Raised after a changed color commits.</summary>
    public event EventHandler<ColorChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the selected terminal color.</summary>
    /// <remarks>
    /// The picker preserves RGB authoring independently of the active terminal output depth.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is transparent.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color Value
    {
        get => _value;
        set => _ = Commit(value);
    }

    /// <summary>Gets the active terminal color depth inherited from the application.</summary>
    public ColorDepth EffectiveColorDepth => Capabilities.ColorDepth;

    /// <summary>Gets or sets the complete local style, or null to use the definition fallback.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ColorPickerStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved presentation currently applied to the owned Sliders, value
    /// editor, and plane selection marker, including the editor's invalid-text error face.</summary>
    public ColorPickerStyle ActualStyle
    {
        get
        {
            var aggregate = Style;
            var slider = SliderStyle.Definition.Resolve(aggregate?.SliderStyle, Theme);
            var hueSlider = SliderStyle.Definition.Resolve(
                NormalizeHueSliderStyle(aggregate?.HueSliderStyle ?? aggregate?.SliderStyle),
                Theme);

            return new ColorPickerStyle(
                ControlStyle.DefaultFace,
                ControlStyle.NoBorder,
                ControlStyle.NoShadow,
                slider,
                ResolveValueTextFace(),
                aggregate?.SelectedMarker,
                hueSlider);
        }
    }

    /// <summary>Gets the uppercase canonical RGB text, invalid raw editor text, or DEFAULT for the
    /// terminal default color.</summary>
    internal string HexText => ValueTextInput.Text;

    /// <summary>Gets the retained value editor used to prove text, focus, style, and synchronization
    /// invariants without exposing a presentation part as public API.</summary>
    internal TextInput ValueTextInput { get; }

    /// <summary>Gets the retained true-color saturation/value surface for interaction tests.</summary>
    internal ColorPlane Plane { get; }

    /// <summary>Gets the retained preview surface.</summary>
    internal ColorSwatch Preview { get; }

    /// <summary>Gets the retained hue slider for interaction tests.</summary>
    internal Slider HueSlider { get; }

    /// <summary>Gets the retained red-component slider.</summary>
    internal Slider RedSlider { get; }

    /// <summary>Gets the retained green-component slider.</summary>
    internal Slider GreenSlider { get; }

    /// <summary>Gets the retained blue-component slider.</summary>
    internal Slider BlueSlider { get; }

    /// <summary>Gets whether the RGB editor participates in layout.</summary>
    internal bool IsRgbEditorVisible => _rgbRoot.Visibility == Visibility.Visible;

    #endregion

    #region Lifecycle and capability adaptation

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        ApplyDepth(EffectiveColorDepth);
    }

    /// <inheritdoc/>
    protected override void OnCapabilitiesChanged(
        TerminalCapabilities previous,
        TerminalCapabilities current)
    {
        base.OnCapabilitiesChanged(previous, current);
        ApplyDepth(current.ColorDepth);
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        Plane.Changed -= OnPlaneChanged;
        HueSlider.ValueChanged -= OnHueChanged;
        RedSlider.ValueChanged -= OnRgbChanged;
        GreenSlider.ValueChanged -= OnRgbChanged;
        BlueSlider.ValueChanged -= OnRgbChanged;
        ValueTextInput.TextChanged -= OnValueTextChanged;
        ValueChanged = null;
        base.OnDisposing();
    }

    private void ApplyDepth(ColorDepth depth)
    {
        Debug.Assert(Enum.IsDefined(depth), "Capability profiles validate color depth.");
        _rgbRoot.Visibility = depth == ColorDepth.Monochrome
            ? Visibility.Collapsed
            : Visibility.Visible;
        _monochromeRoot.Visibility = depth == ColorDepth.Monochrome
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    #endregion

    #region Composition

    private Grid CreateRgbRoot()
    {
        var root = new Grid
        {
            RowSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Rows.Add(Track.Star(1, minimum: Length.Cells(4)));
        root.Rows.Add(Track.Cells(1));
        root.Rows.Add(Track.Auto());
        root.Rows.Add(Track.Cells(1));

        Grid.SetRow(Plane, 0);
        root.Children.Add(Plane);

        var hueOverlay = new Overlay { Height = Length.Cells(1), Children = { new ColorRamp(), HueSlider } };
        Grid.SetRow(hueOverlay, 1);
        root.Children.Add(hueOverlay);

        var components = new LayoutStack
        {
            Spacing = 0,
            Children =
            {
                CreateSliderRow("R", RedSlider),
                CreateSliderRow("G", GreenSlider),
                CreateSliderRow("B", BlueSlider)
            }
        };
        Grid.SetRow(components, 2);
        root.Children.Add(components);

        var valueRow = new Overlay
        {
            Width = Length.Cells(19),
            Height = Length.Cells(1),
            Children = { Preview, ValueTextInput }
        };
        Grid.SetRow(valueRow, 3);
        root.Children.Add(valueRow);
        return root;
    }

    private static LayoutStack CreateMonochromeRoot()
    {
        var swatch = new ColorSwatch { Value = Color.Default };
        return new LayoutStack
        {
            IsEnabled = false,
            Spacing = 1,
            Children = { swatch, new DisplayText("Monochrome terminal · default color only") }
        };
    }

    private static Dock CreateSliderRow(string label, Slider slider)
    {
        var text = new DisplayText(label) { Width = Length.Cells(2) };
        Dock.SetSide(text, DockSide.Left);
        return new Dock { Height = Length.Cells(1), Children = { text, slider } };
    }

    private static Slider CreateSlider(int maximum) => new()
    {
        Maximum = maximum,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private void Subscribe()
    {
        Plane.Changed += OnPlaneChanged;
        HueSlider.ValueChanged += OnHueChanged;
        RedSlider.ValueChanged += OnRgbChanged;
        GreenSlider.ValueChanged += OnRgbChanged;
        BlueSlider.ValueChanged += OnRgbChanged;
        ValueTextInput.TextChanged += OnValueTextChanged;
    }

    private void OnStyleChanged(ColorPickerStyle previous, ColorPickerStyle current)
    {
        _ = previous;
        _ = current;
        var style = Style;
        var sliderStyle = style?.SliderStyle;
        HueSlider.Style = NormalizeHueSliderStyle(style?.HueSliderStyle ?? sliderStyle);
        RedSlider.Style = sliderStyle;
        GreenSlider.Style = sliderStyle;
        BlueSlider.Style = sliderStyle;
        ApplyValueTextStyle();
        Plane.SelectedMarker = style?.SelectedMarker;
    }

    // HueSlider sits directly on top of ColorRamp's per-column rainbow (the two overlap fully in
    // the Overlay built by CreateRgbRoot) and depends on its own background staying transparent so
    // that gradient shows through everywhere but the thumb. RedSlider/GreenSlider/BlueSlider have
    // no such backdrop, so only HueSlider's forwarded Face is pinned transparent here - an opaque
    // ColorPickerStyle.SliderStyle.Face.Background must not reach HueSlider's resolved appearance,
    // or Slider.OnRenderContent clears the gradient out from under it before drawing.
    [Pure]
    private static SliderStyle? NormalizeHueSliderStyle(SliderStyle? sliderStyle) => sliderStyle is null
        ? null
        : sliderStyle with { Face = sliderStyle.Face with { Background = Color.Transparent } };

    #endregion

    #region Synchronization

    private bool Commit(Color requested)
    {
        if (requested.IsTransparent)
        {
            throw new ArgumentException("ColorPicker requires a concrete terminal color.", nameof(requested));
        }

        VerifyMutable();
        var previous = _value;

        if (!SetTransitionProperty(
                ref _value,
                requested,
                InvalidationImpact.Render,
                _valueTransitions,
                out var transition,
                nameof(Value)))
        {
            SynchronizeParts();
            return false;
        }

        transition.CaptureIfCurrent(SynchronizeParts);
        transition.CaptureIfCurrent(
            () => NotifyPropertyChanged(nameof(ActualStyle), InvalidationImpact.None));
        transition.PublishCurrent(
            ValueChanged,
            this,
            new ColorChangedEventArgs(previous, requested));
        transition.ThrowIfFailed();

        return true;
    }

    private void SynchronizeParts()
    {
        var rgb = _value.IsRgb ? _value : Color.Rgb(0, 0, 0);

        rgb.ToHsv(out var hue, out var saturation, out var value);
        _synchronizing = true;

        try
        {
            Plane.SetSelection(hue, saturation, value);
            HueSlider.Value = hue;
            RedSlider.Value = rgb.Red;
            GreenSlider.Value = rgb.Green;
            BlueSlider.Value = rgb.Blue;
            Preview.Value = _value;
            SetValueTextValidity(true);
            ValueTextInput.Text = _value.IsDefault
                ? "DEFAULT"
                : $"#{rgb.Red:X2}{rgb.Green:X2}{rgb.Blue:X2}";
            ApplyValueTextStyle();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void OnPlaneChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_synchronizing)
        {
            _ = Commit(Color.FromHsv(Plane.Hue, Plane.Saturation, Plane.Value));
        }
    }

    private void OnHueChanged(object? sender, SliderValueChangedEventArgs eventArgs)
    {
        _ = sender;

        if (!_synchronizing)
        {
            Plane.SetSelection(eventArgs.Value, Plane.Saturation, Plane.Value);
            _ = Commit(Color.FromHsv(eventArgs.Value, Plane.Saturation, Plane.Value));
        }
    }

    private void OnRgbChanged(object? sender, SliderValueChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_synchronizing)
        {
            _ = Commit(Color.Rgb(RedSlider.Value, GreenSlider.Value, BlueSlider.Value));
        }
    }

    private void OnValueTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (_synchronizing)
        {
            return;
        }

        if (TryParseValueText(ValueTextInput.Text, out var color))
        {
            SetValueTextValidity(true);
            _ = Commit(color);
            return;
        }

        SetValueTextValidity(false);
    }

    private void SetValueTextValidity(bool isValid)
    {
        if (_isValueTextValid == isValid)
        {
            return;
        }

        _isValueTextValid = isValid;
        ApplyValueTextStyle();
        NotifyPropertyChanged(nameof(ActualStyle), InvalidationImpact.None);
    }

    private void ApplyValueTextStyle() =>
        ValueTextInput.Style = new TextInputStyle(
            ResolveValueTextFace(),
            ControlStyle.NoBorder,
            ControlStyle.NoShadow);

    private Face ResolveValueTextFace()
    {
        var authored = Style?.StatusFace ?? ColorPickerStyle.DefaultStatusFace;

        if (_isValueTextValid)
        {
            return _value.IsRgb
                ? authored with
                {
                    Foreground = _value.Contrast(),
                    Background = _value
                }
                : authored;
        }

        var error = (Theme ?? ThemeCatalog.Dark).Error;
        return authored with
        {
            Foreground = error.Contrast(),
            Background = SemanticColor.Error
        };
    }

    [Pure]
    private static bool TryParseValueText(string text, out Color color)
    {
        color = Color.Default;

        if (string.Equals(text, "DEFAULT", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var hex = text.AsSpan();

        if (hex.Length is 6 or 7 && (hex.Length == 6 || hex[0] == '#') &&
            Color.TryFromHex(text, out color))
        {
            return true;
        }

        var components = text.AsSpan();

        if (components.Length >= 5 &&
            components[..4].Equals("rgb(".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
            components[^1] == ')')
        {
            components = components[4..^1];
        }

        var index = 0;

        if (!TryParseComponent(components, ref index, out var red) ||
            !TryConsumeComma(components, ref index) ||
            !TryParseComponent(components, ref index, out var green) ||
            !TryConsumeComma(components, ref index) ||
            !TryParseComponent(components, ref index, out var blue) ||
            index != components.Length)
        {
            color = Color.Default;
            return false;
        }

        color = Color.Rgb(red, green, blue);
        return true;
    }

    [Pure]
    private static bool TryParseComponent(ReadOnlySpan<char> text, ref int index, out int component)
    {
        component = 0;
        var start = index;

        while (index < text.Length && text[index] is >= '0' and <= '9')
        {
            component = (component * 10) + (text[index] - '0');

            if (component > byte.MaxValue)
            {
                return false;
            }

            index++;
        }

        return index > start;
    }

    [Pure]
    private static bool TryConsumeComma(ReadOnlySpan<char> text, ref int index)
    {
        if (index >= text.Length || text[index++] != ',')
        {
            return false;
        }

        while (index < text.Length && text[index] == ' ')
        {
            index++;
        }

        return true;
    }

    #endregion
}
