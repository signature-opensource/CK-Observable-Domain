using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Sidekicks of a domain interact with the external world.
    /// </summary>
    class SidekickManager
    {
        readonly ObservableDomain _domain;
        readonly HashSet<object> _alreadyHandled;
        readonly List<(object, bool)> _toInstantiate;
        readonly List<SidekickBase> _sidekicks;
        readonly IServiceProvider _serviceProvider;

        public SidekickManager( ObservableDomain domain, IServiceProvider sp )
        {
            _domain = domain;
            _alreadyHandled = new HashSet<object>();
            _toInstantiate = new List<(object, bool)>();
            _sidekicks = new List<SidekickBase>();
            _serviceProvider = sp;
        }

        /// <summary>
        /// Called for each Internal or Observable object: associated sidekicks (type or name) of UseSidekickAttribute are
        /// captured (and whether they are optional or not).
        /// </summary>
        /// <param name="o">The object that appeared.</param>
        public void DiscoverSidekicks( IDisposableObject o )
        {
            Type t = o.GetType();
            if( _alreadyHandled.Add( t ) )
            {
                foreach( var attr in t.GetCustomAttributesData().Where( a => a.AttributeType == typeof( UseSidekickAttribute ) ) )
                {
                    object typeOrName = attr.ConstructorArguments[0].Value;
                    var args = attr.NamedArguments;
                    bool optional = args.Count > 0 && args[0].TypedValue.Value.Equals( true );
                    _toInstantiate.Add( (typeOrName, optional) );
                }
            }
        }

        /// <summary>
        /// Instantiates sidekicks that have been discovered by <see cref="DiscoverSidekicks(IDisposableObject)"/>.
        /// This never throws, but when false is returned, it means that (at least) one required sidekick failed
        /// to be instantiated: the exceptions are added to the <paramref name="errorCollector"/> and these are
        /// fatal errors that cancel the whole transaction.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        public bool CreateWaitingSidekicks( IActivityMonitor monitor, Action<Exception> errorCollector )
        {
            bool success = true;
            if( _toInstantiate.Count > 0 )
            {
                foreach( var r in _toInstantiate )
                {
                    if( _alreadyHandled.Add( r ) )
                    {
                        bool optional = r.Item2;
                        using( monitor.OpenInfo( $"Registering sidekick '{r.Item1}'." ) )
                        {
                            try
                            {
                                Type sidekick = r.Item1 as Type;
                                if( sidekick == null )
                                {
                                    sidekick = SimpleTypeFinder.WeakResolver( (string)r.Item1, throwOnError: !optional );
                                    Debug.Assert( sidekick != null );
                                }
                                if( _alreadyHandled.Add( sidekick ) )
                                {
                                    var h = (SidekickBase)SimpleObjectActivator.Create( monitor, sidekick, _serviceProvider, false, new object[] { monitor, _domain } );
                                    if( h == null )
                                    {
                                        throw new Exception( $"Unable to instantiate '{sidekick.FullName}' type." );
                                    }
                                    _sidekicks.Add( h );
                                }
                            }
                            catch( Exception ex )
                            {
                                if( !optional )
                                {
                                    monitor.Fatal( $"While registering required sidekick '{r.Item1}'.", ex );
                                    errorCollector( ex );
                                    success = false;
                                }
                                else
                                {
                                    monitor.Warn( $"While registering optional sidekick '{r.Item1}'.", ex );
                                }
                            }
                        }
                    }
                }
                _toInstantiate.Clear();
            }
            return success;
        }

        /// <summary>
        /// Executes the commands by calling <see cref="SidekickBase.ExecuteCommand(IActivityMonitor, in SidekickCommand)"/> for each sidekick.
        /// If a command is not executed because no sidekick has accepted it, this is an error that, just as other execution errors will
        /// appear in <see cref="TransactionResult.CommandErrors"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="r">The successful transaction result.</param>
        /// <param name="postActions">The registrar for post actions.</param>
        /// <returns>Null on success, otherwise the command and their respective error.</returns>
        public IReadOnlyList<(object,CKExceptionData)>? ExecuteCommands( IActivityMonitor monitor, TransactionResult r, ActionRegistrar<PostActionContext> postActions )
        {
            Debug.Assert( monitor != null );
            Debug.Assert( r.Success );
            List<(object, CKExceptionData)> results = null;
            foreach( var c in r.Commands )
            {
                SidekickCommand cmd = new SidekickCommand( r.StartTimeUtc, r.CommitTimeUtc, c, postActions );
                bool foundHandler = false;
                foreach( var h in _sidekicks )
                {
                    try
                    {
                        foundHandler |= h.ExecuteCommand( monitor, in cmd );
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( "Error while handling command.", ex );
                        if( results == null ) results = new List<(object, CKExceptionData)>();
                        results.Add( (c, CKExceptionData.CreateFrom( ex ) ) );
                        break;
                    }
                }
                if( !foundHandler )
                {
                    var msg = $"No sidekick found to handle command type '{c.GetType().FullName}'.";
                    monitor.Error( msg );
                    if( results == null ) results = new List<(object, CKExceptionData)>();
                    results.Add( (c, CKExceptionData.Create( msg ) ) );
                }
            }
            return results;
        }

    }
}
