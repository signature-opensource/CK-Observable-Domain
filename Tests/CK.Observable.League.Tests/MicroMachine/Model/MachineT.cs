using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
    [BinarySerialization.SerializationVersion( 0 )]
    public abstract class Machine<T> : Machine where T : MachineThing
    {
        readonly ObservableList<T> _all;

        public Machine( string name, MachineConfiguration configuration )
            : base( name, configuration )
        {
            _all = new ObservableList<T>();
        }

        Machine( IBinaryDeserializer r, TypeReadInfo? info )
                : base( BinarySerialization.Sliced.Instance )
        {
            _all = (ObservableList<T>)r.ReadObject()!;
        }

        protected Machine( BinarySerialization.Sliced _ ) : base( _ ) { }

        Machine( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            _all = r.ReadObject<ObservableList<T>>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Machine<T> o )
        {
            s.WriteObject( o._all );
        }

        public IObservableReadOnlyList<T> Things => _all;

        protected abstract T ThingFactory( int tempId );

        protected T? FindThing( int tempId ) => _all.FirstOrDefault( t => t.TemporaryId == tempId );

        internal protected override void OnNewThing( int tempId )
        {
            var info = FindThing( tempId );
            if( info != null )
            {
                info.SetErrorStatus( "ReadProductDuplicateIdentifier" );
            }
            else
            {
                info = ThingFactory( tempId );
                _all.Add( info );
                info.Destroyed += OnProductDisposed;
                info.CreateTimeout( Configuration.IdentifyThingTimeout, OnIdentifyThingTimeout );
            }
        }

        void OnProductDisposed( object sender, ObservableDomainEventArgs e )
        {
            _all.Remove( (T)sender );
        }

        void OnIdentifyThingTimeout( object sender, ObservableReminderEventArgs e )
        {
            var info = (MachineThing)e.Reminder.Tag!;
            if( info.IdentifiedId == null )
            {
                info.SetErrorStatus( "IdentificationTimeout" );
                // Consider that the timeout is a rebout destination confirmation.
                info.OnDestinationConfirmed();
            }
        }

        internal protected override void OnIdentification( int tempId, string identifiedId )
        {
            var info = FindThing( tempId );
            if( info == null )
            {
                Domain.Monitor.Warn( "UnknownTempId" );
            }
            else
            {
                if( info.IdentifiedId == null )
                {
                    info.SetIdentification( identifiedId );
                    info.OnDestinationConfirmed();
                }
                else
                {
                    Domain.Monitor.Error( $"Duplicate tempId received for ProductId n°{tempId}. This has been ignored." );
                }
            }
        }


    }

}
