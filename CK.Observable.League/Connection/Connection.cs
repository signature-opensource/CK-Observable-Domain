using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// A connection is bound to a user.
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    public class Connection<TUser>
    {

        /// <summary>
        /// Gets the user bound to this connection.
        /// </summary>
        public TUser User { get; }



    }
}
