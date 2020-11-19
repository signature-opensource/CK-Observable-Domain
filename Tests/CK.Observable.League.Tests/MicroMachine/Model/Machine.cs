using CK.Core;
using System;
using System.Collections.Generic;
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

    [SerializationVersion( 0 )]
    public class MachineThing : ObservableObject
    {
        readonly DateTime _startRead;
        List<ObservableReminder>? _reminders;

        public MachineThing( Machine m, int index )
        {
            Machine = m;
            TemporaryId = index;
            _startRead = DateTime.UtcNow;
        }

        protected MachineThing( RevertSerialization _ ) : base( _ ) { }

        MachineThing( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            if( !IsDisposed )
            {
                Machine = (Machine)r.ReadObject()!;
                TemporaryId = r.ReadSmallInt32( 1 );
                _startRead = r.ReadDateTime();
                _reminders = (List<ObservableReminder>?)r.ReadObject();
                IdentifiedId = r.ReadNullableString();
                Error = r.ReadNullableString();
            }
        }

        void Write( BinarySerializer w )
        {
            if( !IsDisposed )
            {
                w.WriteObject( Machine );
                w.WriteSmallInt32( TemporaryId, 1 );
                w.Write( _startRead );
                w.WriteObject( _reminders );
                w.WriteNullableString( IdentifiedId );
                w.WriteNullableString( Error );
            }
        }

        public Machine Machine { get; }

        public DateTime StartTime => _startRead;

        public int TemporaryId { get; }

        public string? IdentifiedId { get; private set; }

        public string? Error { get; private set; }

        internal void SetIdentification( string id )
        {
            IdentifiedId = id;
        }

        int GetTimeMilliseconds() => (int)(DateTime.UtcNow - StartTime).Ticks / (int)TimeSpan.TicksPerMillisecond;

        internal protected void CreateTimeout( TimeSpan timeout, SafeEventHandler<ObservableReminderEventArgs> callback )
        {
            if( timeout > TimeSpan.Zero && timeout < TimeSpan.MaxValue )
            {
                var r = new ObservableReminder( StartTime + timeout + Machine.Clock.CumulativeOffset );
                r.Elapsed += callback;
                r.SuspendableClock = Machine.Clock;
                r.Tag = this;
                if( _reminders == null ) _reminders = new List<ObservableReminder>();
                _reminders.Add( r );
            }
        }

        internal protected virtual void SetErrorStatus( string error )
        {
            Error = error;
            Domain.Monitor.Warn( $"Error set for thing '{ToString()}'." );
            ClearTimeouts();
        }

        protected void ClearTimeouts()
        {
            if( _reminders != null && _reminders.Count > 0 )
            {
                foreach( var r in _reminders ) r.Dispose();
                _reminders.Clear();
            }
        }

        /// <summary>
        /// By default, this simply calls <see cref="ClearTimeouts()"/> and sets a new timeout of <see cref="MachineConfiguration.AutoDisposedTimeout"/>
        /// that will call <see cref="Dispose()"/>.
        /// </summary>
        internal protected virtual void OnDestinationConfirmed()
        {
            ClearTimeouts();
            CreateTimeout( Machine.Configuration.AutoDisposedTimeout, OnAutoDisposedTimeout );
        }

        void OnAutoDisposedTimeout( object sender, ObservableReminderEventArgs e )
        {
            Dispose();
        }

    }

}
