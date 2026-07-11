namespace SharpVision.Controls;

/// <summary>Creates one detached owned control for a List item.</summary>
/// <param name="item">The borrowed item value, which may be null.</param>
/// <returns>A non-null detached undisposed control transferred to the List on success.</returns>
public delegate Control ItemTemplate(object? item);
