namespace SharpVision.Styling;

/// <summary>Internal theme-lifecycle operations for a control style owned by a theme.</summary>
/// <remarks>
/// Kept off the public <see cref="IControlStyle"/> surface so that interface stays a pure read-only
/// view; only the built-in <see cref="ControlStyle{TControl}"/> implements these operations.
/// </remarks>
internal interface IStyleLifecycle
{
    /// <summary>Creates an independent unfrozen copy of this style.</summary>
    public IControlStyle CloneForTheme();

    /// <summary>Creates a frozen copy of this style.</summary>
    public IControlStyle FreezeForTheme();
}
