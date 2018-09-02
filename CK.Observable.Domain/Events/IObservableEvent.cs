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
        CollectionRemoveKey,
    }

    public interface IObservableEvent
    {
        ObservableEventType EventType { get; }
    }
}
