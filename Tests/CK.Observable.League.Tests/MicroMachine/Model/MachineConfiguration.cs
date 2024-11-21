using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine;

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

    protected MachineConfiguration( Sliced _ ) : base( _ ) { }

    MachineConfiguration( IBinaryDeserializer d, ITypeReadInfo info )
            : base( Sliced.Instance )
    {
        Debug.Assert( !IsDestroyed );
        IdentifyThingTimeout = d.Reader.ReadTimeSpan();
        AutoDestroyedTimeout = d.Reader.ReadTimeSpan();
    }

    public static void Write( IBinarySerializer s, in MachineConfiguration o )
    {
        Debug.Assert( !o.IsDestroyed );
        s.Writer.Write( o.IdentifyThingTimeout );
        s.Writer.Write( o.AutoDestroyedTimeout );
    }

    public TimeSpan IdentifyThingTimeout { get; set; } = TimeSpan.FromMilliseconds( 200 );

    public TimeSpan AutoDestroyedTimeout { get; set; } = TimeSpan.FromMilliseconds( 400 );

}
