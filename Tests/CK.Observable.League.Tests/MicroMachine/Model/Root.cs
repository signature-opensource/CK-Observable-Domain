using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
    [SerializationVersion( 0 )]
    //[UseSidekick(typeof(ArtemisSidekick))]
    public class Root : ObservableRootObject
    {
        public Root()
        {
            Machine = new SpecializedMachine( "Artemis", new MachineConfiguration() );
        }

        Root( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            Machine = (SpecializedMachine)r.ReadObject()!;
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( Machine );
        }


        /// <summary>
        /// Gets the "Artemis" machine.
        /// </summary>
        public SpecializedMachine Machine { get; }

    }
}
