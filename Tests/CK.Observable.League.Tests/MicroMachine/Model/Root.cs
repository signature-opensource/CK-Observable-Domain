using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
    [SerializationVersion( 0 )]
    public class Root : ObservableRootObject
    {
        public Root()
        {
            Machine = new SpecializedMachine( "Artemis", new MachineConfiguration() );
        }

        Root( IBinaryDeserializer r, ITypeReadInfo? info )
                : base( Sliced.Instance )
        {
            Machine = r.ReadObject<SpecializedMachine>();
        }

        public static void Write( IBinarySerializer s, in Root o )
        {
            s.WriteObject( o.Machine );
        }

        /// <summary>
        /// Gets the "Artemis" machine.
        /// </summary>
        public SpecializedMachine Machine { get; }

    }
}
