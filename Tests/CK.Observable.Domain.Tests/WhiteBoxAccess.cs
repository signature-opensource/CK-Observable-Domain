using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests
{
    static class WhiteBoxAccess
    {
        static FieldInfo _oIdField = typeof( ObservableObject ).GetField( "_id", BindingFlags.Instance | BindingFlags.NonPublic );
        static FieldInfo _freeListField = typeof( ObservableDomain ).GetField( "_freeList", BindingFlags.Instance | BindingFlags.NonPublic );

        public static int GetOId( this ObservableObject o )
        {
            return (int)_oIdField.GetValue( o );
        }

        public static Stack<int> GetFreeList( this ObservableDomain d )
        {
            return (Stack<int>)_freeListField.GetValue( d );
        }

    }
}
