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
            CommandReceivedCount = r.ReadInt32();
        }

        void Write( BinarySerializer w )
        {
            w.Write( CommandReceivedCount );
        }

        public int CommandReceivedCount { get; private set; }

        public void CmdToTheMachine( string bugOrNot )
        {
            ++CommandReceivedCount;
            Domain.SendCommand( new MachineCommand( bugOrNot ) );
            if( bugOrNot == "bug in sending" ) throw new Exception( "Bug in sending command (inside the transaction)." );
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
