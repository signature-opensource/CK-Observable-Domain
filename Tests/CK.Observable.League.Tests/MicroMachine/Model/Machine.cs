using CK.BinarySerialization;
using CK.Core;
using System.Diagnostics;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine;

[SerializationVersion( 0 )]
public abstract class Machine : ObservableObject, ISidekickClientObject<MachineSideKick>
{
    readonly SuspendableClock _clock;

    public Machine( string name, MachineConfiguration configuration )
    {
        Name = name;
        Configuration = configuration;
        _clock = new SuspendableClock( isActive: false );
        _clock.IsActiveChanged += ClockIsActiveChanged;
        Domain.EnsureSidekicks();
    }

    protected Machine( Sliced _ ) : base( _ ) { }

    Machine( IBinaryDeserializer d, ITypeReadInfo info )
            : base( Sliced.Instance )
    {
        Debug.Assert( !IsDestroyed );
        Configuration = d.ReadObject<MachineConfiguration>();
        _clock = d.ReadObject<SuspendableClock>()!;
        Name = d.Reader.ReadString();
        d.PostActions.Add( () => IsRunning = _clock.IsActive );
    }

    public static void Write( IBinarySerializer s, in Machine o )
    {
        Debug.Assert( !o.IsDestroyed );
        s.WriteObject( o.Configuration );
        s.WriteObject( o._clock );
        s.Writer.Write( o.Name );
    }

    public string Name { get; }

    public MachineConfiguration Configuration { get; }

    void ClockIsActiveChanged( object sender, ObservableDomainEventArgs e )
    {
        IsRunning = _clock.IsActive;
    }

    public SuspendableClock Clock => _clock;

    public bool IsRunning { get; private set; }

    internal protected abstract void OnNewThing( int tempId );

    internal protected abstract void OnIdentification( int tempId, string identifiedId );

}
