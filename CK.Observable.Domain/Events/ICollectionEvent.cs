using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    interface ICollectionEvent
    {
        ObservableObject Object { get; }
    }
}