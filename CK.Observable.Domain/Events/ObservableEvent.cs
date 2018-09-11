using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public enum ObservableEventType
    {
        None,
        NewObject,
        DisposedObject,
        NewProperty,
        PropertyChanged,
        ListInsert,
        CollectionClear,
        ListRemoveAt,
        ListSetAt,
        CollectionRemoveKey,
        CollectionMapSet
    }

    public abstract class ObservableEvent
    {
        static readonly string[] _exportCodes =
            {
                null,
                "N",  // NewObject
                "D",  // DisposedObject
                "P",  // NewProperty
                "C",  // PropertyChanged
                "I",  // ListInsert
                "CL", // CollectionClear
                "R",  // ListRemoveAt
                "S",  // ListSetAt
                "K",  // CollectionRemoveKey
                "M"   // CollectionMapSet
            };

        public ObservableEvent( ObservableEventType type )
        {
            EventType = type;
        }

        public ObservableEventType EventType { get; }

        public void Export( ObjectExporter e )
        {
            e.Target.EmitStartObject( -1, ObjectExportedKind.List );
            e.Target.EmitString( _exportCodes[(int)EventType] );
            ExportEventData( e );
            e.Target.EmitEndObject( -1, ObjectExportedKind.List );
        }

        protected void ExportEventObject( ObjectExporter e, object o )
        {
            if( o is ObservableObject obs )
            {
                e.Target.EmitStartObject( -1, ObjectExportedKind.Object );
                e.Target.EmitPropertyName( ">" );
                e.Target.EmitInt32( obs.OId );
                e.Target.EmitEndObject( -1, ObjectExportedKind.Object );
            }
            else
            {
                e.ExportObject( o );
            }
        }

        protected abstract void ExportEventData( ObjectExporter e );
    }
}
