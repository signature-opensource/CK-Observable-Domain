using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
    /// <summary>
    /// Defines a <see cref="Machine"/> configuration that drives the machine behavior with 
    /// values (like <see cref="IdentifyThingTimeout"/>) and/or strategies (like <see cref="GetErrorExit(ProductErrorStatus)"/>.
    /// This can be specialized.
    /// </summary>
    [SerializationVersion( 0 )]
    public class MachineConfiguration : ObservableObject
    {
        /// <summary>
        /// Initializes a new default configuration.
        /// </summary>
        public MachineConfiguration()
        {
        }

        protected MachineConfiguration( RevertSerialization _ ) : base( _ ) { }

        MachineConfiguration( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            Debug.Assert( !IsDestroyed );
            IdentifyThingTimeout = r.ReadTimeSpan();
            AutoDestroyedTimeout = r.ReadTimeSpan();
        }

        void Write( BinarySerializer w )
        {
            Debug.Assert( !IsDestroyed );
            w.Write( IdentifyThingTimeout );
            w.Write( AutoDestroyedTimeout );
        }

        public TimeSpan IdentifyThingTimeout { get; set; } = TimeSpan.FromMilliseconds( 200 );

        public TimeSpan AutoDestroyedTimeout { get; set; } = TimeSpan.FromMilliseconds( 400 );

    }
}
