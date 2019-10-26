using System.Collections.Generic;
using System.Reflection;

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

        public static List<int> GetFreeList( this ObservableDomain d )
        {
            return (List<int>)_freeListField.GetValue( d );
        }

    }
}
