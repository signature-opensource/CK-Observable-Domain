using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Observable.League.Tests.MicroMachine
{
    [BinarySerialization.SerializationVersion( 0 )]
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
        protected MachineThing( BinarySerialization.Sliced _ ) : base( _ ) { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        MachineThing( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            Debug.Assert( !IsDestroyed );
            Machine = r.ReadObject<Machine>();
            TemporaryId = r.Reader.ReadSmallInt32( 1 );
            _startRead = r.Reader.ReadDateTime();
            _reminders = r.ReadObject<List<ObservableReminder>>();
            IdentifiedId = r.Reader.ReadNullableString();
            Error = r.Reader.ReadNullableString();
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in MachineThing o )
        {
            Debug.Assert( !o.IsDestroyed );
            w.WriteObject( o.Machine );
            w.Writer.WriteSmallInt32( o.TemporaryId, 1 );
            w.Writer.Write( o._startRead );
            w.WriteNullableObject( o._reminders );
            w.Writer.WriteNullableString( o.IdentifiedId );
            w.Writer.WriteNullableString( o.Error );
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
                foreach( var r in _reminders ) r.Destroy();
                _reminders.Clear();
            }
        }

        /// <summary>
        /// By default, this simply calls <see cref="ClearTimeouts()"/> and sets a new timeout of <see cref="MachineConfiguration.AutoDestroyedTimeout"/>
        /// that will call <see cref="Dispose()"/>.
        /// </summary>
        internal protected virtual void OnDestinationConfirmed()
        {
            using( Domain.Monitor.OpenInfo( "OnDestinationConfirmed" ) )
            {
                ClearTimeouts();
                CreateTimeout( Machine.Configuration.AutoDestroyedTimeout, OnAutoDestroyedTimeout );
            }
        }

        protected override void OnDestroy()
        {
            ClearTimeouts();
            base.OnDestroy();
        }

        void OnAutoDestroyedTimeout( object sender, ObservableReminderEventArgs e )
        {
            Domain.Monitor.Info( "OnAutoDisposedTimeout" );
            Destroy();
        }

    }

}
