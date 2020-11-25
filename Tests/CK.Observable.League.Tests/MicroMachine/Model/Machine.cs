using System.Diagnostics;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
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
        }

        protected Machine( RevertSerialization _ ) : base( _ ) { }

        Machine( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            Debug.Assert( !IsDisposed );
            Configuration = (MachineConfiguration)r.ReadObject()!;
            _clock = (SuspendableClock)r.ReadObject()!;
            Name = r.ReadString();
            r.ImplementationServices.OnPostDeserialization( () => IsRunning = _clock.IsActive );
        }

        void Write( BinarySerializer w )
        {
            Debug.Assert( !IsDisposed );
            w.WriteObject( Configuration );
            w.WriteObject( _clock );
            w.Write( Name );
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
