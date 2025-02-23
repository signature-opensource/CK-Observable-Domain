using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine;

[SerializationVersion( 0 )]
public class SpecializedMachine : Machine<Thing>
{
    public SpecializedMachine( string deviceName, MachineConfiguration configuration )
        : base( deviceName, configuration )
    {
    }

    SpecializedMachine( IBinaryDeserializer r, ITypeReadInfo info )
            : base( Sliced.Instance )
    {
        CommandReceivedCount = r.Reader.ReadInt32();
    }

    public static void Write( IBinarySerializer s, in SpecializedMachine o )
    {
        s.Writer.Write( o.CommandReceivedCount );
    }

    public int CommandReceivedCount { get; private set; }

    public void CmdToTheMachine( string bugOrNot )
    {
        ++CommandReceivedCount;
        Domain.SendBroadcastCommand( new MachineCommand( bugOrNot ) );
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
