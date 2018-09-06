using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class ObservableDomain<T> : ObservableDomain where T : ObservableRootObject
    {
        public ObservableDomain()
            : this( null, null )
        {
        }

        public ObservableDomain( IActivityMonitor monitor )
            : this( null, monitor )
        {
        }

        public ObservableDomain( IObservableTransactionManager tm )
            : this( tm, null )
        {
        }

        public ObservableDomain( IObservableTransactionManager tm, IActivityMonitor monitor )
            : base( tm, monitor )
        {
            Root = AddRoot<T>();
        }

        /// <summary>
        /// Initializes a previously <see cref="Save"/>d domain.
        /// </summary>
        /// <param name="tm">The transaction manager to use. Can be null.</param>
        /// <param name="monitor">The monitor associated to the domain. Can be null (a dedicated one will be created).</param>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        public ObservableDomain(
            IObservableTransactionManager tm,
            IActivityMonitor monitor,
            Stream s,
            bool leaveOpen = false,
            Encoding encoding = null )
            : base( tm, monitor, s, leaveOpen, encoding )
        {
            if( AllRoots.Count != 1 || !(AllRoots[0] is T) )
            {
                throw new InvalidDataException( $"Incompatible stream. No root of type {typeof(T).FullName}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
            }
            Root = (T)AllRoots[0];
        }

        /// <summary>
        /// Gets the typed root object.
        /// </summary>
        public T Root { get; }

    }
}
