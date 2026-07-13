using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Shared traversal of the control base-type chain used by style resolution.</summary>
/// <remarks>
/// Single source of truth for "walk the <see cref="Control"/> inheritance chain" so registration,
/// theme chains, and snapshot chains cannot drift apart. The walk stops at the first non-control
/// ancestor (for example <see cref="object"/>).
/// </remarks>
internal static class ControlHierarchy
{
    /// <summary>Lists control types from the base <see cref="Control"/> down to the concrete type.</summary>
    /// <param name="controlType">The concrete control type.</param>
    /// <returns>Control types ordered base-first so derived declarations win when applied in order.</returns>
    internal static List<Type> BaseToDerived(Type controlType)
    {
        var chain = new List<Type>();

        for (var current = controlType;
            current is not null && typeof(Control).IsAssignableFrom(current);
            current = current.BaseType)
        {
            chain.Add(current);
        }

        chain.Reverse();
        return chain;
    }
}
