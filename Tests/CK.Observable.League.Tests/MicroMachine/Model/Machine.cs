using System.Diagnostics;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
    [BinarySerialization.SerializationVersion( 0 )]
    public abstract class Machine : ObservableObject, ISidekickClientObject<MachineSideKick>
    {
        readonly SuspendableClock _clock;

        public Machine( string name, MachineConfiguration configuration )
        {
            Name = name;
            Configuration = configuration;
            _clock = new SuspendableClock( isActive: false );
            _clock.IsActiveChanged += ClockIsActiveChanged;
        }

        Machine( IBinaryDeserializer r, TypeReadInfo? info )
                : base( BinarySerialization.Sliced.Instance )
        {
            Debug.Assert( !IsDestroyed );
            Configuration = (MachineConfiguration)r.ReadObject()!;
            _clock = (SuspendableClock)r.ReadObject()!;
            Name = r.ReadString();
            r.ImplementationServices.OnPostDeserialization( () => IsRunning = _clock.IsActive );
        }

        protected Machine( BinarySerialization.Sliced _ ) : base( _ ) { }

        Machine( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            Debug.Assert( !IsDestroyed );
            Configuration = d.ReadObject<MachineConfiguration>();
            _clock = d.ReadObject<SuspendableClock>()!;
            Name = d.Reader.ReadString();
            d.PostActions.Add( () => IsRunning = _clock.IsActive );
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Machine o )
        {
            Debug.Assert( !o.IsDestroyed );
            s.WriteObject( o.Configuration );
            s.WriteObject( o._clock );
            s.Writer.Write( o.Name );
        }

        public string Name { get; }

        /// <summary>
        /// This is NOT to be exposed in real life.
        /// This shows that the observable object may interact with its sidekick if needed.
        /// </summary>
        public MachineSideKick.MicroBridge BridgeToTheSidekick { get; internal set; }

        /// <summary>
        /// This is typically done in the constructor...
        /// </summary>
        public void TestCallEnsureBridge() => Domain.EnsureSidekicks();

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

}
