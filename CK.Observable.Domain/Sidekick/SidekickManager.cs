using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

#nullable enable

namespace CK.Observable
{
    /// <summary>
    /// Sidekicks of a domain interact with the external world.
    /// </summary>
    class SidekickManager
    {
        readonly ObservableDomain _domain;
        // This is used as a cache of "already done" job. Depending on the stage, different type of keys and values
        // are used.
        readonly Dictionary<object,object?> _alreadyHandled;
        readonly List<(object, bool)> _toInstantiate;
        readonly List<ObservableDomainSidekick> _sidekicks;
        readonly IServiceProvider _serviceProvider;

        public SidekickManager( ObservableDomain domain, IServiceProvider sp )
        {
            _domain = domain;
            _alreadyHandled = new Dictionary<object, object?>();
            _toInstantiate = new List<(object, bool)>();
            _sidekicks = new List<ObservableDomainSidekick>();
            _serviceProvider = sp;
        }

        /// <summary>
        /// Called for each Internal or Observable object: associated sidekicks (type or name) of UseSidekickAttribute are
        /// captured (and whether they are optional or not).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="o">The object that appeared.</param>
        public void DiscoverSidekicks( IActivityMonitor monitor, IDisposableObject o )
        {
            Type t = o.GetType();
            if( _alreadyHandled.TryAdd( t, null ) )
            {
                foreach( var attr in t.GetCustomAttributesData().Where( a => a.AttributeType == typeof( UseSidekickAttribute ) ) )
                {
                    object typeOrName = attr.ConstructorArguments[0].Value;
                    var args = attr.NamedArguments;
                    bool optional = args.Count > 0 && args[0].TypedValue.Value.Equals( true );
                    monitor.Trace( $"Domain object '{t.Name}' wants to use {(optional ? "optional" : "required")} sidekick '{typeOrName}'." );
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
        /// <returns>True on success, false if one required (non optional) sidekick failed to be instantiated.</returns>
        public bool CreateWaitingSidekicks( IActivityMonitor monitor, Action<Exception> errorCollector )
        {
            bool success = true;
            if( _toInstantiate.Count > 0 )
            {
                foreach( var r in _toInstantiate )
                {
                    using( monitor.OpenInfo( $"Registering {(r.Item2 ? "optional" : "required")} sidekick '{r.Item1}'." ) )
                    {
                        if( r.Item1 is string name )
                        {
                            success &= Register( monitor, name, r.Item2, errorCollector );
                        }
                        else
                        {
                            success &= Register( monitor, (Type)r.Item1, r.Item2, errorCollector );
                        }
                    }
                }
                _toInstantiate.Clear();
            }
            return success;
        }

        bool Register( IActivityMonitor monitor, string typeName, bool optional, Action<Exception> errorCollector )
        {
            var result = CheckAlreadyRegistered( monitor, typeName, optional, errorCollector );
            if( !result.HasValue )
            {
                try
                {
                    var type = SimpleTypeFinder.WeakResolver( typeName, throwOnError: true );
                    result = Register( monitor, type, optional, errorCollector );
                    if( !result.Value )
                    {
                        // Also associates the exception to the type name.
                        _alreadyHandled.Add( typeName, _alreadyHandled[type] );
                    }
                }
                catch( Exception ex )
                {
                    _alreadyHandled.Add( typeName, ex );
                    result = HandleError( monitor, typeName, optional, errorCollector, ex );
                }

            }
            return result.Value;
        }

        bool Register( IActivityMonitor monitor, Type type, bool optional, Action<Exception> errorCollector )
        {
            var result = CheckAlreadyRegistered( monitor, type, optional, errorCollector );
            if( !result.HasValue )
            {
                try
                {
                    var h = (ObservableDomainSidekick?)SimpleObjectActivator.Create( monitor, type, _serviceProvider, false, new object[] { monitor, _domain } );
                    if( h == null )
                    {
                        throw new Exception( $"Unable to instantiate '{type.FullName}' type." );
                    }
                    _alreadyHandled.Add( type, h );
                    _sidekicks.Add( h );
                    result = true;
                }
                catch( Exception ex )
                {
                    _alreadyHandled.Add( type, ex );
                    result = HandleError( monitor, type, optional, errorCollector, ex );
                }
            }
            return result.Value;
        }

        bool? CheckAlreadyRegistered( IActivityMonitor monitor, object typeOrName, bool optional, Action<Exception> errorCollector )
        {
            if( _alreadyHandled.TryGetValue( typeOrName, out var cache ) )
            {
                // Previous attempt failed.
                if( cache is Exception ex )
                {
                    monitor.Error( "This sidekick instantiation previousy failed." );
                    return HandleError( monitor, typeOrName, optional, errorCollector, ex );
                }
                monitor.Trace( "Already available." );
                // We know this type of sidekick.
                return true;
            }
            return null;
        }

        bool HandleError( IActivityMonitor monitor, object sidekick, bool optional, Action<Exception> errorCollector, Exception ex )
        {
            if( !optional )
            {
                monitor.Fatal( $"While registering required sidekick '{sidekick}'.", ex );
                errorCollector( ex );
                return false;
            }
            monitor.Warn( $"While registering optional sidekick '{sidekick}'.", ex );
            return true;
        }


        /// <summary>
        /// Executes the commands by calling <see cref="ObservableDomainSidekick.ExecuteCommand(IActivityMonitor, in SidekickCommand)"/> for each sidekick.
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

        /// <summary>
        /// Clears the registered sidekicks.
        /// </summary>
        public void Clear()
        {
            _alreadyHandled.Clear();
            _toInstantiate.Clear();
            _sidekicks.Clear();
        }

    }
}
