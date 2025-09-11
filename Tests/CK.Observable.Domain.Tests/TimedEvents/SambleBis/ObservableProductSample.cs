using CK.Core;
using System;

namespace CK.Observable.Domain.Tests;

[SerializationVersion( 0 )]
class ObservableProductSample : ObservableObject
{
    ObservableReminder? _autoDestroyReminder;

    public ObservableProductSample( Machine m )
    {
        Machine = m;
    }

    public string? Name { get; set; }

    public Machine Machine { get; }

    ObservableProductSample( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
        : base( BinarySerialization.Sliced.Instance )
    {
        Name = r.Reader.ReadNullableString();
        Machine = r.ReadObject<Machine>();
        _autoDestroyReminder = r.ReadNullableObject<ObservableReminder>();
    }

    public static void Write( BinarySerialization.IBinarySerializer w, in ObservableProductSample o )
    {
        w.Writer.WriteNullableString( o.Name );
        w.WriteObject( o.Machine );
        w.WriteNullableObject( o._autoDestroyReminder );
    }

    protected override void OnDestroy()
    {
        _autoDestroyReminder?.Destroy();
        base.OnDestroy();
    }

    public void SetAutoDestroyTimeout( TimeSpan? delay )
    {
        if( !delay.HasValue )
        {
            _autoDestroyReminder?.Destroy();
            _autoDestroyReminder = null;
        }
        else
        {
            if( _autoDestroyReminder != null )
            {
                _autoDestroyReminder.DueTimeUtc = DateTime.UtcNow.Add( delay.Value );
            }
            else
            {
                _autoDestroyReminder = new ObservableReminder( DateTime.UtcNow.Add( delay.Value ) );
                _autoDestroyReminder.Elapsed += OnAutoDestroyedTimeout;
                _autoDestroyReminder.SuspendableClock = Machine.Clock;
            }
        }
    }

    // This method should not be renamed because of deserialization.
    void OnAutoDestroyedTimeout( object sender, ObservableReminderEventArgs e )
    {
        e.Monitor.Trace( $"AutoDestroying {ToString()}." );
        Destroy();
    }
}
