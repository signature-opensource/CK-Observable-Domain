using CK.Core;
using CK.Cris;

namespace CK.Observable.Cris
{
    /// <summary>
    /// More than minimal sidekick: it simply sends the commands to the <see cref="CrisBackgroundExecutorService"/> without
    /// returning the <see cref="IExecutedCommand"/> to the domain object that sent it.
    /// This has to be designed.
    /// <para>
    /// Observable or Internal objects that send <see cref="IAbstractCommand">Cris commands</see> should 
    /// be decorated with <see cref="UseSidekickAttribute">[UseSidekick( typeof(CrisSideKick) ]</see>.
    /// </para>
    /// </summary>
    public sealed class CrisSidekick : ObservableDomainSidekick
    {
        readonly CrisBackgroundExecutorService _crisBackgroundExecutor;

        /// <summary>
        /// Initializes a new CrisSideKick.
        /// </summary>
        /// <param name="manager">The domain's sidekick manager.</param>
        /// <param name="crisBackgroundExecutor">The background executor.</param>
        public CrisSidekick( IObservableDomainSidekickManager manager, CrisBackgroundExecutorService crisBackgroundExecutor )
            : base( manager )
        {
            _crisBackgroundExecutor = crisBackgroundExecutor;
        }

        protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
        {
            if( command.Command is IAbstractCommand cmd )
            {
                command.DomainPostActions.Add( ctx => _crisBackgroundExecutor.Submit( ctx.Monitor, cmd, null ) );
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
