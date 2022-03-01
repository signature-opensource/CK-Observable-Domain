using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
    [BinarySerialization.SerializationVersion( 0 )]
    public class SpecializedMachine : Machine<Thing>
    {
        public SpecializedMachine( string deviceName, MachineConfiguration configuration )
            : base( deviceName, configuration )
        {
        }

        SpecializedMachine( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            CommandReceivedCount = r.Reader.ReadInt32();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in SpecializedMachine o )
        {
            s.Writer.Write( o.CommandReceivedCount );
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

}
