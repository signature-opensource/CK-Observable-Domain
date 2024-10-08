using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable;

/// <summary>
/// Describes required (may be <see cref="Optional"/>) sidekick type that should be available on the domain.
/// This attribute can be set on <see cref="InternalObject"/> or <see cref="ObservableObject"/> classes
/// multiple times.
/// <para>
/// Use <see cref="ISidekickClientObject{TSidekick}"/> if you want the <see cref="ObservableDomainSidekick.RegisterClientObject(Core.IActivityMonitor, IDestroyable)"/>
/// to be called with the new instances of the decorated type. This [UseSidekick] attribute simply activates the sidekick but doesn't imply any specific affinity
/// or coupling between the decorated type and the sidekick.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class UseSidekickAttribute : Attribute
{
    /// <summary>
    /// Defines a sidekick type that must be available on the domain.
    /// </summary>
    /// <param name="type">The sidekick type.</param>
    public UseSidekickAttribute( Type type )
    {
    }

    /// <summary>
    /// Defines a sidekick type through its name (late binding) that must be available on the domain.
    /// </summary>
    /// <param name="assemblyQualifiedName">The sidekick type's assembly qualified name.</param>
    public UseSidekickAttribute( string assemblyQualifiedName )
    {
    }

    /// <summary>
    /// Gets or sets whether the sidekick is optional.
    /// Default to false.
    /// </summary>
    public bool Optional { get; set; }
}
