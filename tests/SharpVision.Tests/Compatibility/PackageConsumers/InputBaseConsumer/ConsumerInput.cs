// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.InputBaseConsumer;

/// <summary>Exercises the protected InputBase capability seams exposed by the packed SharpVision
/// package: press activation, a single owned text caption, an optional command, an owned popup,
/// the step-key translation, the shared drop-down glyph, and the off-dispatcher/disposed guard -
/// all called from outside the SharpVision assembly, where only a public or protected member
/// (never an internal or private protected one) can resolve.</summary>
public sealed class ConsumerInput: InputBase
{
    private readonly ListView _list;

    /// <summary>Initializes a consumer input enabling press activation, a caption, a command, and
    /// an owned popup.</summary>
    public ConsumerInput()
    {
        _list = new ListView { IsTabStop = false };
        Popup = EnablePopup(_list, focusOnOpen: false);
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
    }

    /// <summary>Gets the owned popup constructed by the packed EnablePopup seam.</summary>
    public Popup Popup { get; }

    /// <summary>Gets the number of completed activations.</summary>
    public int Activations { get; private set; }

    /// <summary>Gets the shared drop-down indicator cell width through the protected seam.</summary>
    public static int IndicatorWidth => DropDownIndicatorWidth;

    /// <summary>Exercises TryGetStepDelta through the protected static seam.</summary>
    public bool ProbeTryGetStepDelta(KeyEventArgs eventArgs, out int delta) =>
        TryGetStepDelta(eventArgs, out delta);

    /// <summary>Exercises ResolveDropDownGlyph through the protected seam.</summary>
    public Rune ProbeResolveDropDownGlyph(Rune fallback) => ResolveDropDownGlyph(fallback);

    /// <summary>Exercises VerifyMutable through the protected seam.</summary>
    public void ProbeVerifyMutable() => VerifyMutable();

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        Activations++;
        IsOpen = !IsOpen;
        ExecuteCommandIfAny();
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }
}
