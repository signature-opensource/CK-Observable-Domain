using CK.Core;
using System;

namespace CK.Observable
{
    /// <summary>
    /// Low level serialization API. This is intended for advanced scenarii: normal
    /// use of this library through type based serialization does not require direct
    /// use of these methods.
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
        /// Pushes a type information before calling a deserialization constructor and
        /// returns the <see cref="IBinaryDeserializerContext"/> that must be used to call it.
        /// </summary>
        /// <param name="info">The type read information.</param>
        /// <returns>The deserializer context to use.</returns>
        IBinaryDeserializerContext PushConstructorContext( TypeReadInfo info );

        /// <summary>
        /// Pops a previously pushed constructor context. Must be called after the call
        /// to the deserialization constructor.
        /// </summary>
        void PopConstructorContext();

        /// <summary>
        /// Executes all the post deserialization actions that have been
        /// registered by <see cref="ICtorBinaryDeserializer.OnPostDeserialization(Action)"/>
        /// from deserialization constructors and clears the list.
        /// If not called explicitly, this is automatically called when the <see cref="IBinaryDeserializer"/>
        /// is disposed.
        /// </summary>
        void ExecutePostDeserializationActions();
    }
}
