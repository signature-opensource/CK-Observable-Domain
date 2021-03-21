using System;

namespace CK.Observable
{
    /// <summary>
    /// Low level serialization API. This is intended for advanced scenarii: normal
    /// use of this library through type based serialization does not require direct
    /// use of these methods.
    /// This interface is exposed through the <see cref="IBinaryDeserializer.ImplementationServices"/> propertY.
    /// </summary>
    public interface IBinaryDeserializerImpl 
    {
        /// <summary>
        /// Gets the <see cref="IDeserializerResolver"/>.
        /// </summary>
        IDeserializerResolver Drivers { get; }

        /// <summary>
        /// Registers an action that will be executed once all objects are deserialized.
        /// </summary>
        /// <param name="a">An action to be registered. Must not be null.</param>
        void OnPostDeserialization( Action a );

        /// <summary>
        /// Creates a new uninitialized object instance. 
        /// </summary>
        /// <param name="t">The type of the instance to create.</param>
        /// <param name="isTrackedObject">True if the object must be tracked (reference type).</param>
        /// <returns>An uninitialized instance of the type.</returns>
        object CreateUninitializedInstance( Type t, bool isTrackedObject );

        /// <summary>
        /// Registers a newly created reference.
        /// </summary>
        /// <param name="o">The object to register. Must not be null.</param>
        /// <returns>The object itself.</returns>
        T TrackObject<T>( T o ) where T : class;

        /// <summary>
        /// Reserves a slot for an object that will be created afterwards.
        /// </summary>
        /// <returns>The object number that must be provided to <see cref="TrackPreTrackedObject{T}(T, int)"/>.</returns>
        int PreTrackObject();

        /// <summary>
        /// Registers a newly created reference that has been pre tracked.
        /// </summary>
        /// <typeparam name="T">Type of the object to register.</typeparam>
        /// <param name="o">The object instance. Must not be null.</param>
        /// <param name="num">The allocated number (see <see cref="PreTrackObject"/>).</param>
        /// <returns>The object itself.</returns>
        T TrackPreTrackedObject<T>( T o, int num ) where T : class;

        /// <summary>
        /// Executes all the post deserialization actions that have been
        /// registered by <see cref="IBinaryDeserializerImpl.OnPostDeserialization(Action)"/>
        /// from deserialization constructors and clears the list.
        /// If not called explicitly, this is automatically called when the <see cref="IBinaryDeserializer"/>
        /// is disposed.
        /// </summary>
        void ExecutePostDeserializationActions();
    }
}
