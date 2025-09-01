using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable;


/// <summary>
/// Marker interface for singleton object (<see cref="ObservableObject"/> or <see cref="InternalObject"/>).
/// <para>
/// Singletons are created by calling <see cref="DomainView.CreateSingleton(Type)"/> or <see cref="DomainView.CreateSingleton{T}"/>.
/// If an instance already exists, an internal instantiation count is incremented and the existing instance is returned.
/// Singletons can be destroyed the usual way (by calling <see cref="ObservableObject.Destroy"/> or <see cref="InternalObject.Destroy"/>) but
/// are really destroyed when their instantiation count falls to 0.
/// </para>
/// <para>
/// Classes that implement this interface must be abstract or sealed. Abstract classes must have no constructors,
/// sealed classes must have an internal parameter-less constructor.
/// </para>
/// </summary>
public interface IObservableDomainSingleton : IObservableDomainObject
{
}
