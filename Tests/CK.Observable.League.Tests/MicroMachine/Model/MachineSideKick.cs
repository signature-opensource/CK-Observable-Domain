using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.MicroMachine
{
    public class MachineSideKick : ObservableDomainSidekick
    {
        readonly Dictionary<string, MicroBridge> _objects;

        public class MicroBridge
        {
            public readonly Machine Machine;
            public readonly MachineSideKick MachineSideKick;

            public MicroBridge( Machine m, MachineSideKick k )
            {
                Machine = m;
                MachineSideKick = k;
            }
        }

        public MachineSideKick( ObservableDomain d )
            : base( d )
        {
            _objects = new Dictionary<string, MicroBridge>();
        }

        protected override void OnDomainCleared( IActivityMonitor monitor )
        {
        }

        protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
        {
            if( command.Command is MachineCommand c )
            {
                monitor.Info( $"Sidekick handled MachineCommand '{c.BugOrNot}'.");
                if( c.BugOrNot == "bug" ) throw new CKException( "Sorry, I'm asked to bug." );
                return true;
            }
            return false;
        }

        protected override void RegisterClientObject( IActivityMonitor monitor, IDestroyable o )
        {
            if( o is Machine m )
            {
                var b = new MicroBridge( m, this );
                _objects.Add( m.Name, b );
                m.Destroyed += MachineDestroyed;
                m.BridgeToTheSidekick = b;
            }
        }

        private void MachineDestroyed( object sender, ObservableDomainEventArgs e )
        {
            _objects.Remove( ((Machine)sender).Name );
        }
    }
}
