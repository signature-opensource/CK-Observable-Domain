using System;

namespace CK.Observable
{
    public interface ICtorBinaryDeserializer : IBinaryDeserializer
    {
        /// <summary>
        /// Get the type based information as it has been written.
        /// If the object has been written by an external driver, this is null.
        /// </summary>
        TypeReadInfo CurrentReadInfo { get; }

        /// <summary>
        /// Registers an acvtion that will be executed once all objects are deserialized.
        /// </summary>
        /// <param name="a">An action to be registered. Must not be null.</param>
        void OnPostDeserialization( Action a );
    }
}
