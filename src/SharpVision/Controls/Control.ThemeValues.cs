// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;

/// <summary>Provides inherited immutable theme and direct appearance support.</summary>
public abstract partial class Control
{
    private readonly Dictionary<VisualState, Appearance> _appearanceStates = [];

    /// <summary>Gets the immutable theme inherited from the owning application.</summary>
    public Theme? Theme => InheritedTheme;

    /// <summary>Gets or sets whether this control stops ambient text appearance inheritance.</summary>
    public bool AppearanceBoundary { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets or sets a normal-state appearance overlay.</summary>
    public Appearance Appearance { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); }

    /// <summary>Gets state-specific appearance overlays.</summary>
    public IReadOnlyDictionary<VisualState, Appearance> AppearanceStates => _appearanceStates;

    /// <summary>Sets or removes a single state-specific appearance overlay.</summary>
    public void SetAppearance(VisualState state, Appearance? appearance)
    {
        if (state == VisualState.Normal || !Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "A single non-normal visual state is required.");
        }

        VerifyMutable();
        if (appearance is { } value)
        {
            _appearanceStates[state] = value;
        }
        else if (!_appearanceStates.Remove(state))
        {
            return;
        }

        Invalidate(Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceStates)));
    }

    internal Theme? InheritedTheme { get; private set; }

    internal bool CommitTheme(Theme? theme)
    {
        if (ReferenceEquals(InheritedTheme, theme))
        {
            return false;
        }

        InheritedTheme = theme;
        return true;
    }

    internal void SetTheme(Theme? theme)
    {
        if (CommitTheme(theme))
        {
            PublishThemeChanged();
        }
    }

    internal void PropagateTheme(Theme? theme)
    {
        OwnedControlRegistry.VerifyMutationAllowed(this);
        var entered = OwnedControlRegistry.EnterPublication(this);
        var themes = new List<Control>();
        var attached = new List<Control>();
        var detached = new List<Control>();
        var failure = (System.Runtime.ExceptionServices.ExceptionDispatchInfo?) null;
        try
        {
            CommitSubtreeContext(Dispatcher, CellPolicy, FocusOwner, CaptureOwner, theme, themes, attached, detached);
            PublishContextChanges(themes, attached, detached, ref failure);
        }
        finally { OwnedControlRegistry.ExitPublication(entered); }
        failure?.Throw();
    }

    internal Appearance CreateDefaultAppearance(VisualState state)
    {
        var defaults = ControlAppearanceDefaults.Get(this, state);
        return _appearanceStates.TryGetValue(state, out var appearance)
            ? defaults.Overlay(appearance)
            : defaults;
    }

    internal Appearance ApplyLocalAppearance(Appearance appearance)
    {
        var direct = new Appearance(Foreground, Background, Attributes, Underline, UnderlineColor, BorderColor, BorderAttributes, ShadowForeground, ShadowBackground, ShadowAttributes);
        return appearance.Overlay(Appearance).Overlay(direct);
    }

    /// <summary>Gets unresolved text appearance contributed to retained descendants.</summary>
    internal Appearance GetAmbientAppearance() => AppearanceResolver.ResolveAmbient(this, AmbientAppearanceState);

    /// <summary>Gets local state allowed to contribute ambient text appearance to descendants.</summary>
    internal virtual VisualState AmbientAppearanceState => VisualState.Normal;

    /// <inheritdoc/>
    protected internal TerminalStyle GetResolvedStyle(VisualState state) => GetResolvedAppearance(state).Style;

    internal ResolvedAppearance GetResolvedAppearance(VisualState state) => AppearanceResolver.Resolve(this, state);

    internal Color ResolveThemeColor(ThemeColor color) => Theme?.Resolve(color) ??
        (color.TryGetColor(out var concrete) ? concrete : Color.Default);

    internal static void InvalidateResolvedStyleCache() { }

    internal void InvalidateSubtreeResolvedStyleCache() => VisitChildren(child => child.InvalidateSubtreeResolvedStyleCache());
}
