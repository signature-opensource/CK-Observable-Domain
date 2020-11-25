using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Observable.League.Tests.MicroMachine
{
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

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        protected MachineThing( RevertSerialization _ ) : base( _ ) { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        MachineThing( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            Debug.Assert( !IsDisposed );
            Machine = (Machine)r.ReadObject()!;
            TemporaryId = r.ReadSmallInt32( 1 );
            _startRead = r.ReadDateTime();
            _reminders = (List<ObservableReminder>?)r.ReadObject();
            IdentifiedId = r.ReadNullableString();
            Error = r.ReadNullableString();
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
            Domain.Monitor.Info( "CreateTimeout" );
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
            Domain.Monitor.Info( $"Clearing {_reminders?.Count ?? 0} reminders." );
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
            using( Domain.Monitor.OpenInfo( "OnDestinationConfirmed" ) )
            {
                ClearTimeouts();
                CreateTimeout( Machine.Configuration.AutoDisposedTimeout, OnAutoDisposedTimeout );
            }
        }

        protected override void Dispose( bool shouldCleanup )
        {
            if( shouldCleanup )
            {
                ClearTimeouts();
            }
            base.Dispose( shouldCleanup );
        }

        void OnAutoDisposedTimeout( object sender, ObservableReminderEventArgs e )
        {
            Domain.Monitor.Info( "OnAutoDisposedTimeout" );
            Dispose();
        }

    }

}
