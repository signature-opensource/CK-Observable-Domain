using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Observable
{

    public partial class ObservableDomain
    {
        /// <summary>
        /// The change tracker handles the transformation of actual changes into events that are
        /// optimized and serialized by the <see cref="Commit"/> method.
        /// </summary>
        class ChangeTracker
        {
            class PropChanged
            {
                public readonly ObservableObject Object;
                public readonly ObservablePropertyChangedEventArgs Info;
                public object? FinalValue;

                public long Key => Info.GetObjectPropertyId( Object );

                public PropChanged( ObservableObject o, ObservablePropertyChangedEventArgs p, object? final )
                {
                    Object = o;
                    Info = p;
                    FinalValue = final;
                }
            }

            readonly List<ObservableEvent> _changeEvents;
            readonly Dictionary<ObservableObject, List<PropertyInfo>?> _newObjects;
            readonly Dictionary<long, PropChanged> _propChanged;
            // A new list is allocated each time since commands can be appended to it after the commit, during the
            // OnSuccessfulTransaction raising.
            List<ObservableDomainCommand> _commands;

            public ChangeTracker()
            {
                _changeEvents = new List<ObservableEvent>();
                _newObjects = new Dictionary<ObservableObject, List<PropertyInfo>?>( PureObjectRefEqualityComparer<ObservableObject>.Default );
                _propChanged = new Dictionary<long, PropChanged>();
                _commands = new List<ObservableDomainCommand>();
            }

            public TransactionDoneEventArgs Commit( ObservableDomain domain,
                                                          Func<string, ObservablePropertyChangedEventArgs> ensurePropertInfo,
                                                          DateTime startTime,
                                                          int tranNum,
                                                          RolledbackTransactionInfo? rollbacked )
            {
                Debug.Assert( rollbacked == null || !rollbacked.Failure.Success );
                _changeEvents.RemoveAll( e => e is ICollectionEvent c && c.Object.IsDestroyed );
                foreach( var p in _propChanged.Values )
                {
                    if( !p.Object.IsDestroyed )
                    {
                        _changeEvents.Add( new PropertyChangedEvent( p.Object, p.Info.PropertyId, p.Info.PropertyName, p.FinalValue ) );
                        if( _newObjects.TryGetValue( p.Object, out var exportables ) )
                        {
                            Debug.Assert( exportables != null, "If the object is not exportable, there must be no property changed events." );
                            int idx = exportables.IndexOf( exp => exp.Name == p.Info.PropertyName );
                            if( idx >= 0 ) exportables.RemoveAt( idx );
                        }
                    }
                }
                foreach( var kv in _newObjects )
                {
                    if( kv.Value == null || kv.Value.Count == 0 ) continue;
                    foreach( var exp in kv.Value )
                    {
                        object? propValue = exp.GetValue( kv.Key );
                        var pInfo = ensurePropertInfo( exp.Name );
                        _changeEvents.Add( new PropertyChangedEvent( kv.Key, pInfo.PropertyId, pInfo.PropertyName, propValue ) );
                    }
                }
                var result = new TransactionDoneEventArgs( domain,
                                                                 domain.FindPropertyId,
                                                                 _changeEvents.ToArray(),
                                                                 _commands,
                                                                 startTime,
                                                                 tranNum,
                                                                 rollbacked );
                Reset();
                return result;
            }

            /// <summary>
            /// Clears all events collected so far from the 3 internal lists and allocates a new empty command list for the next transaction.
            /// </summary>
            public void Reset()
            {
                _changeEvents.Clear();
                _newObjects.Clear();
                _propChanged.Clear();
                _commands = new List<ObservableDomainCommand>();
            }

            /// <summary>
            /// Gets whether the object has been created in the current transaction:
            /// it belongs to the _newObjects dictionary.
            /// </summary>
            /// <param name="o">The potential new object.</param>
            /// <returns>True if this is a new object. False if the object has been created earlier.</returns>
            internal bool IsNewObject( ObservableObject o ) => _newObjects.ContainsKey( o );

            /// <summary>
            /// Called when a new object is being created.
            /// </summary>
            /// <param name="o">The object itself.</param>
            /// <param name="objectId">The assigned object identifier.</param>
            /// <param name="exporter">The export driver of the object. Can be null.</param>
            internal void OnNewObject( ObservableObject o, ObservableObjectId objectId, IObjectExportTypeDriver exporter )
            {
                _changeEvents.Add( new NewObjectEvent( o, objectId ) );
                if( exporter != null )
                {
                    _newObjects.Add( o, exporter.ExportableProperties.ToList() );
                }
                else _newObjects.Add( o, null );
            }

            internal void OnDisposeObject( ObservableObject o )
            {
                if( IsNewObject( o ) )
                {
                    int idx = _changeEvents.IndexOf( e => e is NewObjectEvent n ? n.Object == o : false );
                    _changeEvents.RemoveAt( idx );
                    _newObjects.Remove( o );
                }
                else
                {
                    _changeEvents.Add( new DisposedObjectEvent( o ) );
                }
            }

            internal void OnNewProperty( ObservablePropertyChangedEventArgs info )
            {
                _changeEvents.Add( new NewPropertyEvent( info.PropertyId, info.PropertyName ) );
            }

            internal void OnPropertyChanged( ObservableObject o, ObservablePropertyChangedEventArgs p, object? after )
            {
                if( _propChanged.TryGetValue( p.GetObjectPropertyId( o ), out var c ) )
                {
                    c.FinalValue = after;
                }
                else
                {
                    c = new PropChanged( o, p, after );
                    _propChanged.Add( c.Key, c );
                }
            }

            internal ListRemoveAtEvent OnListRemoveAt( ObservableObject o, int index )
            {
                var e = new ListRemoveAtEvent( o, index );
                _changeEvents.Add( e );
                return e;
            }

            internal ListSetAtEvent OnListSetAt( ObservableObject o, int index, object value )
            {
                var e = new ListSetAtEvent( o, index, value );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionClearEvent OnCollectionClear( ObservableObject o )
            {
                var e = new CollectionClearEvent( o );
                _changeEvents.Add( e );
                return e;
            }

            internal ListInsertEvent OnListInsert( ObservableObject o, int index, object? item )
            {
                var e = new ListInsertEvent( o, index, item );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionMapSetEvent OnCollectionMapSet( ObservableObject o, object key, object? value )
            {
                var e = new CollectionMapSetEvent( o, key, value );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionRemoveKeyEvent OnCollectionRemoveKey( ObservableObject o, object key )
            {
                var e = new CollectionRemoveKeyEvent( o, key );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionAddKeyEvent OnCollectionAddKey( ObservableObject o, object key )
            {
                var e = new CollectionAddKeyEvent( o, key );
                _changeEvents.Add( e );
                return e;
            }

            internal void OnSendCommand( in ObservableDomainCommand command ) => _commands.Add( command );

        }

    }
}
