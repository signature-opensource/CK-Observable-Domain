using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
    [SerializationVersion( 0 )]
    public class SpecializedMachine : Machine<Thing>
    {
        public SpecializedMachine( string deviceName, MachineConfiguration configuration )
            : base( deviceName, configuration )
        {
        }

        SpecializedMachine( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
        }

        void Write( BinarySerializer s )
        {
        }

        public void CmdToTheMachine()
        {
            Domain.SendCommand( new MachineCommand() );
        }

        public void CreateThing( int productId )
        {
            OnNewThing( productId );
        }

        public void SetIdentification( int tempId, string identifiedId )
        {
            OnIdentification( tempId, identifiedId );
        }

        protected override Thing ThingFactory( int tempId ) => new Thing( this, tempId );
    }

    [SerializationVersion( 0 )]
    public class Thing : MachineThing
    {
        public Thing( SpecializedMachine m, int tempId )
            : base( m, tempId )
        {
        }

        protected Thing( RevertSerialization _ ) : base( _ ) { }

        Thing( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
        }

        void Write( BinarySerializer w )
        {
        }
    }

}
