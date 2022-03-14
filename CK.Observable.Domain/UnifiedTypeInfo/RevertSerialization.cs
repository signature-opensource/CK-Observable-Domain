using System;
using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// This type is a marker used by the "empty reversed deserializer constructor".
    /// </summary>
    public class RevertSerialization
    {
        /// <summary>
        /// The only instance.
        /// </summary>
        public static readonly RevertSerialization Default = new RevertSerialization();

        [ThreadStatic]
        static Stack<object>? _deserialized;

        /// <summary>
        /// This method must be called by the root objects deserialization constructor
        /// when this "empty reversed deserializer constructor" pattern is used.
        /// <para>
        /// Specialized classes doesn't have to call this.
        /// </para>
        /// </summary>
        /// <param name="o">The root object being deserialized.</param>
        public static void OnRootDeserialized( object o )
        {
            _deserialized.Push( o );
        }

        internal static bool CheckLastDeserialized( object o )
        {
            return _deserialized!.TryPop( out object s ) && s == o;
        }

        internal static IDisposable? StartRevertSerializationCheck()
        {
            if( _deserialized == null )
            {
                _deserialized = new Stack<object>();
                return CK.Core.Util.CreateDisposableAction( () => _deserialized = null );
            }
            return null;
        }

        RevertSerialization() {}
    }
}
