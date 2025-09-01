using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Observable;


public partial class ObservableDomain
{
    /// <summary>
    /// Creates a singleton instance from its type. Even if it is named "Create", this actually is a "find or create".
    /// <para>
    /// Once useless, the object should be destroyed if it has also been created by other participants: its real destruction
    /// will wait until all participants have also destroyed their obtained instance.
    /// </para>
    /// </summary>
    /// <param name="type">
    /// The singleton type.
    /// Must be a <see cref="IObservableDomainSingleton"/> otherwise an <see cref="ArgumentException"/> is thrown.
    /// </param>
    /// <returns>A new or already existing instance.</returns>
    public IObservableDomainObject CreateSingleton( Type type )
    {
        Throw.CheckArgument( typeof( IObservableDomainSingleton ).IsAssignableFrom( type ) );
        Throw.DebugAssert( !type.IsValueType );
        return DoCreateSingleton( type );
    }

    /// <summary>
    /// Creates a singleton instance for type <typeparamref name="T"/>. Even if it is named "Create", this actually
    /// is a "find or create".
    /// <para>
    /// Once useless, the object should be destroyed if it has also been created by other participants: its real destruction
    /// will wait until all participants have also destroyed their obtained instance.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of singleton to obtain.</typeparam>
    /// <returns>A new or already existing instance.</returns>
    public T CreateSingleton<T>() where T : class, IObservableDomainSingleton
    {
        return (T)DoCreateSingleton( typeof( T ) );
    }

    IObservableDomainSingleton DoCreateSingleton( Type type )
    {
        if( _singletons.TryGetValue( type, out var e ) )
        {
            _singletons[type] = (e.ICount + 1, e.Instance);
            return e.Instance;
        }
        var instance =(IObservableDomainSingleton?)Activator.CreateInstance( type, nonPublic: true );
        Throw.DebugAssert( instance != null );
        _singletons.Add( type, (1, instance) );
        return instance;
    }

    internal bool ShouldDestroySingleton( IObservableDomainSingleton s )
    {
        Throw.DebugAssert( _singletons.ContainsKey( s.GetType() ) );
        var t = s.GetType();
        var (count,instance) = _singletons[t];
        if( --count == 0 )
        {
            _singletons.Remove( t );
            return true;
        }
        _singletons[t] = (count,instance);
        return false;
    }

    /// <summary>
    /// Less than ideal... This should be statically checked.
    /// This current under performant implementation is mitigated by the fact that we call
    /// only if the domain object is a IObservableDomainSingleton.
    /// </summary>
    internal static void ThrowOnPublicConstructors( IObservableDomainSingleton s )
    {
        var t = s.GetType();
        if( t.GetConstructors().Length > 0 )
        {
            Throw.InvalidOperationException( $"Type '{t.ToCSharpName()}' is a 'IObservableDomainSingleton'. It cannot have public constructors." );
        }
    }

    void CheckDeserializedInstance( IObservableDomainSingleton s, ref HashSet<Type>? newSingletonTracker )
    {
        // The object is now a singleton, but was it?
        var t = s.GetType();
        if( !_singletons.ContainsKey( t ) )
        {
            // New singleton type: auto register it.
            _singletons.Add( t, (1, s) );
            // But check that only this instance for the type has been serialized.
            newSingletonTracker ??= new HashSet<Type>();
            newSingletonTracker.Add( t );
        }
        else if( newSingletonTracker != null && newSingletonTracker.Contains( t ) )
        {
            Throw.InvalidDataException( $"Deserialization error for '{t.ToCSharpName()}': this is now a 'IObservableDomainSingleton' " +
                                        $"""
                                        and was not before but (at least) two different instances of this type have been serialized.
                                        There must be at most one instance of this type in the serialized domain before it can be mutated to be a singleton.
                                        """ );
        }
    }
}
