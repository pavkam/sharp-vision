// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Owns one generation of temporary property overrides for a retained child.</summary>
/// <remarks>
/// <para>
/// A lease is one generation: it captures the caller-authored value for each property it controls
/// the moment it is acquired through <see cref="RetainedPropertyOverrideService.Acquire"/>, and it
/// stays the exclusive writer for those properties on its exact <see cref="Child"/> until it is
/// restored or retired. This is the generation rule every public member below depends on: once a
/// lease stops being <see cref="IsCurrent"/> - because a later <see cref="RetainedPropertyOverrideService.Acquire"/>
/// call superseded it, or because it already restored or retired - every write it attempts through
/// <see cref="SetLive{T}"/> is silently ignored rather than landing on a control a different
/// generation, or no generation at all, now owns.
/// </para>
/// <para>
/// A caller request against a controlled property while the lease is current updates only the
/// captured authored value, never the live one; the live value changes only through
/// <see cref="SetLive{T}"/>. <see cref="Restore"/> writes every captured authored value back to
/// the child before retiring, but skips the write entirely - retiring only - once the child itself
/// is disposed or disposing, because a disposing control's storage is no longer a safe write
/// target. <see cref="Retire"/> never writes anything back.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class RetainedPropertyOverrideLease
{
    private readonly Action<ControlBase, RetainedControlProperty>? _authoredValueChanged;
    private readonly Dictionary<RetainedControlProperty, RetainedPropertyOverrideEntry> _entries;
    private readonly Action<RetainedPropertyOverrideLease> _retired;
    private RetainedControlProperty? _writingProperty;
    private object? _writingValue;

    /// <summary>Initializes and installs one unique child generation.</summary>
    /// <param name="child">The owned child whose authored values are captured.</param>
    /// <param name="descriptors">The non-empty distinct property descriptor snapshot.</param>
    /// <param name="authoredValueChanged">Optional callback after a caller request is captured.</param>
    /// <param name="retired">The non-null service callback that releases generation metadata.</param>
    internal RetainedPropertyOverrideLease(
        ControlBase child,
        IReadOnlyList<RetainedPropertyOverrideDescriptor> descriptors,
        Action<ControlBase, RetainedControlProperty>? authoredValueChanged,
        Action<RetainedPropertyOverrideLease> retired)
    {
        Debug.Assert(child is not null, "A retained override lease requires its child.");
        Debug.Assert(descriptors is not null && descriptors.Count > 0, "A lease requires property descriptors.");
        Debug.Assert(retired is not null, "A lease requires retirement coordination.");
        Child = child;
        _authoredValueChanged = authoredValueChanged;
        _retired = retired;
        _entries = new Dictionary<RetainedControlProperty, RetainedPropertyOverrideEntry>(descriptors.Count);

        foreach (var descriptor in descriptors)
        {
            if (!_entries.TryAdd(descriptor.Property, new RetainedPropertyOverrideEntry(descriptor, child)))
            {
                throw new ArgumentException("A retained property override cannot repeat a property.", nameof(descriptors));
            }
        }

        child.InstallRetainedPropertyOverride(this);
    }

    /// <summary>Gets whether this generation still controls its child.</summary>
    /// <remarks>
    /// This is the exact currency rule the generation model depends on: true only while this
    /// instance is neither restored nor retired and is still the one generation
    /// <see cref="Child"/> currently records. A later <see cref="RetainedPropertyOverrideService.Acquire"/>
    /// call for the same child installs a new generation and immediately makes every earlier one
    /// report false here, without racing or requiring the earlier holder to notice on its own.
    /// </remarks>
    public bool IsCurrent => !IsRetired && ReferenceEquals(Child.RetainedPropertyOverride, this);

    /// <summary>Gets the exact child generation bound by this lease.</summary>
    public ControlBase Child { get; }

    private bool IsRetired { get; set; }

    /// <summary>Gets whether an owner-attributed live write is active for one property.</summary>
    /// <param name="property">The queried property.</param>
    /// <returns>True only during the matching owner write.</returns>
    public bool IsWriting(RetainedControlProperty property) => _writingProperty == property;

    /// <summary>Gets the latest caller-authored value for one controlled property.</summary>
    /// <typeparam name="T">The exact property value type.</typeparam>
    /// <param name="property">The controlled property.</param>
    /// <returns>The latest authored value.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="property"/> is not controlled by this lease, or <typeparamref name="T"/>
    /// does not match its registered value type.
    /// </exception>
    public T GetAuthored<T>(RetainedControlProperty property)
        where T : notnull
    {
        var entry = RequireEntry<T>(property);
        return (T) entry.AuthoredValue;
    }

    /// <summary>Writes an owner-imposed live value without recapturing it as authored state.</summary>
    /// <typeparam name="T">The exact property value type.</typeparam>
    /// <param name="property">The controlled property.</param>
    /// <param name="value">The imposed live value.</param>
    /// <remarks>
    /// A no-op once this generation is no longer <see cref="IsCurrent"/>, so a caller may issue
    /// several writes in sequence and rely on each one independently re-checking currency rather
    /// than assuming an earlier write in the same sequence still holds the generation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="property"/> is not controlled by this lease, or <typeparamref name="T"/>
    /// does not match its registered value type.
    /// </exception>
    public void SetLive<T>(RetainedControlProperty property, T value)
        where T : notnull
    {
        if (!IsCurrent)
        {
            return;
        }

        var entry = RequireEntry<T>(property);
        var previousProperty = _writingProperty;
        var previousValue = _writingValue;
        _writingProperty = property;
        _writingValue = value;

        try
        {
            entry.Descriptor.Write(Child, value);
        }
        finally
        {
            _writingProperty = previousProperty;
            _writingValue = previousValue;
        }
    }

    /// <summary>Consumes a caller request or permits the attributed owner write to reach storage.</summary>
    /// <typeparam name="T">The exact property value type.</typeparam>
    /// <param name="property">The requested property.</param>
    /// <param name="value">The requested value.</param>
    /// <returns>True when the request updated authored state only; false when normal storage should continue.</returns>
    internal bool TryHandleRequest<T>(RetainedControlProperty property, T value)
        where T : notnull
    {
        if (!IsCurrent || !_entries.TryGetValue(property, out var entry))
        {
            return false;
        }

        if (entry.Descriptor.ValueType != typeof(T))
        {
            throw new InvalidOperationException("A retained property request used the wrong value type.");
        }

        if (_writingProperty == property && Equals(_writingValue, value))
        {
            return false;
        }

        entry.AuthoredValue = value;
        _authoredValueChanged?.Invoke(Child, property);
        return true;
    }

    /// <summary>Restores authored values while this generation remains current, then retires it.</summary>
    /// <remarks>
    /// Writing stops the moment this generation is no longer <see cref="IsCurrent"/> or
    /// <see cref="Child"/> is disposed or disposing, leaving any remaining controlled properties at
    /// their last live value rather than attempting an unsafe write; the generation is retired
    /// either way. Idempotent: retiring an already-retired generation is a no-op.
    /// </remarks>
    public void Restore()
    {
        ExceptionDispatchInfo? failure = null;

        try
        {
            foreach (var entry in _entries.Values.ToArray())
            {
                if (!IsCurrent || Child.IsDisposed || Child.IsDisposing)
                {
                    break;
                }

                ExceptionAggregation.Capture(() => SetLiveObject(entry), ref failure);
            }
        }
        finally
        {
            Retire();
        }

        failure?.Throw();
    }

    /// <summary>Retires metadata without restoring values.</summary>
    /// <remarks>Idempotent: retiring an already-retired generation is a no-op.</remarks>
    public void Retire()
    {
        if (IsRetired)
        {
            return;
        }

        IsRetired = true;
        Child.ClearRetainedPropertyOverride(this);
        _entries.Clear();
        _retired(this);
    }

    private RetainedPropertyOverrideEntry RequireEntry<T>(RetainedControlProperty property)
        where T : notnull
    {
        return !_entries.TryGetValue(property, out var entry) || entry.Descriptor.ValueType != typeof(T)
            ? throw new InvalidOperationException("The retained property is not controlled by this lease and type.")
            : entry;
    }

    private void SetLiveObject(RetainedPropertyOverrideEntry entry)
    {
        var previousProperty = _writingProperty;
        var previousValue = _writingValue;
        _writingProperty = entry.Descriptor.Property;
        _writingValue = entry.AuthoredValue;

        try
        {
            entry.Descriptor.Write(Child, entry.AuthoredValue);
        }
        finally
        {
            _writingProperty = previousProperty;
            _writingValue = previousValue;
        }
    }
}
