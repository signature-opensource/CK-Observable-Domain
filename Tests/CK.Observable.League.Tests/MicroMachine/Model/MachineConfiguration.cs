using CK.Core;
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

        MachineConfiguration( IBinaryDeserializer r, TypeReadInfo? info )
                : base( BinarySerialization.Sliced.Instance )
        {
            Debug.Assert( !IsDestroyed );
            IdentifyThingTimeout = r.ReadTimeSpan();
            AutoDestroyedTimeout = r.ReadTimeSpan();
        }

        protected MachineConfiguration( BinarySerialization.Sliced _ ) : base( _ ) { }

        MachineConfiguration( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            Debug.Assert( !IsDestroyed );
            IdentifyThingTimeout = r.Reader.ReadTimeSpan();
            AutoDestroyedTimeout = r.Reader.ReadTimeSpan();
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in MachineConfiguration o )
        {
            Debug.Assert( !o.IsDestroyed );
            w.Writer.Write( o.IdentifyThingTimeout );
            w.Writer.Write( o.AutoDestroyedTimeout );
        }

        public TimeSpan IdentifyThingTimeout { get; set; } = TimeSpan.FromMilliseconds( 200 );

        public TimeSpan AutoDestroyedTimeout { get; set; } = TimeSpan.FromMilliseconds( 400 );

    }
}
