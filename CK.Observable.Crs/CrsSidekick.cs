using System;
using CK.Core;
using CK.Crs;
using CK.Crs.CommandDiscoverer.Attributes;
using CK.Observable;


namespace CK.Observable
{
    /// <summary>
    /// Sidekick that sends commands to the <see cref="ICommandDispatcher"/>.
    /// Observable or Internal objects that send <see cref="ICrsCommand"/> should 
    /// be decorated with <see cref="UseSidekickAttribute">[UseSidekick( typeof(CrsSideKick) ]</see>.
    /// </summary>
    public sealed class CrsSidekick : ObservableDomainSidekick
    {
        readonly ICommandDispatcher _commandDispatcher;

        /// <summary>
        /// Initializes a new CrsSideKick.
        /// </summary>
        /// <param name="manager">The domain's sidekick manager.</param>
        /// <param name="commandDispatcher"></param>
        public CrsSidekick( IObservableDomainSidekickManager manager, ICommandDispatcher commandDispatcher )
            : base( manager )
        {
            _commandDispatcher = commandDispatcher;
        }

        protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
        {
            if( command.Command is ICrsCommand cmd )
            {
                var t = cmd.GetType();
                var aName = (CommandNameAttribute?)Attribute.GetCustomAttribute( t, typeof( CommandNameAttribute ) );
                if( aName == null ) throw new CKException( $"ICrsCommand '{t.FullName}' must be decorated with [CommandName( \"...\" )] attribute." );
                command.PostActions.Add( _ => _commandDispatcher.Send( Guid.NewGuid(), cmd, aName.Name, CallerId.None ) );
                return true;
            }
            return false;
        }

        protected override void OnUnload( IActivityMonitor monitor )
        {
        }

        protected override void RegisterClientObject( IActivityMonitor monitor, IDestroyable o )
        {
        }
    }
}
