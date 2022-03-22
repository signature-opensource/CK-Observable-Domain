//using System;

//namespace CK.Observable
//{
//    /// <summary>
//    /// Handles serialization of instances of a given type.
//    /// Note that this abstraction doesn't require to be bound to a Type: the <see cref="SerializerRegistry"/>
//    /// is in charge of the association from a Type to a driver.
//    /// </summary>
//    public interface ITypeSerializationDriver
//    {
//        /// <summary>
//        /// Gets whether this type cannot have any subordinate types.
//        /// </summary>
//        bool IsFinalType { get; }

//        /// <summary>
//        /// Gets whether this type can be written in two parts (header and then the actual data).
//        /// This enables to handle too deep recursion in object graphs (typically because of linked list).
//        /// When an object is written in defferred mode, its deserialization driver must be able to handle this: it must be
//        /// a  <see cref="IDeserializationDeferredDriver"/>.
//        /// Defaults to false.
//        /// </summary>
//        /// <remarks>
//        /// Currently only <see cref="IUnifiedTypeDriver"/> automatic implementation allows deferred deserialization
//        /// and since this is the one used for every objects other tha array, list and dictionary, it is fine: regular
//        /// objects benefit from this behavior (and they are the ones that may be chained).
//        /// </remarks>
//        bool AllowDeferred => false;

//        /// <summary>
//        /// Writes the type descriptor in the serializer.
//        /// </summary>
//        /// <param name="s">The serializer.</param>
//        void WriteTypeInformation( BinarySerializer s ); 

//        /// <summary>
//        /// Writes the object's data.
//        /// </summary>
//        /// <param name="w">The serializer.</param>
//        /// <param name="o">The object instance.</param>
//        void WriteData( BinarySerializer w, object o );

//    }
//}
