using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// The exception that is thrown when an operation is performed on a destroyed object.
    /// </summary>
    public class ObjectDestroyedException : Exception
    {
        /// <summary>
        /// Initializes a new destroyed exception with a string containing the name of the destroyed object.
        /// </summary>
        /// <param name="message">The exception message (the object's description).</param>
        public ObjectDestroyedException( string message )
            : base( message )
        {
        }
    }
}
