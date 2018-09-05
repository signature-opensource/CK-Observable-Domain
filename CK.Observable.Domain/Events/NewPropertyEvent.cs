using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{

    public class NewPropertyEvent : ObservableEvent
    {
        public string Name { get; }

        public int PropertyId { get; }

        public NewPropertyEvent( int id, string name )
            : base( ObservableEventType.NewProperty )
        {
            PropertyId = id;
            Name = name;
        }


        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitString( Name );
            e.Target.EmitInt32( PropertyId );
        }

        public override string ToString() => $"{EventType} {Name} -> {PropertyId}.";

    }

}
