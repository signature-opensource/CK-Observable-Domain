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
        // This is used as a cache of "already done" job. Depending on the stage, keys are type and or strings and values
        // can be the instance, a type or an exception.
        readonly Dictionary<object,object?> _alreadyHandled;

        /// <summary>
        /// This list is:
        /// - filled by <see cref="DiscoverSidekicks"/> each time a new type appears that requires
        /// one or more sidekick to be available.
        /// - emptied by <see cref="CreateWaitingSidekicks"/> where the type or name (the object Item1) is resolved.
        /// </summary>
        readonly List<(object, bool)> _toInstantiate;

        /// <summary>
        /// This list is:
        /// - filled by <see cref="DiscoverSidekicks"/> each time a new object with ISidekickClientObject base interfaces appears.
        /// - emptied by <see cref="CreateWaitingSidekicks"/> (after having ensured that the sidekick instances are available)
        ///   where <see cref="ObservableDomainSidekick.RegisterClientObject(IActivityMonitor, IDisposableObject)"/> is called
        ///   with the IDisposableObject Item1.
        /// </summary>
        readonly List<(IDisposableObject, object[])> _toAutoregister;
        readonly List<ObservableDomainSidekick> _sidekicks;
        readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// A new instance is created at the end of each transaction if at least one new sidekick appeared.
        /// This is required because the <see cref="ExecuteCommands"/> is called outside of the domain lock.
        /// This is used to resolve sidekicks by type but also for broadcasting (to the Values).
        /// </summary>
        Dictionary<Type, ObservableDomainSidekick> _currentIndex;

        public SidekickManager( ObservableDomain domain, IServiceProvider sp )
        {
            _domain = domain;
            _alreadyHandled = new Dictionary<object, object?>();
            _toInstantiate = new List<(object, bool)>();
            _toAutoregister = new List<(IDisposableObject, object[])>();
            _sidekicks = new List<ObservableDomainSidekick>();
            _currentIndex = new Dictionary<Type, ObservableDomainSidekick>();
            _serviceProvider = sp;
        }

        /// <summary>
        /// Called for each new Internal or Observable object: associated sidekicks (type or name) of UseSidekickAttribute are
        /// captured (and whether they are optional or not).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="o">The object that appeared.</param>
        public void DiscoverSidekicks( IActivityMonitor monitor, IDisposableObject o )
        {
            // Consider the ObservableObject's type.
            Type t = o.GetType();
            if( !_alreadyHandled.TryGetValue( t, out var previouslyHandled ) )
            {
                // We have not seen this ObservableObject's type before.
                // 1 - We analyse its Sidekick attributes and populates _toInstantiate tuples.
                foreach( var attr in t.GetCustomAttributesData().Where( a => a.AttributeType == typeof( UseSidekickAttribute ) ) )
                {
                    object typeOrTypeName = attr.ConstructorArguments[0].Value;
                    var args = attr.NamedArguments;
                    bool optional = args.Count > 0 && args[0].TypedValue.Value.Equals( true );
                    monitor.Trace( $"Domain object '{t.Name}' wants to use {(optional ? "optional" : "required")} sidekick '{typeOrTypeName}'." );
                    _toInstantiate.Add( (typeOrTypeName, optional) );
                }
                // 2 - We analyse its ISidekickClientObject<> generic interfaces and populates _toInstantiate tuples.
                //     The list is of object because the array must be object[] since the types will be replaced
                //     with sidekick instances on the first registration (to avoid subsequent lookups).
                List<object>? sidekickTypes = null;
                foreach( var tI in t.GetInterfaces() )
                {
                    if( tI.IsGenericType && tI.GetGenericTypeDefinition() == typeof(ISidekickClientObject<>) )
                    {
                        if( sidekickTypes == null ) sidekickTypes = new List<object>();
                        var tSidekick = tI.GetGenericArguments()[0];
                        sidekickTypes.Add( tSidekick );
                        _toInstantiate.Add( (tSidekick, false) );
                    }
                }
                // We store either null or the array of ISidekickClientObject<> types (as objects) if there
                // are "interface typed sidekicks".
                previouslyHandled = sidekickTypes?.ToArray();
                _alreadyHandled.Add( t, previouslyHandled );
            }
            // Whether the objects's type was already known or not, a non null previouslyHandled array
            // indicates that this object must be registered onto one or more sidekicks.
            if( previouslyHandled != null )
            {
                _toAutoregister.Add( (o, (object[])previouslyHandled) );
            }
        }

        /// <summary>
        /// Instantiates sidekicks that have been discovered by <see cref="DiscoverSidekicks(IActivityMonitor, IDisposableObject)"/>.
        /// This never throws, but when false is returned, it means that (at least) one required sidekick failed
        /// to be instantiated: the exceptions are added to the <paramref name="errorCollector"/> and these are fatal
        /// errors that cancel the whole transaction.
        /// <para>
        /// This is called when <see cref="DomainView.EnsureSidekicks()"/> is called (typically from a <see cref="ISidekickClientObject{TSidekick}"/>
        /// object's constructor) or at the end the Modify session (if no transaction error occurred).
        /// </para>
        /// <para>
        /// When loading a domain (at the end of the deserialization of the graph), such errors don't prevent the load of
        /// the domain. Errors are logged and the cause should be seriously investigated.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="errorCollector">Error collector (note that errors are already logged).</param>
        /// <param name="finalCall">Whether this is called from the commit conclusion.</param>
        /// <returns>True on success, false if one required (non optional) sidekick failed to be instantiated.</returns>
        internal bool CreateWaitingSidekicks( IActivityMonitor monitor, Action<Exception> errorCollector, bool finalCall )
        {
            bool success = true;
            if( _toInstantiate.Count > 0 )
            {
                foreach( var r in _toInstantiate )
                {
                    success &= Register( monitor, r, errorCollector );
                }
                _toInstantiate.Clear();
            }
            if( _toAutoregister.Count > 0 )
            {
                foreach( var r in _toAutoregister )
                {
                    success &= AutoRegisterClientObject( monitor, r, errorCollector );
                }
                _toAutoregister.Clear();
            }
            if( finalCall && _currentIndex.Count != _sidekicks.Count )
            {
                _currentIndex = _sidekicks.ToDictionary( s => s.GetType() );
            }
            return success;
        }

        bool AutoRegisterClientObject( IActivityMonitor monitor, (IDisposableObject, object[]) r, Action<Exception> errorCollector )
        {
            bool success = true;
            for( int i = 0; i < r.Item2.Length; ++i )
            {
                var tOrS = r.Item2[i];
                try
                {
                    if( tOrS is ObservableDomainSidekick s )
                    {
                        s.RegisterClientObject( monitor, r.Item1 );
                    }
                    else
                    {
                        Debug.Assert( tOrS is Type );
                        Debug.Assert( _alreadyHandled.ContainsKey( tOrS ), "The type has been already registered. But it can be on error!" );
                        var mapped = _alreadyHandled[tOrS];
                        if( mapped is Exception ex )
                        {
                            success = false;
                            monitor.Fatal( $"Sidekick '{tOrS}' failed to instantiated.", ex );
                            errorCollector( ex );
                        }
                        else
                        {
                            Debug.Assert( mapped is ObservableDomainSidekick );
                            var h = (ObservableDomainSidekick)mapped;
                            // Changes the Type to the direct object: no more _alreadyHandled lookups.
                            r.Item2[i] = h;
                            h.RegisterClientObject( monitor, r.Item1 );
                        }
                    }
                }
                catch( Exception ex )
                {
                    monitor.Fatal( $"Failed to register object on sidekick '{tOrS}'.", ex );
                    success = false;
                    errorCollector( ex );
                }
            }
            return success;
        }

        bool Register( IActivityMonitor monitor, (object, bool) toInstantiate, Action<Exception> errorCollector )
        {
            using( monitor.OpenInfo( $"Registering {(toInstantiate.Item2 ? "optional" : "required")} sidekick '{toInstantiate.Item1}'." ) )
            {
                if( toInstantiate.Item1 is string name )
                {
                    return RegisterName( monitor, name, toInstantiate.Item2, errorCollector );
                }
                else
                {
                    return RegisterType( monitor, (Type)toInstantiate.Item1, toInstantiate.Item2, errorCollector );
                }
            }
        }

        bool RegisterName( IActivityMonitor monitor, string typeName, bool optional, Action<Exception> errorCollector )
        {
            var result = CheckAlreadyRegistered( monitor, typeName, optional, errorCollector );
            if( !result.HasValue )
            {
                try
                {
                    var type = SimpleTypeFinder.WeakResolver( typeName, throwOnError: true );
                    result = RegisterType( monitor, type, optional, errorCollector );
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

        bool RegisterType( IActivityMonitor monitor, Type type, bool optional, Action<Exception> errorCollector )
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
                monitor.Debug( "Already available." );
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

        internal void OnSuccessfulTransaction( in SuccessfulTransactionEventArgs result, ref List<CKExceptionData>? errors )
        {
            foreach( var h in _sidekicks )
            {
                try
                {
                    h.OnSuccessfulTransaction( in result );
                }
                catch( Exception ex )
                {
                    result.Monitor.Error( "Error while calling ObservableDomainSideKick.OnSuccessfulTransaction.", ex );
                    if( errors == null ) errors = new List<CKExceptionData>();
                    errors.Add( CKExceptionData.CreateFrom( ex ) );
                }
            }
        }

        /// <summary>
        /// Executes the commands by calling <see cref="ObservableDomainSidekick.ExecuteCommand(IActivityMonitor, in SidekickCommand)"/> for each sidekick.
        /// If a command is not executed because no sidekick has accepted it, this is an error that, just as other execution errors will
        /// appear in <see cref="TransactionResult.CommandHandlingErrors"/>.
        /// <para>
        /// This executes outside of any locks on the domain. Doamin's objects must not be touched in any way (read or write).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="r">The successful transaction result.</param>
        /// <param name="localPostActions">The registrar for post actions.</param>
        /// <param name="domainPostActions">The registrar for domain post actions.</param>
        /// <returns>Null on success, otherwise the command and their respective error.</returns>
        public IReadOnlyList<(object,CKExceptionData)>? ExecuteCommands( IActivityMonitor monitor,
                                                                         TransactionResult r,
                                                                         ActionRegistrar<PostActionContext> localPostActions,
                                                                         ActionRegistrar<PostActionContext> domainPostActions )
        {
            Debug.Assert( monitor != null );
            Debug.Assert( r.Success );
            List<(object, CKExceptionData)>? results = null;
            foreach( var c in r.Commands )
            {
                if( c.Command == ObservableDomain.SaveCommand ) continue;
                SidekickCommand cmd = new SidekickCommand( r.StartTimeUtc, r.CommitTimeUtc, c.Command, localPostActions, domainPostActions );
                bool errorTarget = false;
                bool foundHandler = false;
                ObservableDomainSidekick? known = c.KnownTarget as ObservableDomainSidekick;
                if( known == null )
                {
                    if( c.KnownTarget is Type t )
                    {
                        if( !_currentIndex.TryGetValue( t, out known ) )
                        {
                            if( results == null ) results = new List<(object, CKExceptionData)>();
                            results.Add( (c, CKExceptionData.Create( $"Sidekick type '{t}' not found. An error may have prevented its instantiation, see previous logs." )) );
                            errorTarget = true;
                        }
                    }
                    else if( c.KnownTarget is ISidekickLocator locator )
                    {
                        if( (known = locator.Sidekick) == null )
                        {
                            if( results == null ) results = new List<(object, CKExceptionData)>();
                            results.Add( (c, CKExceptionData.Create( $"SidekickLocator exposes a null Sidekick." )) );
                            errorTarget = true;
                        }
                    }
                }
                if( known != null )
                {
                    // Target found.
                    if( known.Domain != _domain )
                    {
                        if( results == null ) results = new List<(object, CKExceptionData)>();
                        results.Add( (c, CKExceptionData.Create( $"Domain mismatch error. Cannot execute a command by a sidekick of domain '{known.Domain.DomainName}' while in domain '{_domain.DomainName}'." )) );
                        known = null;
                    }
                    else
                    {
                        foundHandler = ExecuteCommand( monitor, known, in cmd, c, ref results );
                    }
                }
                else if( !errorTarget )
                {
                    if( c.KnownTarget != null )
                    {
                        if( results == null ) results = new List<(object, CKExceptionData)>();
                        results.Add( (c, CKExceptionData.Create( $"Target sidekick must be a ObservableDomainSidekick, a Type or a ISidekickLocator. Cannot handle instance of type '{c.KnownTarget.GetType().FullName}'." )) );
                    }
                    else
                    {
                        // Broadcast.
                        foreach( var h in _currentIndex.Values )
                        {
                            foundHandler |= ExecuteCommand( monitor, h, in cmd, c, ref results );
                        }
                    }
                }
                if( !foundHandler )
                {
                    var msg = $"No sidekick found to handle command type '{c.GetType().FullName}'.";
                    if( known != null )
                    {
                        msg += $" The presumably known target '{known}' rejected it.";
                    }
                    if( c.IsOptionalExecution )
                    {
                        monitor.Warn( msg );
                    }
                    else
                    {
                        monitor.Error( msg );
                        if( results == null ) results = new List<(object, CKExceptionData)>();
                        results.Add( (c, CKExceptionData.Create( msg )) );
                    }
                }
            }
            return results;
        }

        static bool ExecuteCommand(IActivityMonitor monitor, ObservableDomainSidekick h, in SidekickCommand cmd, ObservableDomainCommand c, ref List<(object, CKExceptionData)>? errors)
        {
            try
            {
                return h.ExecuteCommand( monitor, in cmd );
            }
            catch( Exception ex )
            {
                monitor.Error( "Error while handling command.", ex );
                if( errors == null ) errors = new List<(object, CKExceptionData)>();
                errors.Add( (c, CKExceptionData.CreateFrom( ex )) );
            }
            // If an error occured, it's because, somehow it has been handled.
            return true;
        }

        /// <summary>
        /// Clears the registered sidekicks information and disposes all existing sidekick instances.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        public void Clear( IActivityMonitor monitor )
        {
            _alreadyHandled.Clear();
            _toInstantiate.Clear();
            _toAutoregister.Clear();
            using( monitor.OpenInfo( $"Disposing {_sidekicks.Count} sidekicks." ) )
            {
                // Reverse the disposing... Doesn't cost a lot and, who knows,
                // disposing first a sidekick that has been activated after another one
                // may help...
                int i = _sidekicks.Count;
                while( i > 0 )
                {
                    var h = _sidekicks[--i];
                    try
                    {
                        h.OnDomainCleared( monitor );
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( $"While disposing '{h}'.", ex );
                    }
                }
            }
            _sidekicks.Clear();
            _currentIndex.Clear();
        }

        internal void Load( BinaryDeserializer r )
        {
            r.ReadByte(); // Version.
        }

        internal void Save( BinarySerializer w )
        {
            w.Write( (byte)0 );
        }

    }
}
